using HarmonyLib;
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
        uint id = CustomExplosionManager.LastInitializedComponent != null ? CustomExplosionManager.LastInitializedComponent.CurrentExplosionParams._explId : uint.MaxValue;
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
            bool flag = CustomExplosionManager.GetCustomParticleComponents(index, out ExplosionComponent components);
            if(flag && components != null)
            {
                //Log.Out("Retrieved particle index:" + index.ToString());
                //_explosionData = components.BoundExplosionData;
                components.CurrentExplosionParams = new ExplosionParams(_clrIdx, _worldPos, _blockPos, _rotation, _explosionData, _playerId, CustomExplosionManager.NextExplosionIndex++);
                //Log.Out("params:" + _clrIdx + _blockPos + _playerId + _rotation + _worldPos + _explosionData.ParticleIndex);
                //Log.Out("params:" + components.CurrentExplosionParams._clrIdx + components.CurrentExplosionParams._blockPos + components.CurrentExplosionParams._playerId + components.CurrentExplosionParams._rotation + components.CurrentExplosionParams._worldPos + components.CurrentExplosionParams._explosionData.ParticleIndex);
                components.CurrentItemValue = _itemValueExplosive;
                CustomExplosionManager.PushLastInitComponent(components);
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
        CustomExplosionManager.PopLastInitComponent();
    }

    [HarmonyPatch(nameof(GameManager.ExplosionClient))]
    [HarmonyPostfix]
    private static void ExplosionClient_Postfix(GameManager __instance, ref GameObject __result, Vector3 _center, Quaternion _rotation, int _index, int _blastPower, float _blastRadius)
    {
        if (__result != null || __instance.World == null)
            return;

        ExplosionComponent components = CustomExplosionManager.LastInitializedComponent;
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
            ApplyExplosionForce.Explode(_center, (float)_blastPower, _blastRadius);
            __result = CustomExplosionManager.InitializeParticle(components, _center - Origin.position, _rotation);
            //CustomParticleEffectLoader.LastInitializedComponent = null;
        }
        else
            Log.Warning("Failed to retrieve particle on client! Index:" + _index.ToString());
    }

    [HarmonyPatch(nameof(GameManager.Disconnect))]
    [HarmonyPostfix]
    private static void Disconnect_Postfix()
    {
        CustomExplosionManager.destroyAllParticles();
    }

    /*
    [HarmonyPatch(nameof(GameManager.PlayerSpawnedInWorld))]
    [HarmonyPostfix]
    private static void PlayerSpawnedInWorld_Postfix(ClientInfo _cInfo, RespawnType _respawnReason)
    {
        if(SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && _cInfo != null && _cInfo.entityId != -1 && (_respawnReason == RespawnType.EnterMultiplayer || _respawnReason == RespawnType.JoinMultiplayer))
            CustomParticleEffectLoader.OnClientConnected(_cInfo);
    }
    */
}

[HarmonyPatch(typeof(NetEntityDistribution), nameof(NetEntityDistribution.Add), new Type[] {typeof(Entity)})]
class ExplosionSyncPatch
{
    private static void Postfix(Entity _e)
    {
        if(_e is EntityPlayer)
            CustomExplosionManager.OnClientConnected(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(_e.entityId));
    }
}


[HarmonyPatch]
class ExplosionParsePatch
{
    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.Init))]
    [HarmonyPrefix]
    private static bool Init_ItemClass_Prefix(ItemClass __instance)
    {
        if(CustomExplosionManager.parseParticleData(ref __instance.Properties))
        {
            CustomExplosionManager.LastInitializedComponent.BoundItemClass = __instance;
            CustomExplosionManager.PopLastInitComponent();
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemAction), nameof(ItemAction.ReadFrom))]
    [HarmonyPrefix]
    private static bool ReadFrom_ItemAction_Prefix(ItemAction __instance, ref DynamicProperties _props)
    {
        if(CustomExplosionManager.parseParticleData(ref _props))
        {
            CustomExplosionManager.LastInitializedComponent.BoundItemClass = __instance.item;
            CustomExplosionManager.PopLastInitComponent();
        }
        return true;
    }

    [HarmonyPatch(typeof(Block), nameof(Block.Init))]
    [HarmonyPrefix]
    private static bool Init_Block_Prefix(Block __instance)
    {
        if(CustomExplosionManager.parseParticleData(ref __instance.Properties))
            CustomExplosionManager.PopLastInitComponent();
        return true;
    }

    [HarmonyPatch(typeof(EntityClass), nameof(EntityClass.Init))]
    [HarmonyPrefix]
    private static bool Init_EntityClass_Prefix(EntityClass __instance)
    {
        if (CustomExplosionManager.parseParticleData(ref __instance.Properties))
            CustomExplosionManager.PopLastInitComponent();
        return true;
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnEntityDeath))]
class EntityExplosionPatch
{
    private static void Postfix(EntityAlive __instance)
    {
        if (__instance.isEntityRemote || __instance is EntityCar || __instance is EntityZombieCop || EntityClass.list[__instance.entityClass].explosionData.ParticleIndex <= 0)
            return;

        GameManager.Instance.ExplosionServer(0, __instance.GetPosition(), World.worldToBlockPos(__instance.GetPosition()), Quaternion.identity, EntityClass.list[__instance.entityClass].explosionData, __instance.entityId, 0f, false, null);
    }
}

[HarmonyPatch(typeof(Explosion), nameof(Explosion.AttackEntites))]
class ExplosionAttackPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new List<CodeInstruction>(instructions);

        FieldInfo fld_hit_entities = typeof(Explosion).GetField("hitEntities", BindingFlags.NonPublic | BindingFlags.Static);
        Type entityDict = fld_hit_entities.FieldType;

        LocalBuilder lbd_entity_dict = generator.DeclareLocal(entityDict);

        codes.InsertRange(0, new CodeInstruction[]
        {
            new CodeInstruction(OpCodes.Newobj, entityDict.GetConstructor(new Type[]{ })),
            new CodeInstruction(OpCodes.Stloc_S, lbd_entity_dict)
        });

        for(int i = 0; i < codes.Count; ++i)
        {
            if (codes[i].opcode == OpCodes.Ldsfld && codes[i].LoadsField(fld_hit_entities))
            {
                codes[i].opcode = OpCodes.Ldloc_S;
                codes[i].operand = lbd_entity_dict;
            }
        }

        return codes;
    }
}
