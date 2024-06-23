using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

[HarmonyPatch(typeof(GameManager))]
internal class ExplosionEffectPatch
{
    public static void SendCustomExplosionPackage(int _clrIdx, Vector3 _center, Vector3i _blockpos, Quaternion _rotation, ExplosionData _explosionData, int _playerId, ItemValue _itemValueExplosive, List<BlockChangeInfo> _explosionChanges, GameObject result)
    {
        uint id = CustomExplosionManager.LastInitializedComponent != null ? CustomExplosionManager.LastInitializedComponent.CurrentExplosionParams._explId : uint.MaxValue;
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageExplosionParams>().Setup(_clrIdx, _center, _blockpos, _rotation, _explosionData, _playerId, id, _itemValueExplosive, _explosionChanges, result), true);
    }

    private struct ExplodeState
    {
        public bool useCustom;
        public int playerLayer;
    }

    [HarmonyPatch(nameof(GameManager.explode))]
    [HarmonyPrefix]
    private static bool explode_Prefix(GameManager __instance, int _clrIdx, Vector3 _worldPos, Vector3i _blockPos, Quaternion _rotation, ExplosionData _explosionData, int _entityId, ItemValue _itemValueExplosionSource, out ExplodeState __state)
    {
        __state = new ExplodeState()
        {
            useCustom = false,
            playerLayer = -1,
        };
        int index = _explosionData.ParticleIndex;
        //Log.Out(_worldPos.ToString() + _blockPos.ToString());
        //Log.Out("Particle index:" + index.ToString());
        if (index >= WorldStaticData.prefabExplosions.Length)
        {
            //Log.Out("Retrieving particle index:" + index.ToString());
            bool flag = CustomExplosionManager.GetCustomParticleComponents(index, out ExplosionComponent components);
            if (flag && components != null)
            {
                //Log.Out("Retrieved particle index:" + index.ToString());
                //_explosionData = components.BoundExplosionData;
                ExplosionValue value = new ExplosionValue()
                {
                    Component = components,
                    CurrentExplosionParams = new ExplosionParams(_clrIdx, _worldPos, _blockPos, _rotation, _explosionData, _entityId, CustomExplosionManager.NextExplosionIndex++),
                    CurrentItemValue = _itemValueExplosionSource
                };
                //Log.Out("params:" + _clrIdx + _blockPos + _playerId + _rotation + _worldPos + _explosionData.ParticleIndex);
                //Log.Out("params:" + components.CurrentExplosionParams._clrIdx + components.CurrentExplosionParams._blockPos + components.CurrentExplosionParams._playerId + components.CurrentExplosionParams._rotation + components.CurrentExplosionParams._worldPos + components.CurrentExplosionParams._explosionData.ParticleIndex);
                CustomExplosionManager.PushLastInitComponent(value);
                __state.useCustom = true;
            }
#if DEBUG
            else
                Log.Warning("Failed to retrieve particle on server! Index:" + index.ToString());
#endif
        }
        EntityPlayerLocal player = __instance.World.GetPrimaryPlayer();
        if (player != null)
        {
            __state.playerLayer = player.GetModelLayer();
            if (__state.playerLayer != 24)
            {
                player.SetModelLayer(24, false);
            }
            else
            {
                __state.playerLayer = -1;
            }
        }
        return true;
    }

    [HarmonyPatch("explode")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> explode_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        MethodInfo mtd_sendpackage = AccessTools.Method(typeof(ConnectionManager), nameof(ConnectionManager.SendPackage), new Type[] { typeof(NetPackage), typeof(bool), typeof(int), typeof(int), typeof(int), typeof(Vector3?), typeof(int) });

        for (int i = 0, totali = codes.Count; i < totali; i++)
        {
            if (codes[i].opcode == OpCodes.Callvirt && codes[i].Calls(mtd_sendpackage))
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
                    CodeInstruction.LoadField(typeof(GameManager), nameof(GameManager.tempExplPositions)),
                    new CodeInstruction(OpCodes.Ldloc_1),
                    CodeInstruction.Call(typeof(ExplosionEffectPatch), nameof(SendCustomExplosionPackage))
                });
                codes.RemoveRange(i - 26, 27);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(nameof(GameManager.explode))]
    [HarmonyPostfix]
    private static void explode_Postfix(GameManager __instance, ExplodeState __state)
    {
        if (__state.playerLayer >= 0)
        {
            __instance.World.GetPrimaryPlayer().SetModelLayer(__state.playerLayer, false);
        }
        if (!__state.useCustom)
            return;
        CustomExplosionManager.PopLastInitComponent();
    }

    [HarmonyPatch(nameof(GameManager.ExplosionClient))]
    [HarmonyPostfix]
    private static void ExplosionClient_Postfix(GameManager __instance, ref GameObject __result, Vector3 _center, Quaternion _rotation, int _index, int _blastPower, float _blastRadius)
    {
        if (__result != null || __instance.World == null)
            return;

        ExplosionValue components = CustomExplosionManager.LastInitializedComponent;
        //sorcery uses index over 20 to trigger explosion without particle and spawn visual particle on its own,
        //such usage could potentially break the chained explosion stack, thus this additional check is added.
        //invalid explosion component is not pushed to the stack, if index param does not match index on the stack,
        //then the param must be invalid, skip particle creation.
        if (components != null && components.CurrentExplosionParams._explosionData.ParticleIndex == _index)
        {
            ApplyExplosionForce.Explode(_center, (float)_blastPower, _blastRadius);
            __result = CustomExplosionManager.InitializeParticle(components.Component, _center - Origin.position, _rotation);
        }
#if DEBUG
        else
            Log.Warning("Failed to retrieve particle on client! Index:" + _index.ToString());
#endif
    }

    [HarmonyPatch(nameof(GameManager.SaveAndCleanupWorld))]
    [HarmonyPostfix]
    private static void SaveAndCleanupWorld_Postfix()
    {
        CustomExplosionManager.OnCleanUp();
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

[HarmonyPatch(typeof(NetEntityDistribution), nameof(NetEntityDistribution.Add), new Type[] { typeof(Entity) })]
internal class ExplosionSyncPatch
{
    private static void Postfix(Entity _e)
    {
        if (_e is EntityPlayer)
            CustomExplosionManager.OnClientConnected(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(_e.entityId));
    }
}

[HarmonyPatch]
internal class ExplosionParsePatch
{
    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.Init))]
    [HarmonyPrefix]
    private static bool Init_ItemClass_Prefix(ItemClass __instance)
    {
        if (CustomExplosionManager.parseParticleData(__instance.Properties, out var component))
        {
            component.BoundItemClass = __instance;
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemAction), nameof(ItemAction.ReadFrom))]
    [HarmonyPrefix]
    private static bool ReadFrom_ItemAction_Prefix(ItemAction __instance, ref DynamicProperties _props)
    {
        if (CustomExplosionManager.parseParticleData(_props, out var component))
        {
            component.BoundItemClass = __instance.item;
        }
        return true;
    }

    [HarmonyPatch(typeof(Block), nameof(Block.Init))]
    [HarmonyPrefix]
    private static bool Init_Block_Prefix(Block __instance)
    {
        CustomExplosionManager.parseParticleData(__instance.Properties, out _);
        return true;
    }

    [HarmonyPatch(typeof(EntityClass), nameof(EntityClass.Init))]
    [HarmonyPrefix]
    private static bool Init_EntityClass_Prefix(EntityClass __instance)
    {
        CustomExplosionManager.parseParticleData(__instance.Properties, out _);
        return true;
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnEntityDeath))]
internal class EntityExplosionPatch
{
    private static void Postfix(EntityAlive __instance)
    {
        if (__instance.isEntityRemote || __instance is EntityCar || __instance is EntityZombieCop)
            return;

        ref ExplosionData explosion = ref EntityClass.list[__instance.entityClass].explosionData;
        if (explosion.ParticleIndex > 0)
            GameManager.Instance.ExplosionServer(0, __instance.GetPosition(), World.worldToBlockPos(__instance.GetPosition()), Quaternion.identity, explosion, __instance.entityId, 0f, false, null);
    }
}

