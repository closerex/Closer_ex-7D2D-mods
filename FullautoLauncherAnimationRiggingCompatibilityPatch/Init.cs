﻿using FullautoLauncher.Scripts.ProjectileManager;
using HarmonyLib;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static ActionModuleMultiBarrel;

public class FLARCompatibilityPatchInit : IModApi
{
    private static bool inited = false;
    public void InitMod(Mod _modInstance)
    {
        if (inited)
            return;
        inited = true;
        Log.Out(" Loading Patch: " + GetType());
        var harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

[TypeTargetExtension(typeof(ActionModuleMultiBarrel))]
public static class FLARMultiBarrelExt
{
    //multi barrel patch
    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemAction.ItemActionEffects))]
    [MethodTargetPrefix]
    private static bool Prefix_ItemActionEffects_ItemActionBetterLauncher(ActionModuleMultiBarrel self, ItemActionData _actionData, int _userData, int _firingState, MultiBarrelData __customData)
    {
        if (_actionData is ItemActionBetterLauncher.ItemActionDataBetterLauncher launcherData)
        {
            launcherData.projectileJoint = __customData.projectileJoints[(byte)(_userData >> 8)];
            if (launcherData.projectileJoint == null && ConsoleCmdReloadLog.LogInfo)
            {
                Log.Warning($"null projectile joint on inventory slot {launcherData.invData.slotIdx}!!\n{StackTraceUtility.ExtractStackTrace()}");
            }
        }
        return self.Prefix_ItemActionEffects_ItemActionRanged(_actionData, _userData, _firingState, __customData);
    }
}

[TypeTargetExtension(typeof(ActionModuleMultiActionFix))]
public static class FLARMultiActionFixExt
{
    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemAction.OnModificationsChanged))]
    [MethodTargetPostfix]
    private static void Postfix_OnModificationChanged_ActionModuleMultiActionFix(ActionModuleMultiActionFix self, ItemActionData _data, ItemActionAttack __instance)
    {
        self.Postfix_OnModificationChanged_ItemActionRanged(_data, __instance);
        if (_data is ItemActionBetterLauncher.ItemActionDataBetterLauncher launcherData)
        {
            string indexExtension = (_data.indexInEntityOfAction > 0 ? _data.indexInEntityOfAction.ToString() : "");
            string jointName = _data.invData.itemValue.GetPropertyOverrideForAction($"ProjectileJoint_Name", $"ProjectileJoint{indexExtension}", _data.indexInEntityOfAction);
            launcherData.projectileJoint = AnimationRiggingManager.GetTransformOverrideByName(launcherData.invData.model, jointName) ?? launcherData.projectileJoint;
            if (launcherData.projectileJoint == null && ConsoleCmdReloadLog.LogInfo)
            {
                Log.Warning($"null projectile joint on inventory slot {launcherData.invData.slotIdx}!\n{StackTraceUtility.ExtractStackTrace()}");
            }
        }
    }
}

[TypeTargetExtension(typeof(ActionModuleProceduralRecoil))]
public static class FLARProceduralRecoilExt
{
    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemActionBetterLauncher.GetActionEffectsValues))]
    [MethodTargetTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler_ItemActionBetterLauncher_getImageActionEffectsStartPosAndDirection(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_ray = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.GetLookRay));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_ray))
            {
                codes.RemoveRange(i - 3, 4);
                codes.InsertRange(i - 3, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.Call(typeof(ActionModuleProceduralRecoil), nameof(ActionModuleProceduralRecoil.GetLookRayOverride))
                });
                break;
            }
        }
        return codes;
    }
}

[HarmonyPatch]
public static class FLARPatch
{
    //Animation Rigging patch, find joint transform
    //[HarmonyPatch(typeof(ItemActionBetterLauncher.ItemActionDataBetterLauncher), MethodType.Constructor, new Type[] { typeof(ItemInventoryData), typeof(int) })]
    //[HarmonyPostfix]
    //private static void Postfix_ctor_ItemActionDataBetterLauncher(ItemActionBetterLauncher.ItemActionDataBetterLauncher __instance, ItemInventoryData _invData)
    //{
    //    __instance.projectileJoint = AnimationRiggingManager.GetTransformOverrideByName("ProjectileJoint", _invData.model);
    //}

    ////projectile damage patch
    //[HarmonyPatch(typeof(ProjectileParams), nameof(ProjectileParams.CheckCollision))]
    //[HarmonyTranspiler]
    //private static IEnumerable<CodeInstruction> Transpiler_checkCollision_ProjectileMoveScript(IEnumerable<CodeInstruction> instructions)
    //{
    //    var codes = instructions.ToList();
    //    var mtd_block = AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetDamageBlock));
    //    var mtd_entity = AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetDamageEntity));

