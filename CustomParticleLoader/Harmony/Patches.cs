﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

[HarmonyPatch(typeof(GameManager))]
class ExplosionEffectPatch
{
    public static void SendCustomExplosionPackage(int _clrIdx, Vector3 _center, Vector3i _blockpos, Quaternion _rotation, ExplosionData _explosionData, int _playerId, ItemValue _itemValueExplosive, List<BlockChangeInfo> _explosionChanges, GameObject result)
    {
        uint id = CustomParticleEffectLoader.LastInitializedComponent != null ? CustomParticleEffectLoader.LastInitializedComponent.CurrentExplosionParams._explId : uint.MaxValue;
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageExplosionParams>().Setup(_clrIdx, _center, _blockpos, _rotation, _explosionData, _playerId, id, _itemValueExplosive, _explosionChanges, result), true);
    }

    [HarmonyPatch("explode")]
    [HarmonyPrefix]
    private static bool explode_Prefix(int _clrIdx, Vector3 _worldPos, Vector3i _blockPos, Quaternion _rotation, ExplosionData _explosionData, int _playerId, ItemValue _itemValueExplosive, out bool __state)
    {
        __state = false;
        int index = _explosionData.ParticleIndex;
        //Log.Out(_worldPos.ToString() + _blockPos.ToString());
        //Log.Out("Particle index:" + index.ToString());
        if (index >= WorldStaticData.prefabExplosions.Length)
        {
            //Log.Out("Retrieving particle index:" + index.ToString());
            bool flag = CustomParticleEffectLoader.GetCustomParticleComponents(index, out CustomParticleComponents components);
            if(flag && components != null)
            {
                //Log.Out("Retrieved particle index:" + index.ToString());
                //_explosionData = components.BoundExplosionData;
                components.CurrentExplosionParams = new ExplosionParams(_clrIdx, _worldPos, _blockPos, _rotation, _explosionData, _playerId, CustomParticleEffectLoader.NextExplosionIndex++);
                //Log.Out("params:" + _clrIdx + _blockPos + _playerId + _rotation + _worldPos + _explosionData.ParticleIndex);
                //Log.Out("params:" + components.CurrentExplosionParams._clrIdx + components.CurrentExplosionParams._blockPos + components.CurrentExplosionParams._playerId + components.CurrentExplosionParams._rotation + components.CurrentExplosionParams._worldPos + components.CurrentExplosionParams._explosionData.ParticleIndex);
                components.CurrentItemValue = _itemValueExplosive;
                CustomParticleEffectLoader.LastInitializedComponent = components;
                __state = true;
            }
            else
                Log.Warning("Failed to retrieve particle on server! Index:" + index.ToString());
        }
        return true;
    }

    [HarmonyPatch("explode")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> explode_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        MethodInfo mtd_sendpackage = AccessTools.Method(typeof(ConnectionManager), nameof(ConnectionManager.SendPackage), new Type[] { typeof(NetPackage), typeof(bool), typeof(int), typeof(int), typeof(int), typeof(int) });

        for (int i = 0, totali = codes.Count; i < totali; i++)
        {
            if(codes[i].opcode == OpCodes.Callvirt && codes[i].Calls(mtd_sendpackage))
            {
                codes.InsertRange(i + 1, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Ldarg_3),
                    new CodeInstruction(OpCodes.Ldarg, 4),
                    new CodeInstruction(OpCodes.Ldarg, 5),
                    new CodeInstruction(OpCodes.Ldarg, 6),
                    new CodeInstruction(OpCodes.Ldarg, 7),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(GameManager), "tempExplPositions"),
                    new CodeInstruction(OpCodes.Ldloc_1),
                    CodeInstruction.Call(typeof(ExplosionEffectPatch), nameof(ExplosionEffectPatch.SendCustomExplosionPackage))
                });
                codes.RemoveRange(i - 20, 21);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch("explode")]
    [HarmonyPostfix]
    private static void explode_Postfix(bool __state)
    {
        if (!__state)
            return;
        CustomParticleEffectLoader.LastInitializedComponent = null;
    }

    [HarmonyPatch(nameof(GameManager.ExplosionClient))]
    [HarmonyPostfix]
    private static void ExplosionClient_Postfix(GameManager __instance, ref GameObject __result, Vector3 _center, Quaternion _rotation, int _index, int _blastPower, float _blastRadius)
    {
        if (__result != null || __instance.World == null)
            return;

        CustomParticleComponents components = CustomParticleEffectLoader.LastInitializedComponent;
        /*
        if(SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient && _index >= WorldStaticData.prefabExplosions.Length && components == null)
        {
            bool flag = CustomParticleEffectLoader.GetCustomParticleComponents(_index, out components);
            CustomParticleEffectLoader.LastInitializedComponent = components;
            if (!flag || components == null)
                Log.Warning("Failed to retrieve particle on client! Index:" + _index.ToString());
        }
        */
        if (components != null)
        {
            __result = CustomParticleEffectLoader.InitializeParticle(components, _center - Origin.position, _rotation);
            ApplyExplosionForce.Explode(_center, (float)_blastPower, _blastRadius);
            //CustomParticleEffectLoader.LastInitializedComponent = null;
        }
        else
            Log.Warning("Failed to retrieve particle on client! Index:" + _index.ToString());
    }

    [HarmonyPatch(nameof(GameManager.Disconnect))]
    [HarmonyPostfix]
    private static void Disconnect_Postfix()
    {
        CustomParticleEffectLoader.destroyAllParticles();
    }

    [HarmonyPatch(nameof(GameManager.PlayerSpawnedInWorld))]
    [HarmonyPostfix]
    private static void PlayerSpawnedInWorld_Postfix(ClientInfo _cInfo, RespawnType _respawnReason)
    {
        if(_cInfo != null && _cInfo.entityId != -1 && (_respawnReason == RespawnType.EnterMultiplayer || _respawnReason == RespawnType.JoinMultiplayer))
            CustomParticleEffectLoader.OnClientConnected(_cInfo);
    }
}
[HarmonyPatch]
class ExplosionParsePatch
{
    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.Init))]
    [HarmonyPrefix]
    private static bool Init_ItemClass_Prefix(ItemClass __instance)
    {
        if(CustomParticleEffectLoader.parseParticleData(ref __instance.Properties))
            CustomParticleEffectLoader.LastInitializedComponent.BoundItemClass = __instance;
        return true;
    }

    [HarmonyPatch(typeof(ItemAction), nameof(ItemAction.ReadFrom))]
    [HarmonyPrefix]
    private static bool ReadFrom_ItemAction_Prefix(ItemAction __instance, ref DynamicProperties _props)
    {
        if(CustomParticleEffectLoader.parseParticleData(ref _props))
            CustomParticleEffectLoader.LastInitializedComponent.BoundItemClass = __instance.item;
        return true;
    }

    [HarmonyPatch(typeof(Block), nameof(Block.Init))]
    [HarmonyPrefix]
    private static bool Init_Block_Prefix(Block __instance)
    {
        CustomParticleEffectLoader.parseParticleData(ref __instance.Properties);
        return true;
    }

    [HarmonyPatch(typeof(EntityClass), nameof(EntityClass.Init))]
    [HarmonyPrefix]
    private static bool Init_EntityClass_Prefix(EntityClass __instance)
    {
        CustomParticleEffectLoader.parseParticleData(ref __instance.Properties);
        return true;
    }
}