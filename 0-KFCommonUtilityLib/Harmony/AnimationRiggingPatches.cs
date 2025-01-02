using HarmonyLib;
using KFCommonUtilityLib.Scripts.StaticManagers;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Xml.Linq;
using UniLinq;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[HarmonyPatch]
static class AnimationRiggingPatches
{
    ///// <summary>
    ///// compatibility patch for launcher projectile joint
    ///// </summary>
    ///// <param name="__instance"></param>
    ///// <param name="_invData"></param>
    //[HarmonyPatch(typeof(ItemActionLauncher.ItemActionDataLauncher), MethodType.Constructor, new Type[] { typeof(ItemInventoryData), typeof(int) })]
    //[HarmonyPostfix]
    //private static void Postfix_ctor_ItemActionDataLauncher(ItemActionLauncher.ItemActionDataLauncher __instance, ItemInventoryData _invData)
    //{
    //    __instance.projectileJoint = AnimationRiggingManager.GetTransformOverrideByName("ProjectileJoint", _invData.model);
    //}

    //[HarmonyPatch(typeof(ItemActionRanged.ItemActionDataRanged), MethodType.Constructor, new Type[] { typeof(ItemInventoryData), typeof(int) })]
    //[HarmonyPostfix]
    //private static void Postfix_ctor_ItemActionDataRanged(ItemActionRanged.ItemActionDataRanged __instance, ItemInventoryData _invData)
    //{
    //    if (__instance.IsDoubleBarrel)
    //    {
    //        __instance.muzzle = AnimationRiggingManager.GetTransformOverrideByName("Muzzle_L", _invData.model);
    //        __instance.muzzle2 = AnimationRiggingManager.GetTransformOverrideByName("Muzzle_R", _invData.model);
    //    }
    //    else
    //    {
    //        __instance.muzzle = AnimationRiggingManager.GetTransformOverrideByName("Muzzle", _invData.model);
    //    }
    //}

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.OnModificationsChanged))]
    [HarmonyPostfix]
    private static void Postfix_OnModificationChanged_ItemActionRanged(ItemActionData _data)
    {
        ItemActionRanged.ItemActionDataRanged rangedData = (ItemActionRanged.ItemActionDataRanged)_data;
        if (rangedData.IsDoubleBarrel)
        {
            rangedData.muzzle = AnimationRiggingManager.GetTransformOverrideByName(rangedData.invData.model, "Muzzle_L");
            rangedData.muzzle2 = AnimationRiggingManager.GetTransformOverrideByName(rangedData.invData.model, "Muzzle_R");
        }
        else
        {
            rangedData.muzzle = AnimationRiggingManager.GetTransformOverrideByName(rangedData.invData.model, "Muzzle");
        }
        rangedData.Laser = AnimationRiggingManager.GetTransformOverrideByName(rangedData.invData.model, "laser");

        ItemActionLauncher.ItemActionDataLauncher launcherData = _data as ItemActionLauncher.ItemActionDataLauncher;
        if (launcherData != null)
        {
            launcherData.projectileJoint = AnimationRiggingManager.GetTransformOverrideByName(launcherData.invData.model, "ProjectileJoint");
        }
    }


    /// <summary>
    /// attachment path patch, only apply to MinEventActionSetTransformActive!
    /// </summary>
    /// <param name="instructions"></param>
    /// <returns></returns>
    [HarmonyPatch(typeof(MinEventActionSetTransformActive), nameof(MinEventActionSetTransformActive.Execute))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Execute_MinEventActionSetTransformActive(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_find = AccessTools.Method(typeof(GameUtils), nameof(GameUtils.FindDeepChild));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_find))
            {
                codes.RemoveAt(i);
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.Self)),
                    CodeInstruction.Call(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.GetAttachmentReferenceOverrideTransform))
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(MinEventActionSetTransformChildrenActive), nameof(MinEventActionSetTransformChildrenActive.Execute))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Execute_MinEventActionSetTransformChildrenActive(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_find = AccessTools.Method(typeof(GameUtils), nameof(GameUtils.FindDeepChildActive));
        var fld_trans = AccessTools.Field(typeof(MinEventActionSetTransformChildrenActive), nameof(MinEventActionSetTransformChildrenActive.transformPath));
        for (int i = 1; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_find) && codes[i - 1].LoadsField(fld_trans))
            {
                codes.RemoveAt(i);
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.Self)),
                    CodeInstruction.Call(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.GetAttachmentReferenceOverrideTransformActive))
                });
                break;
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(MinEventActionAddPart), nameof(MinEventActionAddPart.Execute))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Execute_MinEventActionAddPart(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_idx = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItemIdx));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_idx))
            {
                codes.InsertRange(i + 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(MinEventActionAddPart),nameof(MinEventActionAddPart.partName)),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    CodeInstruction.Call(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.GetAddPartTransformOverride))
                });
                break;
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(MinEventActionAttachPrefabToHeldItem), nameof(MinEventActionAttachPrefabToHeldItem.Execute))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Execute_MinEventActionAttachPrefabToHeldItem(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_find = AccessTools.Method(typeof(GameUtils), nameof(GameUtils.FindDeepChild));
        var fld_trans = AccessTools.Field(typeof(MinEventParams), nameof(MinEventParams.Transform));
        for (int i = 1; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_0)
            {
                if (codes[i - 1].Calls(mtd_find))
                {
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        CodeInstruction.Call(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.GetAddPartTransformOverride))
                    });
                    codes.RemoveAt(i - 1);
                    i += 1;
                }
                else if (codes[i - 1].LoadsField(fld_trans))
                {
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(MinEventActionAttachPrefabToHeldItem), nameof(MinEventActionAttachPrefabToHeldItem.parent_transform)),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        CodeInstruction.Call(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.GetAddPartTransformOverride))
                    });
                    i += 4;
                }
            }
        }
        return codes;
    }

    /// <summary>
    /// reload logging patch
    /// </summary>
    /// <param name="stateInfo"></param>
    [HarmonyPatch(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.OnStateEnter))]
    [HarmonyPostfix]
    private static void Postfix_OnStateEnter_AnimatorRangedReloadState(AnimatorStateInfo stateInfo)
    {
        if (ConsoleCmdReloadLog.LogInfo)
        {
            Log.Out(string.Format("ANIMATION LENGTH: length {0} speed {1} speedMultiplier {2} original length {3}", stateInfo.length, stateInfo.speed, stateInfo.speedMultiplier, stateInfo.length * stateInfo.speedMultiplier));
        }
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.OnHoldingItemChanged))]
    [HarmonyPostfix]
    private static void Postfix_OnHoldingItemChanged_EntityPlayerLocal(EntityPlayerLocal __instance)
    {
        AnimationRiggingManager.OnHoldingItemIndexChanged(__instance);
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveAndCleanupWorld))]
    [HarmonyPostfix]
    private static void Postfix_SaveAndCleanupWorld_GameManager()
    {
        AnimationRiggingManager.Clear();
    }

    private static void ParseTakeOverReloadTime(XElement _node)
    {
        string itemName = _node.GetAttribute("name");
        if (string.IsNullOrEmpty(itemName))
        {
            return;
        }
        ItemClass item = ItemClass.GetItemClass(itemName);
        if (item.Properties.GetBool("TakeOverReloadTime"))
        {
            AnimationRiggingManager.AddReloadTimeTakeOverItem(item.Name);
            //Log.Out($"take over reload time: {item.Name} {item.Id}");
        }
    }

    [HarmonyPatch(typeof(ItemClassesFromXml), nameof(ItemClassesFromXml.parseItem))]
    [HarmonyPostfix]
    private static void Postfix_parseItem_ItemClassesFromXml(XElement _node)
    {
        ParseTakeOverReloadTime(_node);
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.ShowHeldItem))]
    [HarmonyPostfix]
    private static void Postfix_ShowHeldItem_Inventory(Inventory __instance, bool show)
    {
        if (!show && __instance.GetHoldingItemTransform() && __instance.GetHoldingItemTransform().TryGetComponent<RigTargets>(out var targets) && !targets.Destroyed)
        {
            targets.SetEnabled(false, true);
        }
    }

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.StopHolding))]
    [HarmonyPostfix]
    private static void Postfix_StopHolding_ItemClass(Transform _modelTransform)
    {
        if (_modelTransform != null && _modelTransform.TryGetComponent<RigTargets>(out var targets) && !targets.Destroyed)
        {
            targets.SetEnabled(false, true);
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.createHeldItem))]
    [HarmonyPostfix]
    private static void Postfix_createHeldItem_Inventory(Inventory __instance, Transform __result)
    {
        if (__result != null && __result.TryGetComponent<RigTargets>(out var targets) && !targets.Destroyed)
        {
            if (GameManager.IsDedicatedServer || !(__instance.entity is EntityPlayerLocal player))
            {
                targets.Destroy();
            }
            else
            {
                Transform fpsArms = (player.emodel.avatarController as AvatarLocalPlayerController)?.FPSArms?.Parts.BodyObj.transform;
                if (fpsArms != null)
                    targets.Init(fpsArms);
            }
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.ForceHoldingItemUpdate))]
    [HarmonyPrefix]
    private static bool Prefix_ForceHoldingItemUpdate(Inventory __instance)
    {
        if (__instance.entity is EntityPlayerLocal)
            AnimationRiggingManager.OnClearInventorySlot(__instance, __instance.holdingItemIdx);
        return true;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionEffects_ItemActionRanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var fld_fpv = AccessTools.Field(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.bFirstPersonView));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(fld_fpv))
            {
                codes.InsertRange(i + 4, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, codes[i + 3].operand),
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.particlesMuzzleFire))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.particlesMuzzleFireFpv))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.particlesMuzzleSmoke))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.particlesMuzzleSmokeFpv))),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.SpawnFpvParticles))),
                    new CodeInstruction(OpCodes.Brtrue_S, codes[i - 5].operand)
                });
                break;
            }
        }
        //FieldInfo fld_muzzle = AccessTools.Field(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.muzzle));
        //FieldInfo fld_muzzle2 = AccessTools.Field(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.muzzle2));
        //MethodInfo mtd_getmuzzle = AccessTools.Method(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.GetMuzzleOverrideFPV));
        //MethodInfo mtd_getmuzzle2 = AccessTools.Method(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.GetMuzzle2OverrideFPV));
        //for (int i = 0; i < codes.Count; i++)
        //{
        //    if (codes[i].LoadsField(fld_muzzle))
        //    {
        //        codes.InsertRange(i + 1, new[]
        //        {
        //            new CodeInstruction(OpCodes.Ldloc_S, 4),
        //            new CodeInstruction(OpCodes.Call, mtd_getmuzzle)
        //        });
        //    }
        //    else if (codes[i].LoadsField(fld_muzzle2))
        //    {
        //        codes.InsertRange(i + 1, new[]
        //        {
        //            new CodeInstruction(OpCodes.Ldloc_S, 4),
        //            new CodeInstruction(OpCodes.Call, mtd_getmuzzle2)
        //        });
        //    }
        //}

        return codes;
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.clearSlotByIndex))]
    [HarmonyPrefix]
    private static bool Prefix_clearSlotByIndex(Inventory __instance, int _idx)
    {
        if (__instance.entity is EntityPlayerLocal)
            AnimationRiggingManager.OnClearInventorySlot(__instance, _idx);
        return true;
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.Update))]
    [HarmonyPostfix]
    private static void Postfix_Update_AvatarMultiBodyController(AvatarMultiBodyController __instance)
    {
        if (__instance is AvatarLocalPlayerController avatarLocalPlayer)
        {
            //if ((avatarLocalPlayer.entity as EntityPlayerLocal).bFirstPersonView && !avatarLocalPlayer.entity.inventory.GetIsFinishedSwitchingHeldItem())
            //{
            //    avatarLocalPlayer.UpdateInt(AvatarController.weaponHoldTypeHash, -1, false);
            //    avatarLocalPlayer.UpdateBool("Holstered", false, false);
            //    avatarLocalPlayer.FPSArms.Animator.Play("idle", 0, 0f);
            //}
            AnimationRiggingManager.UpdateLocalPlayerAvatar(avatarLocalPlayer);
            var mapping = MultiActionManager.GetMappingForEntity(__instance.entity.entityId);
            if (mapping != null)
            {
                avatarLocalPlayer.UpdateInt(MultiActionUtils.ExecutingActionIndexHash, mapping.CurActionIndex, true);
            }
        }
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.StartAnimationReloading))]
    [HarmonyPrefix]
    private static bool Prefix_StartAnimationReloding_AvatarController(AvatarMultiBodyController __instance)
    {
        __instance.Entity?.FireEvent(CustomEnums.onReloadAboutToStart);
        return true;
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.StartAnimationReloading))]
    [HarmonyPostfix]
    private static void Postfix_StartAnimationReloading_AvatarMultibodyController(AvatarMultiBodyController __instance)
    {
        if (__instance.HeldItemTransform != null && __instance.HeldItemTransform.TryGetComponent<RigTargets>(out var targets) && !targets.Destroyed)
        {
            EntityAlive entity = __instance.Entity;
            ItemValue holdingItemItemValue = entity.inventory.holdingItemItemValue;
//#if DEBUG
//            float x = 1, y = 1;
//            var tags = entity.inventory.holdingItem.ItemTags;
//            var tags_prev = tags;
//            MultiActionManager.ModifyItemTags(entity.inventory.holdingItemItemValue, entity.MinEventContext.ItemActionData, ref tags);
//            entity.Progression.ModifyValue(PassiveEffects.ReloadSpeedMultiplier, ref x, ref y, tags);
//            Log.Out($"item {entity.inventory.holdingItem.Name} action index {entity.MinEventContext.ItemActionData.indexInEntityOfAction} progression base {x} perc {y} has tag {tags.Test_AnySet(FastTags.Parse("perkMachineGunner"))} \ntags prev {tags_prev} \ntags after {tags}");
//#endif
            float reloadSpeed = EffectManager.GetValue(PassiveEffects.ReloadSpeedMultiplier, holdingItemItemValue, 1f, entity);
            float reloadSpeedRatio = EffectManager.GetValue(CustomEnums.ReloadSpeedRatioFPV2TPV, holdingItemItemValue, 1f, entity);

            float partialReloadMultiplier = EffectManager.GetValue(CustomEnums.PartialReloadCount, holdingItemItemValue, 0, entity);
            float partialReloadRatio = 1f;
            if (partialReloadMultiplier <= 0)
            {
                partialReloadMultiplier = 1;
            }
            else
            {
                int magSize = (int)EffectManager.GetValue(PassiveEffects.MagazineSize, holdingItemItemValue, ((ItemActionRanged)entity.inventory.holdingItem.Actions[MultiActionManager.GetActionIndexForEntity(entity)]).BulletsPerMagazine, entity);
                //how many partial reload is required to fill an empty mag
                partialReloadRatio = Mathf.Ceil(magSize / partialReloadMultiplier);
                //how many partial reload is required to finish this reload
                partialReloadMultiplier = Mathf.Ceil((magSize - holdingItemItemValue.Meta) / partialReloadMultiplier);
                //reload time percentage of this reload
                partialReloadRatio = partialReloadMultiplier / partialReloadRatio;
            }

            float localMultiplier, remoteMultiplier;
            bool isFPV = entity as EntityPlayerLocal != null && (entity as EntityPlayerLocal).emodel.IsFPV;
            bool takeOverReloadTime = AnimationRiggingManager.IsReloadTimeTakeOverItem(holdingItemItemValue.type);

            if (isFPV && !takeOverReloadTime)
            {
                localMultiplier = reloadSpeed / reloadSpeedRatio;
            }
            else if (!isFPV && takeOverReloadTime)
            {
                localMultiplier = reloadSpeed * reloadSpeedRatio / partialReloadMultiplier;
            }
            else if(isFPV && takeOverReloadTime)
            {
                localMultiplier = reloadSpeed;
            }
            else
            {
                localMultiplier = reloadSpeed * partialReloadRatio;
            }

            if (takeOverReloadTime)
            {
                remoteMultiplier = reloadSpeed * reloadSpeedRatio / partialReloadMultiplier;
            }
            else
            {
                remoteMultiplier = reloadSpeed * partialReloadRatio;
            }

            if (ConsoleCmdReloadLog.LogInfo)
                Log.Out($"Set reload multiplier: isFPV {isFPV}, reloadSpeed {reloadSpeed}, reloadSpeedRatio {reloadSpeedRatio}, finalMultiplier {localMultiplier}, remoteMultiplier {remoteMultiplier}, partialMultiplier {partialReloadMultiplier}, partialRatio {partialReloadRatio}");
            
            __instance.UpdateFloat(AvatarController.reloadSpeedHash, localMultiplier, false);
            SetDataFloat(__instance, (AvatarController.DataTypes)AvatarController.reloadSpeedHash, remoteMultiplier, true);
        }
    }

    /// <summary>
    /// sets float only on remote clients but not on local client.
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="_type"></param>
    /// <param name="_value"></param>
    /// <param name="_netsync"></param>
    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController.SetDataFloat))]
    [HarmonyReversePatch(HarmonyReversePatchType.Original)]
    private static void SetDataFloat(AvatarController __instance, AvatarController.DataTypes _type, float _value, bool _netsync = true)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (instructions == null)
                return null;

            var codes = instructions.ToList();
            codes.RemoveRange(0, 5);
            codes[0].labels.Clear();
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsConstant(AnimParamData.ValueTypes.DataFloat))
                {
                    codes[i].opcode = OpCodes.Ldc_I4;
                    codes[i].operand = (int)AnimParamData.ValueTypes.Float;
                    break;
                }
            }
            return codes;
        }
        _ = Transpiler(null);
    }

    //[HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.StartAnimationAttack))]
    //[HarmonyPostfix]
    //private static void Postfix_StartAnimationAttack_AvatarMultiBodyController(AvatarMultiBodyController __instance)
    //{
    //    if (__instance is AvatarLocalPlayerController)
    //        AnimationRiggingManager.FpvWeaponFire();
    //}

    [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController.SetInRightHand))]
    [HarmonyPostfix]
    private static void Postfix_SetInRightHand_AvatarLocalPlayerController(Transform _transform, AvatarLocalPlayerController __instance)
    {
        if (_transform != null && _transform.TryGetComponent<RigTargets>(out var targets) && !targets.Destroyed)
        {
            targets.SetEnabled(__instance.isFPV);
            _transform.SetParent(__instance.CharacterBody.Parts.RightHandT, false);
            _transform.localPosition = Vector3.zero;
            _transform.localRotation = Quaternion.identity;
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.setHoldingItemTransform))]
    [HarmonyPrefix]
    private static bool Prefix_setHoldingItemTransform_Inventory(Inventory __instance)
    {
        if (__instance.lastdrawnHoldingItemTransform && __instance.lastdrawnHoldingItemTransform.TryGetComponent<RigTargets>(out var targets) && !targets.Destroyed)
        {
            targets.SetEnabled(false, true);
        }
        return true;
    }

    [HarmonyPatch(typeof(vp_FPWeapon), nameof(vp_FPWeapon.Start))]
    [HarmonyPostfix]
    private static void Postfix_Start_vp_FPWeapon(vp_FPWeapon __instance)
    {
        var player = __instance.GetComponentInParent<EntityPlayerLocal>();
        if (player != null)
        {
            foreach (var model in player.inventory.models)
            {
                if (model != null && model.TryGetComponent<RigTargets>(out var targets) && !targets.Destroyed)
                {
                    targets.Init(__instance.transform);
                }
            }
        }
    }

    #region temporary fix for arm glitch on switching weapon
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.updateHoldingItem))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_updateHoldingItem_Inventory(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_setparent = AccessTools.Method(typeof(Transform), nameof(Transform.SetParent), new[] { typeof(Transform), typeof(bool) });

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_setparent))
            {
                codes[i - 1].opcode = OpCodes.Ldc_I4_1;
                break;
            }
        }
        return codes;
    }

    //private static Coroutine delayShowWeaponCo;
    //private static IEnumerator DelayShowWeapon(Camera camera)
    //{
    //    Log.Out($"Delay show weapon!");
    //    camera.cullingMask &= ~(1 << 10);
    //    yield return new WaitForSeconds(0.5f);
    //    if (camera)
    //    {
    //        camera.cullingMask |= 1 << 10;
    //    }
    //    delayShowWeaponCo = null;
    //    Log.Out($"Show weapon!");
    //    yield break;
    //}

    //[HarmonyPatch(typeof(Inventory), nameof(Inventory.setHeldItemByIndex))]
    //[HarmonyPrefix]
    //private static bool Prefix_setHeldItemByIndex_Inventory(Inventory __instance, out bool __state)
    //{
    //    __state = __instance.holdingItemData?.model && __instance.holdingItemData.model.GetComponent<RigTargets>();

    //    return true;
    //}

    //[HarmonyPatch(typeof(Inventory), nameof(Inventory.setHeldItemByIndex))]
    //[HarmonyPostfix]
    //private static void Postfix_setHeldItemByIndex_Inventory(Inventory __instance, bool __state)
    //{
    //    if (__state && __instance.entity is EntityPlayerLocal player && player.bFirstPersonView && (!__instance.holdingItemData?.model || !__instance.holdingItemData.model.GetComponent<RigTargets>()))
    //    {
    //        if (delayShowWeaponCo != null)
    //        {
    //            ThreadManager.StopCoroutine(delayShowWeaponCo);
    //        }

    //        if (__instance.holdingItemIdx == __instance.DUMMY_SLOT_IDX)
    //        {
    //            player.ShowHoldingItem(true);
    //        }
    //        else
    //        {
    //            delayShowWeaponCo = ThreadManager.StartCoroutine(DelayShowWeapon(player.playerCamera));
    //        }
    //    }
    //}

    //[HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.ShowHoldingItem))]
    //[HarmonyPrefix]
    //private static bool Prefix_ShowHoldingItem_EntityPlayerLocal(bool show)
    //{
    //    if (delayShowWeaponCo != null)
    //    {
    //        if (show)
    //        {
    //            return false;
    //        }
    //        ThreadManager.StopCoroutine(delayShowWeaponCo);
    //    }
    //    return true;
    //}
    /*
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.setHoldingItemTransform))]
    [HarmonyPostfix]
    private static void Postfix_setHoldingItemTransform_Inventory(Transform _t, Inventory __instance)
    {
        if (_t != null && _t.TryGetComponent<RigTargets>(out var targets) && !targets.Destroyed)
        {
            targets.SetEnabled(__instance.entity.emodel.IsFPV);
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.SetItem), new[] {typeof(int), typeof(ItemValue), typeof(int), typeof(bool)})]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_SetItem_Inventory(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo mtd_update = AccessTools.Method(typeof(Inventory), nameof(Inventory.updateHoldingItem));
        foreach (var code in instructions)
        {
            yield return code;
            if (code.Calls(mtd_update))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
                yield return CodeInstruction.Call(typeof(Inventory), nameof(Inventory.ShowHeldItem));
            }
        }
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.ShowWeaponCamera))]
    [HarmonyPostfix]
    private static void Postfix_ShowWeaponCamera_EntityPlayerLocal(EntityPlayerLocal __instance, bool show)
    {
        if (__instance.bFirstPersonView)
        {
            __instance.weaponCamera.cullingMask &= ~(1 << 10);
            if (delayShowWeaponCo != null)
            {
                ThreadManager.StopCoroutine(delayShowWeaponCo);
            }
            if (show)
            {
                delayShowWeaponCo = ThreadManager.StartCoroutine(DelayShowWeapon(__instance.weaponCamera));
            }
        }
    }

    */
    #endregion

    [HarmonyPatch(typeof(World), nameof(World.SpawnEntityInWorld))]
    [HarmonyPrefix]
    private static bool Prefix_SpawnEntityInWorld_World(Entity _entity)
    {
        if (_entity is EntityItem _entityItem)
        {
            var targets = _entityItem.GetComponentInChildren<RigTargets>(true);
            if (targets != null && !targets.Destroyed)
            {
                targets.Destroy();
            }
        }
        return true;
    }

    [HarmonyPatch(typeof(SDCSUtils), nameof(SDCSUtils.cleanupEquipment))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_cleanupEquipment_SDCSUtils(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_removeat = AccessTools.Method(typeof(List<RigLayer>), nameof(List<RigLayer>.RemoveAt));
        var mtd_destroy = AccessTools.Method(typeof(GameUtils), nameof(GameUtils.DestroyAllChildrenBut), new[] {typeof(Transform), typeof(List<string>)});
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_removeat))
            {
                codes.InsertRange(i - 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldloc_3),
                    CodeInstruction.Call(typeof(List<RigLayer>), "get_Item"),
                    CodeInstruction.Call(typeof(RigLayer), "get_name"),
                    CodeInstruction.Call(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.ShouldExcludeRig)),
                    new CodeInstruction(OpCodes.Brtrue_S, codes[i - 3].operand)
                });
                i += 6;
            }
            else if (codes[i].Calls(mtd_destroy))
            {
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Dup),
                    CodeInstruction.Call(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.GetExcludeRigs)),
                    CodeInstruction.Call(typeof(List<string>), nameof(List<string>.AddRange)),
                });
                i += 3;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.SwapSelectedAmmo))]
    [HarmonyPrefix]
    private static bool Prefix_SwapSelectedAmmo_ItemActionRanged(ItemActionRanged __instance, EntityAlive _entity, int _ammoIndex)
    {
        if (_ammoIndex == (int)_entity.inventory.holdingItemItemValue.SelectedAmmoTypeIndex && __instance is IModuleContainerFor<ActionModuleInspectable> inspectable && _entity is EntityPlayerLocal player)
        {
            ItemActionRanged.ItemActionDataRanged _actionData = _entity.inventory.holdingItemData.actionData[__instance.ActionIndex] as ItemActionRanged.ItemActionDataRanged;
            if (!_entity.MovementRunning && !_entity.AimingGun && !player.bLerpCameraFlag && _actionData != null && !_entity.inventory.holdingItem.IsActionRunning(_entity.inventory.holdingItemData) && !__instance.CanReload(_actionData) && (_entity.inventory.holdingItemItemValue.Meta > 0 || inspectable.Instance.allowEmptyInspect))
            {
                _entity.emodel.avatarController._setTrigger("weaponInspect", false);
                return false;
            }
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction))]
    [HarmonyPostfix]
    private static void Postfix_ExecuteAction_ItemActionRanged(ItemActionRanged __instance, ItemActionData _actionData)
    {
        if (_actionData is ItemActionRanged.ItemActionDataRanged rangedData)
        {
            int burstCount = __instance.GetBurstCount(_actionData);
            _actionData.invData.holdingEntity.emodel.avatarController._setBool("TriggerPulled", rangedData.bPressed && rangedData.curBurstCount < burstCount, false);
        }
    }

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.LateInitAll))]
    [HarmonyPostfix]
    private static void Postfix_LateInitAll_ItemClass()
    {
        AnimationRiggingManager.ParseItemIDs();
    }

    [HarmonyPatch(typeof(Animator), nameof(Animator.Rebind), new Type[0])]
    [HarmonyReversePatch(HarmonyReversePatchType.Original)]
    public static void RebindNoDefault(this Animator __instance)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (instructions == null)
            {
                yield break;
            }
            foreach (var ins in instructions)
            {
                if (ins.opcode == OpCodes.Ldc_I4_1)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                }
                else
                {
                    yield return ins;
                }
            }
        }
        _ = Transpiler(null);
    }

    //[HarmonyPatch(typeof(ItemActionDynamic), nameof(ItemActionDynamic.GetExecuteActionGrazeTarget))]
    //[HarmonyPostfix]
    //private static void Postfix_Test2(WorldRayHitInfo[] __result)
    //{
    //    Log.Out($"World ray info count: {__result.Length}");
    //}

    //[HarmonyPatch(typeof(ItemActionDynamic), nameof(ItemActionDynamic.hitTarget))]
    //[HarmonyPostfix]
    //private static void Postfix_hittest(ItemActionData _actionData, WorldRayHitInfo hitInfo, bool _isGrazingHit = false)
    //{
    //    Log.Out($"HIT TARGET! IsGrazing: {_isGrazingHit}\n{StackTraceUtility.ExtractStackTrace()}");
    //}

    //[HarmonyPatch(typeof(XUiC_CameraWindow), nameof(XUiC_CameraWindow.OnOpen))]
    //[HarmonyPrefix]
    //private static bool Prefix_OnOpen_XuiC_CameraWindow(XUiC_CameraWindow __instance)
    //{
    //    AnimationRiggingManager.IsCameraWindowOpen = true;
    //    Inventory inventory = __instance.xui.playerUI.localPlayer.entityPlayerLocal.inventory;
    //    AnimationRiggingManager.OnClearInventorySlot(inventory, inventory.holdingItemIdx);
    //    return true;
    //}

    //[HarmonyPatch(typeof(XUiC_CameraWindow), nameof(XUiC_CameraWindow.OnClose))]
    //[HarmonyPrefix]
    //private static bool Prefix_OnClose_XuiC_CameraWindow()
    //{
    //    AnimationRiggingManager.IsCameraWindowOpen = false;
    //    return true;
    //}

    /// <summary>
    /// Changed in A22?
    /// </summary>
    /// <param name="___sensorCamera"></param>
    //[HarmonyPatch(typeof(XUiC_CameraWindow), nameof(XUiC_CameraWindow.OnOpen))]
    //[HarmonyTranspiler]
    //private static IEnumerable<CodeInstruction> Transpiler_OnOpen_XuiC_CameraWindow(IEnumerable<CodeInstruction> instructions)
    //{
    //    var codes = instructions.ToList();

    //    var mtd_switch = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.switchModelView));
    //    for (int i = 0; i < codes.Count; i++)
    //    {
    //        if (codes[i].Calls(mtd_switch))
    //        {
    //            //codes[i - 1].opcode = OpCodes.Ldc_I4_0;
    //            //codes[i].opcode = OpCodes.Call;
    //            //codes[i].operand = AccessTools.Method(typeof(EntityPlayerLocal), "setFirstPersonView");
    //            //codes.Insert(i, new CodeInstruction(OpCodes.Ldc_I4_1));
    //            codes.Insert(i - 5, new CodeInstruction(OpCodes.Br_S, codes[i - 6].operand));
    //            break;
    //        }
    //    }

    //    return codes;
    //}

    //[HarmonyPatch(typeof(XUiC_CameraWindow), nameof(XUiC_CameraWindow.OnClose))]
    //[HarmonyTranspiler]
    //private static IEnumerable<CodeInstruction> Transpiler_OnClose_XuiC_CameraWindow(IEnumerable<CodeInstruction> instructions)
    //{
    //    var codes = instructions.ToList();

    //    var mtd_switch = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.switchModelView));
    //    for (int i = 0; i < codes.Count; i++)
    //    {
    //        if (codes[i].Calls(mtd_switch))
    //        {
    //            //codes[i - 1].opcode = OpCodes.Ldc_I4_1;
    //            //codes[i].opcode = OpCodes.Call;
    //            //codes[i].operand = AccessTools.Method(typeof(EntityPlayerLocal), "setFirstPersonView");
    //            //codes.Insert(i, new CodeInstruction(OpCodes.Ldc_I4_1));
    //            codes.RemoveRange(i - 5, 6);
    //            break;
    //        }
    //    }

    //    return codes;
    //}

    //[HarmonyPatch(typeof(XUiC_CameraWindow), "CreateCamera")]
    //[HarmonyPostfix]
    //private static void Postfix_CreateCamera_XUiC_CameraWindow(Camera ___sensorCamera)
    //{
    //    ___sensorCamera.cullingMask &= ~(1 << 24 | 1 << 10);
    //}

    [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._setTrigger))]
    [HarmonyPostfix]
    private static void Postfix_Avatar_SetTrigger(int _pid)
    {
        AnimationRiggingManager.SetTrigger(_pid);
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._resetTrigger))]
    [HarmonyPostfix]
    private static void Postfix_Avatar_ResetTrigger(int _pid)
    {
        AnimationRiggingManager.ResetTrigger(_pid);
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._setFloat))]
    [HarmonyPostfix]
    private static void Postfix_Avatar_SetFloat(int _pid, float _value)
    {
        AnimationRiggingManager.SetFloat(_pid, _value);
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._setBool))]
    [HarmonyPostfix]
    private static void Postfix_Avatar_SetBool(int _pid, bool _value)
    {
        AnimationRiggingManager.SetBool(_pid, _value);
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._setInt))]
    [HarmonyPostfix]
    private static void Postfix_Avatar_SetInt(int _pid, int _value)
    {
        AnimationRiggingManager.SetInt(_pid, _value);
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._resetTrigger), typeof(int), typeof(bool))]
    [HarmonyReversePatch(HarmonyReversePatchType.Original)]
    public static void VanillaResetTrigger(AvatarLocalPlayerController __instance, int _pid, bool _netsync = true)
    {

    }
}
