using FullautoLauncher.Scripts.ProjectileManager;
using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;
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
    private static void Prefix_ItemActionEffects_ItemActionBetterLauncher(ActionModuleMultiBarrel self, ItemActionData _actionData, int _userData, int _firingState, MultiBarrelData __customData)
    {
        if (_actionData is ItemActionBetterLauncher.ItemActionDataBetterLauncher launcherData)
        {
            launcherData.projectileJoint = __customData.projectileJoints[(byte)RequireUserDataBits.ExtractUserDataBits(ref _userData, self.mask, self.shift)] ?? launcherData.projectileJoint;
            if (launcherData.projectileJoint == null && ConsoleCmdReloadLog.LogInfo)
            {
                Log.Warning($"null projectile joint on inventory slot {launcherData.invData.slotIdx}!!\n{StackTraceUtility.ExtractStackTrace()}");
            }
        }
        self.Prefix_ItemActionRanged_ItemActionEffects(_actionData, _userData, _firingState, __customData);
    }
}

[TypeTargetExtension(typeof(ActionModuleMultiActionFix))]
public static class FLARMultiActionFixExt
{
    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemAction.OnModificationsChanged))]
    [MethodTargetPostfix]
    private static void Postfix_OnModificationChanged_ItemActionBetterLauncher(ActionModuleMultiActionFix self, ItemActionData _data, ItemActionAttack __instance)
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

    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemAction.ItemActionEffects)), MethodTargetPrefix]
    private static void Prefix_ItemActionEffects_ItemActionBetterLauncher(ActionModuleMultiActionFix self, ItemActionData _actionData, out ItemActionData __state)
    {
        ActionModuleMultiActionFix.SetAndSaveItemActionData(_actionData, out __state);
    }

    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemAction.ItemActionEffects)), MethodTargetPostfix]
    public static void Postfix_ItemActionEffects_ItemActionBetterLauncher(ActionModuleMultiActionFix self, ItemActionData _actionData, ItemActionData __state)
    {
        ActionModuleMultiActionFix.RestoreItemActionData(_actionData, __state);
    }
}