[HarmonyPatch(typeof(Explosion), nameof(Explosion.AttackEntites))]
internal class ExplosionAttackPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new List<CodeInstruction>(instructions);

        FieldInfo fld_hit_entities = AccessTools.Field(typeof(Explosion), nameof(Explosion.hitEntities));
        Type entityDict = fld_hit_entities.FieldType;

        LocalBuilder lbd_entity_dict = generator.DeclareLocal(entityDict);

        codes.InsertRange(0, new CodeInstruction[]
        {
            new CodeInstruction(OpCodes.Newobj, entityDict.GetConstructor(new Type[]{ })),
            new CodeInstruction(OpCodes.Stloc_S, lbd_entity_dict)
        });

        for (int i = 0; i < codes.Count; ++i)
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

[HarmonyPatch(typeof(ExplosionData))]
internal class ExplosionDataPatch
{
    [HarmonyPatch(nameof(ExplosionData.Write))]
    [HarmonyPrefix]
    private static bool Prefix_ExplosionData_Write(BinaryWriter _bw, ref ExplosionData __instance)
    {
        _bw.Write((ushort)__instance.ParticleIndex);
        _bw.Write((short)(__instance.Duration * 10f));
        _bw.Write((short)(__instance.BlockRadius * 20f));
        _bw.Write((short)__instance.EntityRadius);
        _bw.Write((short)__instance.BlastPower);
        _bw.Write(__instance.BlockDamage);
        _bw.Write(__instance.EntityDamage);
        _bw.Write(__instance.BlockTags);
        _bw.Write(__instance.IgnoreHeatMap);
        if (__instance.damageMultiplier != null)
        {
            _bw.Write(true);
            __instance.damageMultiplier.Write(_bw);
        }
        else
        {
            _bw.Write(false);
        }
        if (__instance.BuffActions != null)
        {
            _bw.Write((byte)__instance.BuffActions.Count);
            for (int i = 0; i < __instance.BuffActions.Count; i++)
            {
                _bw.Write(__instance.BuffActions[i]);
            }
        }
        else
        {
            _bw.Write(0);
        }
        return false;
    }