    //    for (int i = 0; i < codes.Count; i++)
    //    {
    //        if (codes[i].Calls(mtd_block))
    //        {
    //            codes.InsertRange(i + 1, new CodeInstruction[]
    //            {
    //                new CodeInstruction(OpCodes.Ldarg_0),
    //                CodeInstruction.LoadField(typeof(ProjectileParams), nameof(ProjectileParams.info)),
    //                CodeInstruction.LoadField(typeof(ProjectileParams.ItemInfo), nameof(ProjectileParams.ItemInfo.itemValueProjectile)),
    //                new CodeInstruction(OpCodes.Ldarg_1),
    //                CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(CommonUtilityPatch.GetProjectileBlockDamagePerc)),
    //                new CodeInstruction(OpCodes.Mul)
    //            });
    //        }
    //        else if (codes[i].Calls(mtd_entity))
    //        {
    //            codes.InsertRange(i + 1, new CodeInstruction[]
    //            {
    //                new CodeInstruction(OpCodes.Ldarg_0),
    //                CodeInstruction.LoadField(typeof(ProjectileParams), nameof(ProjectileParams.info)),
    //                CodeInstruction.LoadField(typeof(ProjectileParams.ItemInfo), nameof(ProjectileParams.ItemInfo.itemValueProjectile)),
    //                new CodeInstruction(OpCodes.Ldarg_1),
    //                CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(CommonUtilityPatch.GetProjectileEntityDamagePerc)),
    //                new CodeInstruction(OpCodes.Mul)
    //            });
    //        }
    //    }

    //    return codes;
    //}