[TypeTargetExtension(typeof(ActionModuleProceduralRecoil))]
public static class FLARProceduralRecoilExt
{
    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemActionBetterLauncher.GetActionEffectsValues)), MethodTargetTranspiler]
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
    //multi action patch
    [HarmonyPatch(typeof(ItemActionBetterLauncher), nameof(ItemActionBetterLauncher.StartHolding))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_StartHolding_ItemActionBetterLauncher(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var mtd_ammoindex = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));
        var mtd_meta = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.Meta));

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
            else if (codes[i].Calls(mtd_ammoindex))
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
            else if (codes[i].Calls(mtd_meta))
            {
                codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_S, lbd_meta));
                codes.RemoveRange(i - 1, 2);
                i--;
            }
        }

        return codes;
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

    [HarmonyPatch(typeof(ProjectileParams), nameof(ProjectileParams.Fire))]
    [HarmonyPrefix]
    private static bool Prefix_Fire_ProjectileParams(ProjectileParams.ItemInfo _info, Entity _firingEntity)
    {
        if (_firingEntity is EntityAlive entityAlive)
            entityAlive.MinEventContext.ItemActionData = _info.actionData;
        return true;
    }

    private static void ApplyLauncherProperties(ProjectileParams.ItemInfo info)
    {
        MultiActionUtils.CopyLauncherValueToProjectile(
            info.itemValueLauncher, info.itemValueProjectile, info.actionData.indexInEntityOfAction);
    }

    [HarmonyPatch(typeof(ProjectileParams), nameof(ProjectileParams.Fire))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Fire_ProjectileParams(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        FieldInfo fld_launcher = AccessTools.Field(typeof(ProjectileParams.ItemInfo), nameof(ProjectileParams.ItemInfo.itemValueLauncher));
        FieldInfo fld_projectile = AccessTools.Field(typeof(ProjectileParams.ItemInfo), nameof(ProjectileParams.ItemInfo.itemValueProjectile));
        FieldInfo fld_info = AccessTools.Field(typeof(ProjectileParams), nameof(ProjectileParams.info));
        MethodInfo mtd_getvalue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));
        MethodInfo mtd_getvaluenew = AccessTools.Method(typeof(MultiActionReversePatches), nameof(MultiActionReversePatches.ProjectileGetValue));
        bool infoAssigned = false;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].StoresField(fld_info))
            {
                infoAssigned = true;
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, fld_info),
                    CodeInstruction.Call(typeof(FLARPatch), nameof(ApplyLauncherProperties))
                });
                i += 3;
            }
            else if (infoAssigned && codes[i].LoadsField(fld_launcher))
            {
                codes[i].operand = fld_projectile;
            }
            else if (infoAssigned && codes[i].Calls(mtd_getvalue))
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
        float magnitude = dir.magnitude;
        if (magnitude < 0.04f)
        {
            __result = false;
            return false;
        }

        Ray ray = new Ray(__instance.previousPosition, dir);
        __instance.waterCollisionParticles.CheckCollision(ray.origin, ray.direction, magnitude, (entityAlive != null) ? entityAlive.entityId : (-1));
        int hitmask = ((__instance.hmOverride == 0) ? 80 : __instance.hmOverride);
        float collisionOffset = __instance.radius + __instance.collisionStartBack;
        ray.origin -= ray.direction * collisionOffset;
        magnitude += collisionOffset;
        bool bHit = Voxel.Raycast(world, ray, magnitude, -538751005, hitmask, __instance.radius);
        __instance.collisionStartBack = 0.1f;
        if (bHit && (GameUtils.IsBlockOrTerrain(Voxel.voxelRayHitInfo.tag) || Voxel.voxelRayHitInfo.tag.StartsWith("E_")))
        {
            if (entityAlive != null && !entityAlive.isEntityRemote)
            {
                ProjectileParams.ItemInfo info = __instance.info;
                EntityAlive hitEntity = ItemActionAttack.FindHitEntity(Voxel.voxelRayHitInfo) as EntityAlive;
                EntityDrone hitDrone = hitEntity as EntityDrone;
                if (hitDrone && hitDrone.isAlly(entityAlive))
                {
                    __result = false;
                    return false;
                }

                entityAlive.MinEventContext.Other = hitEntity;
                entityAlive.MinEventContext.ItemActionData = info.actionData;
                entityAlive.MinEventContext.ItemValue = info.itemValueLauncher;
                entityAlive.MinEventContext.Position = Voxel.voxelRayHitInfo.hit.pos;
                ItemActionAttack.AttackHitInfo attackHitInfo = new ItemActionAttack.AttackHitInfo
                {
                    WeaponTypeTag = ItemActionAttack.RangedTag
                };

                MinEventParams.CachedEventParam.Self = entityAlive;
                MinEventParams.CachedEventParam.Position = Voxel.voxelRayHitInfo.hit.pos;
                MinEventParams.CachedEventParam.ItemValue = info.itemValueProjectile;
                MinEventParams.CachedEventParam.ItemActionData = info.actionData;
                MinEventParams.CachedEventParam.Other = entityAlive.MinEventContext.Other;
                info.itemProjectile.FireEvent(MinEventTypes.onProjectilePreImpact, MinEventParams.CachedEventParam);

                float strainPercent = Mathf.Clamp01(__instance.ReadLaunchStrain());
                float blockDamage = Mathf.Lerp(1f,
                    info.itemActionProjectile.GetProjectileDamageBlock(info.itemValueProjectile,
                        ItemActionAttack.GetBlockHit(world, Voxel.voxelRayHitInfo), entityAlive,
                        info.actionData.indexInEntityOfAction),
                    strainPercent);
                float entityDamage = Mathf.Lerp(1f,
                    info.itemActionProjectile.GetProjectileDamageEntity(info.itemValueProjectile, entityAlive,
                        info.actionData.indexInEntityOfAction),
                    strainPercent);
                bool forceNoDamage;
                if (entityAlive is EntityPlayer)
                {
                    entityDamage *= ItemActionAttack.RangedDamagePercent;
                    forceNoDamage = ItemActionAttack.RangedDamagePercent == 0f;
                }
                else
                {
                    entityDamage *= ItemActionAttack.EntityPlayerDamagePercent;
                    forceNoDamage = ItemActionAttack.EntityPlayerDamagePercent == 0f;
                }

                MultiActionProjectileRewrites.ProjectileHit(Voxel.voxelRayHitInfo,
                                       entityAlive.entityId,
                                       EnumDamageTypes.Piercing,
                                       blockDamage,
                                       entityDamage,
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
                                       info.itemValueLauncher,
                                       false,
                                       forceNoDamage);
                if (entityAlive.MinEventContext.Other == null)
                {
                    entityAlive.FireEvent(MinEventTypes.onSelfPrimaryActionMissEntity, false);
                }
                entityAlive.FireEvent(MinEventTypes.onProjectileImpact, false);
                MinEventParams.CachedEventParam.Self = entityAlive;
                MinEventParams.CachedEventParam.Position = Voxel.voxelRayHitInfo.hit.pos;
                MinEventParams.CachedEventParam.ItemValue = info.itemValueProjectile;
                MinEventParams.CachedEventParam.ItemActionData = info.actionData;
                MinEventParams.CachedEventParam.Other = hitEntity;
                info.itemProjectile.FireEvent(MinEventTypes.onProjectileImpact, MinEventParams.CachedEventParam);
                if (info.itemActionProjectile.Explosion.ParticleIndex > 0)
                {
                    Vector3 vector3 = Voxel.voxelRayHitInfo.hit.pos - dir.normalized * 0.1f;
                    Vector3i vector3i = World.worldToBlockPos(vector3);
                    if (!world.GetBlock(vector3i).isair)
                    {
                        BlockFace blockFace;
                        vector3i = Voxel.OneVoxelStep(vector3i, vector3, -dir.normalized, out vector3, out blockFace);
                    }
                    GameManager.Instance.ExplosionServer(vector3, vector3i, Quaternion.identity, info.itemActionProjectile.Explosion, entityAlive.entityId, 0f, false, info.itemValueProjectile);
                }
                else
                {
                    if (hitEntity is EntitySwarm)
                    {
                        __result = false;
                        return false;
                    }

                    if (!info.itemProjectile.IsSticky)
                    {
                        __result = true;
                        return false;
                    }

                    GameRandom gameRandom = world.GetGameRandom();
                    float stickRoll = __instance.stickyRoll ?? gameRandom.RandomFloat;
                    __instance.stickyRoll = stickRoll;
                    if (GameUtils.IsBlockOrTerrain(Voxel.voxelRayHitInfo.tag))
                    {
                        if (stickRoll < MultiActionReversePatches.ProjectileGetValue(PassiveEffects.ProjectileStickChance, info.itemValueProjectile, 0.5f, entityAlive, null, info.itemProjectile.ItemTags | FastTags<TagGroup.Global>.Parse(Voxel.voxelRayHitInfo.fmcHit.blockValue.Block.blockMaterial.SurfaceCategory), true, false))
                        {
                            ProjectileManager.AddProjectileItem(ProjectileParams.CreateStickyProjectile(info, entityAlive, __instance), -1, Voxel.voxelRayHitInfo.hit.pos, dir.normalized, info.itemValueProjectile.type);
                        }
                        else
                        {
                            GameManager.Instance.SpawnParticleEffectServer(new ParticleEffect("impact_metal_on_wood", Voxel.voxelRayHitInfo.hit.pos, Utils.BlockFaceToRotation(Voxel.voxelRayHitInfo.fmcHit.blockFace), 1f, Color.white, string.Format("{0}hit{1}", Voxel.voxelRayHitInfo.fmcHit.blockValue.Block.blockMaterial.SurfaceCategory, info.itemProjectile.MadeOfMaterial.SurfaceCategory), null), entityAlive.entityId, false, false);
                        }
                    }
                    else if (stickRoll < MultiActionReversePatches.ProjectileGetValue(PassiveEffects.ProjectileStickChance, info.itemValueProjectile, 0.5f, entityAlive, null, info.itemProjectile.ItemTags, true, false))
                    {
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

    [HarmonyPatch(typeof(ProjectileParams), nameof(ProjectileParams.CreateStickyProjectile))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_CreateStickyProjectile_ProjectileParams(IEnumerable<CodeInstruction> instructions)
    { 
        var codes = instructions.ToList();
        FieldInfo initialImpactConsumed = AccessTools.Field(typeof(CustomProjectileMoveScript), nameof(CustomProjectileMoveScript.initialImpactConsumed));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].operand is MethodInfo method &&
                method.IsGenericMethod &&
                method.Name == "GetOrAddComponent" &&
                method.GetGenericArguments()[0] == typeof(ProjectileMoveScript))
            {
                codes[i].operand = method.GetGenericMethodDefinition().MakeGenericMethod(typeof(CustomProjectileMoveScript));
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Stfld, initialImpactConsumed)
                });
                return codes;
            }
        }

        throw new System.InvalidOperationException("Failed to replace ProjectileMoveScript in ProjectileParams.CreateStickyProjectile.");
    }
}