    [HarmonyPatch(nameof(ExplosionData.Read))]
    [HarmonyPrefix]
    private static bool Prefix_ExplosionData_Read(BinaryReader _br, ref ExplosionData __instance)
    {
        __instance.ParticleIndex = _br.ReadUInt16();
        __instance.Duration = _br.ReadInt16() * 0.1f;
        __instance.BlockRadius = _br.ReadInt16() * 0.05f;
        __instance.EntityRadius = _br.ReadInt16();
        __instance.BlastPower = _br.ReadInt16();
        __instance.BlockDamage = _br.ReadSingle();
        __instance.EntityDamage = _br.ReadSingle();
        __instance.BlockTags = _br.ReadString();
        __instance.IgnoreHeatMap = _br.ReadBoolean();
        if (_br.ReadBoolean())
        {
            __instance.damageMultiplier = new DamageMultiplier();
            __instance.damageMultiplier.Read(_br);
        }

        int num = _br.ReadByte();
        if (num > 0)
        {
            __instance.BuffActions = new List<string>();
            for (int i = 0; i < num; i++)
            {
                __instance.BuffActions.Add(_br.ReadString());
            }
        }
        else
        {
            __instance.BuffActions = null;
        }
        return false;
    }
}

[HarmonyPatch(typeof(ItemHasTags), nameof(ItemHasTags.IsValid))]
internal class ItemHasTagsPatch : HarmonyPatch
{
    private static bool Prefix(MinEventParams _params, ref bool __result)
    {
        if (_params.ItemValue == null)
        {
            __result = false;
            return false;
        }
        return true;
    }
}

//MinEventParams workarounds
[HarmonyPatch(typeof(ItemActionRanged), "fireShot")]
internal class ItemActionRangedFireShotPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        var fld_ranged_tag = AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.RangedTag));
        var fld_params = AccessTools.Field(typeof(EntityAlive), nameof(EntityAlive.MinEventContext));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(fld_ranged_tag))
            {
                if (!codes[i + 3].LoadsField(fld_params))
                {
                    codes.InsertRange(i + 2, new CodeInstruction[]
                    {
                            new CodeInstruction(OpCodes.Ldloc_1),
                            CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.MinEventContext)),
                            new CodeInstruction(OpCodes.Dup),
                            new CodeInstruction(OpCodes.Ldloc, 10),
                            CodeInstruction.LoadField(typeof(WorldRayHitInfo), nameof(WorldRayHitInfo.hit)),
                            CodeInstruction.LoadField(typeof(HitInfoDetails), nameof(HitInfoDetails.pos)),
                            CodeInstruction.StoreField(typeof(MinEventParams), nameof(MinEventParams.Position)),
                            new CodeInstruction(OpCodes.Ldloc_1),
                            CodeInstruction.Call(typeof(EntityAlive), nameof(EntityAlive.GetPosition)),
                            CodeInstruction.StoreField(typeof(MinEventParams), nameof(MinEventParams.StartPosition))
                    });
                }
                break;
            }
        }

        return codes;
    }
}

//[HarmonyPatch(typeof(MinEventActionExplode), nameof(MinEventActionExplode.Execute))]
//class TempPatch
//{
//    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
//    {
//        var codes = instructions.ToList();
//        var mtd_explode_server = AccessTools.Method(typeof(GameManager), nameof(GameManager.ExplosionServer));
//        for (int i = 0; i < codes.Count; i++)
//        {
//            if (codes[i].Calls(mtd_explode_server))
//            {
//                codes.InsertRange(i + 1, new CodeInstruction[]
//                {
//                    new CodeInstruction(OpCodes.Ldloca_S, 2),
//                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ExplosionData), nameof(ExplosionData.ToByteArray))),
//                    new CodeInstruction(OpCodes.Pop)
//                });
//                break;
//            }
//        }
//        return codes;
//    }
//}