    //multi action patch
    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemActionBetterLauncher.StartHolding))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_StartHolding_ItemActionBetterLauncher(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var fld_ammoindex = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));
        var fld_meta = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Meta));

        var lbd_meta = generator.DeclareLocal(typeof(int));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_1)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.indexInEntityOfAction)),
                    CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetMetaByActionIndex)),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_meta)
                });
                i += 5;
            }
            else if (codes[i].LoadsField(fld_ammoindex))
            {
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetSelectedAmmoIndexByActionIndex));
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.indexInEntityOfAction))
                });
                i += 2;
            }
            else if (codes[i].LoadsField(fld_meta))
            {
                codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_S, lbd_meta));
                codes.RemoveRange(i - 1, 2);
                i--;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemActionBetterLauncher.StartHolding))]
    [HarmonyPostfix]
    private static void Postfix_StartHolding_ItemActionBetterLauncher(ItemActionData _actionData)
    {
        ItemActionBetterLauncher.ItemActionDataBetterLauncher ItemActionDataBetterLauncher = (ItemActionBetterLauncher.ItemActionDataBetterLauncher)_actionData;
        var info = ItemActionDataBetterLauncher.info;
        var projectileValue = info.itemValueProjectile;
        var launcherValue = info.itemValueLauncher;
        MultiActionUtils.CopyLauncherValueToProjectile(launcherValue, projectileValue, _actionData.indexInEntityOfAction);
    }

    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemActionBetterLauncher.OnModificationsChanged))]
    [HarmonyPostfix]
    private static void Postfix_OnModificationsChanged_ItemActionBetterLauncher(ItemActionData _data)
    {
        ItemActionBetterLauncher.ItemActionDataBetterLauncher ItemActionDataBetterLauncher = (ItemActionBetterLauncher.ItemActionDataBetterLauncher)_data;
        ItemActionDataBetterLauncher.projectileJoint = AnimationRiggingManager.GetTransformOverrideByName(ItemActionDataBetterLauncher.invData.model, "ProjectileJoint");
        if (!ItemActionDataBetterLauncher.projectileJoint && ConsoleCmdReloadLog.LogInfo)
        {
            Log.Warning($"null projectile joint on inventory slot {ItemActionDataBetterLauncher.invData.slotIdx}!\n{StackTraceUtility.ExtractStackTrace()}");
        }
    }

    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemActionBetterLauncher.SwapAmmoType))]
    [HarmonyPostfix]
    private static void Postfix_SwapAmmoType_ItemActionBetterLauncher(EntityAlive _entity, ItemActionBetterLauncher __instance)
    {
        var ItemActionDataBetterLauncher = (ItemActionBetterLauncher.ItemActionDataBetterLauncher)_entity.inventory.holdingItemData.actionData[__instance.ActionIndex];
        var info = ItemActionDataBetterLauncher.info;
        var projectileValue = info.itemValueProjectile;
        var launcherValue = info.itemValueLauncher;
        MultiActionUtils.CopyLauncherValueToProjectile(launcherValue, projectileValue, ItemActionDataBetterLauncher.indexInEntityOfAction);
    }

    [HarmonyPatch(typeof(ProjectileParams), nameof(ProjectileParams.Fire))]
    [HarmonyPrefix]
    private static bool Prefix_Fire_ProjectileParams(ProjectileParams.ItemInfo _info, Entity _firingEntity)
    {
        if (_firingEntity is EntityAlive entityAlive)
            entityAlive.MinEventContext.ItemActionData = _info.actionData;
        if (_info.itemProjectile.IsSticky)
        {
            var projectileValue = _info.itemValueProjectile;
            var launcherValue = _info.itemValueLauncher;
            MultiActionUtils.CopyLauncherValueToProjectile(launcherValue, projectileValue, _info.actionData.indexInEntityOfAction);
        }
        return true;
    }

    [HarmonyPatch(typeof(ProjectileParams), nameof(ProjectileParams.Fire))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Fire_ProjectileParams(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        FieldInfo fld_launcher = AccessTools.Field(typeof(ProjectileParams.ItemInfo), nameof(ProjectileParams.ItemInfo.itemValueLauncher));
        FieldInfo fld_projectile = AccessTools.Field(typeof(ProjectileParams.ItemInfo), nameof(ProjectileParams.ItemInfo.itemValueProjectile));
        MethodInfo mtd_getvalue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));
        MethodInfo mtd_getvaluenew = AccessTools.Method(typeof(MultiActionReversePatches), nameof(MultiActionReversePatches.ProjectileGetValue));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(fld_launcher))
            {
                codes[i].operand = fld_projectile;
            }
            else if (codes[i].Calls(mtd_getvalue))
            {
                codes[i].operand = mtd_getvaluenew;
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(ProjectileParams), nameof(ProjectileParams.CheckCollision))]
    [HarmonyPrefix]
    private static bool Prefix_CheckCollision_ProjectileParams(ProjectileParams __instance, EntityAlive entityAlive, ref bool __result)
    {
        World world = GameManager.Instance.World;
        Vector3 dir = __instance.currentPosition - __instance.previousPosition;
        Vector3 dirNorm = dir.normalized;
        float magnitude = dir.magnitude;
        if (magnitude < 0.04f)
        {
            __result = false;
            return false;
        }
        Ray ray = new Ray(__instance.previousPosition, dir);
        __instance.waterCollisionParticles.CheckCollision(ray.origin, ray.direction, magnitude, (entityAlive != null) ? entityAlive.entityId : (-1));
        int hitmask = ((__instance.hmOverride == 0) ? 80 : __instance.hmOverride);
        bool bHit = Voxel.Raycast(world, ray, magnitude, -538750997, hitmask, __instance.radius);
        if (bHit && (GameUtils.IsBlockOrTerrain(Voxel.voxelRayHitInfo.tag) || Voxel.voxelRayHitInfo.tag.StartsWith("E_")))
        {
            if (entityAlive != null && !entityAlive.isEntityRemote)
            {
                ProjectileParams.ItemInfo info = __instance.info;
                entityAlive.MinEventContext.Other = ItemActionAttack.FindHitEntity(Voxel.voxelRayHitInfo) as EntityAlive;
                entityAlive.MinEventContext.ItemActionData = info.actionData;
                entityAlive.MinEventContext.ItemValue = info.itemValueLauncher;
                entityAlive.MinEventContext.Position = Voxel.voxelRayHitInfo.hit.pos;
                ItemActionAttack.AttackHitInfo attackHitInfo = new ItemActionAttack.AttackHitInfo
                {
                    WeaponTypeTag = ItemActionAttack.RangedTag
                };
                MultiActionProjectileRewrites.ProjectileHit(Voxel.voxelRayHitInfo,
                                       entityAlive.entityId,
                                       EnumDamageTypes.Piercing,
                                       MultiActionProjectileRewrites.GetProjectileDamageBlock(info.itemActionProjectile, info.itemValueProjectile, ItemActionAttack.GetBlockHit(world, Voxel.voxelRayHitInfo), entityAlive, info.actionData.indexInEntityOfAction),
                                       MultiActionProjectileRewrites.GetProjectileDamageEntity(info.itemActionProjectile, info.itemValueProjectile, entityAlive, info.actionData.indexInEntityOfAction),
                                       1f,
                                       1f,
                                       MultiActionReversePatches.ProjectileGetValue(PassiveEffects.CriticalChance, info.itemValueProjectile, info.itemProjectile.CritChance.Value, entityAlive, null, info.itemProjectile.ItemTags, true, false),
                                       ItemAction.GetDismemberChance(info.actionData, Voxel.voxelRayHitInfo),
                                       info.itemProjectile.MadeOfMaterial.SurfaceCategory,
                                       info.itemActionProjectile.GetDamageMultiplier(),
                                       info.itemActionProjectile.BuffActions,
                                       attackHitInfo,
                                       1,
                                       info.itemActionProjectile.ActionExp,
                                       info.itemActionProjectile.ActionExpBonusMultiplier,
                                       null,
                                       null,
                                       ItemActionAttack.EnumAttackMode.RealNoHarvesting,
                                       null,
                                       -1,
                                       info.itemValueProjectile,
                                       info.itemValueLauncher);
                if (entityAlive.MinEventContext.Other == null)
                {
                    entityAlive.FireEvent(MinEventTypes.onSelfPrimaryActionMissEntity, false);
                }
                entityAlive.FireEvent(MinEventTypes.onProjectileImpact, false);
                MinEventParams.CachedEventParam.Self = entityAlive;
                MinEventParams.CachedEventParam.Position = Voxel.voxelRayHitInfo.hit.pos;
                MinEventParams.CachedEventParam.ItemValue = info.itemValueProjectile;
                MinEventParams.CachedEventParam.ItemActionData = info.actionData;
                MinEventParams.CachedEventParam.Other = entityAlive.MinEventContext.Other;
                info.itemProjectile.FireEvent(MinEventTypes.onProjectileImpact, MinEventParams.CachedEventParam);
                if (info.itemActionProjectile.Explosion.ParticleIndex > 0)
                {
                    Vector3 vector3 = Voxel.voxelRayHitInfo.hit.pos - dirNorm * 0.1f;
                    Vector3i vector3i = World.worldToBlockPos(vector3);
                    if (!world.GetBlock(vector3i).isair)
                    {
                        BlockFace blockFace;
                        vector3i = Voxel.OneVoxelStep(vector3i, vector3, -dirNorm, out vector3, out blockFace);
                    }
                    GameManager.Instance.ExplosionServer(Voxel.voxelRayHitInfo.hit.clrIdx, vector3, vector3i, Quaternion.identity, info.itemActionProjectile.Explosion, entityAlive.entityId, 0f, false, info.itemValueProjectile);
                }
                else if (info.itemProjectile.IsSticky)
                {
                    GameRandom gameRandom = world.GetGameRandom();
                    if (GameUtils.IsBlockOrTerrain(Voxel.voxelRayHitInfo.tag))
                    {
                        if (gameRandom.RandomFloat < MultiActionReversePatches.ProjectileGetValue(PassiveEffects.ProjectileStickChance, info.itemValueProjectile, 0.5f, entityAlive, null, info.itemProjectile.ItemTags | FastTags<TagGroup.Global>.Parse(Voxel.voxelRayHitInfo.fmcHit.blockValue.Block.blockMaterial.SurfaceCategory), true, false))
                        {
                            MultiActionProjectileRewrites.RestoreProjectileValue(info.itemValueProjectile);
                            ProjectileManager.AddProjectileItem(ProjectileParams.CreateStickyProjectile(info, entityAlive, __instance), -1, Voxel.voxelRayHitInfo.hit.pos, dir.normalized, info.itemValueProjectile.type);
                        }
                        else
                        {
                            GameManager.Instance.SpawnParticleEffectServer(new ParticleEffect("impact_metal_on_wood", Voxel.voxelRayHitInfo.hit.pos, Utils.BlockFaceToRotation(Voxel.voxelRayHitInfo.fmcHit.blockFace), 1f, Color.white, string.Format("{0}hit{1}", Voxel.voxelRayHitInfo.fmcHit.blockValue.Block.blockMaterial.SurfaceCategory, info.itemProjectile.MadeOfMaterial.SurfaceCategory), null), entityAlive.entityId, false, false);
                        }
                    }
                    else if (gameRandom.RandomFloat < MultiActionReversePatches.ProjectileGetValue(PassiveEffects.ProjectileStickChance, info.itemValueProjectile, 0.5f, entityAlive, null, info.itemProjectile.ItemTags, true, false))
                    {
                        MultiActionProjectileRewrites.RestoreProjectileValue(info.itemValueProjectile);
                        int ProjectileID = ProjectileManager.AddProjectileItem(ProjectileParams.CreateStickyProjectile(info, entityAlive, __instance), -1, Voxel.voxelRayHitInfo.hit.pos, dir.normalized, info.itemValueProjectile.type);
                        Utils.SetLayerRecursively(ProjectileManager.GetProjectile(ProjectileID).gameObject, 14, null);
                    }
                    else
                    {
                        GameManager.Instance.SpawnParticleEffectServer(new ParticleEffect("impact_metal_on_wood", Voxel.voxelRayHitInfo.hit.pos, Utils.BlockFaceToRotation(Voxel.voxelRayHitInfo.fmcHit.blockFace), 1f, Color.white, "bullethitwood", null), entityAlive.entityId, false, false);
                    }
                }
            }
            __result = true;
        }
        else
        {
            __result = false;
        }
        return false;
    }
}