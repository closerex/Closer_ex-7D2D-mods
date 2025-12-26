using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using UniLinq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

[HarmonyPatch]
static class AnimationRiggingPatches
{
    [HarmonyPatch(typeof(SDCSUtils), nameof(SDCSUtils.setupEquipmentCommon))]
    [HarmonyPrefix]
    private static bool Prefix_setupEquipmentCommon_SDCSUtils(GameObject _rigObj, out bool __state)
    {
        __state = false;
        if (_rigObj.TryGetComponent<Animator>(out var animator))
        {
            __state = true;
            animator.UnbindAllStreamHandles();
            animator.UnbindAllSceneHandles();
        }
        return true;
    }

    [HarmonyPatch(typeof(SDCSUtils), nameof(SDCSUtils.setupEquipmentCommon))]
    [HarmonyPostfix]
    private static void Postfix_setupEquipmentCommon_SDCSUtils(GameObject _rigObj, bool __state)
    {
        if (__state && _rigObj.TryGetComponent<Animator>(out var animator))
        {
            animator.Rebind();
        }
    }

    [HarmonyPatch(typeof(SDCSUtils), nameof(SDCSUtils.setupRig))]
    [HarmonyPrefix]
    private static bool Prefix_setupRig_SDCSUtils(ref RuntimeAnimatorController animController, ref GameObject _rigObj)
    {
        if (_rigObj && _rigObj.TryGetComponent<AnimationGraphBuilder>(out var builder) && builder.HasWeaponOverride)
        {
            animController = null;
        }
        return true;
    }

    //[HarmonyPatch(typeof(UMACharacterBodyAnimator), nameof(UMACharacterBodyAnimator.assignLayerWeights))]
    //[HarmonyPrefix]
    //private static bool Prefix_assignLayerWeights_UMACharacterBodyAnimator(UMACharacterBodyAnimator __instance)
    //{
    //    if (__instance.Animator && __instance.Animator.TryGetComponent<AnimationGraphBuilder>(out var builder) && builder.HasWeaponOverride)
    //    {
    //        return false;
    //    }
    //    return true;
    //}

    //[HarmonyPatch(typeof(AvatarSDCSController), nameof(AvatarSDCSController.setLayerWeights))]
    //[HarmonyPrefix]
    //private static bool Prefix_setLayerWeights_AvatarSDCSController(AvatarSDCSController __instance)
    //{
    //    if (__instance.anim && __instance.anim.TryGetComponent<AnimationGraphBuilder>(out var builder) && builder.HasWeaponOverride)
    //    {
    //        return false;
    //    }
    //    return true;
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
        rangedData.Laser = AnimationRiggingManager.GetTransformOverrideByName(rangedData.invData.model, "laser", false);

        ItemActionLauncher.ItemActionDataLauncher launcherData = _data as ItemActionLauncher.ItemActionDataLauncher;
        if (launcherData != null)
        {
            launcherData.projectileJointT = AnimationRiggingManager.GetTransformOverrideByName(launcherData.invData.model, "ProjectileJoint");
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
    private static IEnumerable<CodeInstruction> Transpiler_Execute_MinEventActionAttachPrefabToHeldItem(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var mtd_find = AccessTools.Method(typeof(GameUtils), nameof(GameUtils.FindDeepChild));
        var fld_trans = AccessTools.Field(typeof(MinEventParams), nameof(MinEventParams.Transform));
        var mtd_layer = AccessTools.Method(typeof(Utils), nameof(Utils.SetLayerRecursively), new[] {typeof(GameObject), typeof(int), typeof(string[])} );

        var lbd_targets = generator.DeclareLocal(typeof(AnimationTargetsAbs));

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
            else if (codes[i].opcode == OpCodes.Stloc_2)
            {
                var lbl = generator.DefineLabel();
                var lbls = codes[i + 1].ExtractLabels();
                codes[i + 1].WithLabels(lbl);
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1).WithLabels(lbls),
                    CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.Transform)),
                    new CodeInstruction(OpCodes.Ldnull),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), "op_Inequality")),
                    new CodeInstruction(OpCodes.Brfalse_S, lbl),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.Transform)),
                    CodeInstruction.Call(typeof(Transform), nameof(Transform.GetComponent), new Type[0], new Type[]{ typeof(AnimationTargetsAbs)}),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_targets)
                });
                i += 9;
            }
            else if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 4)
            {
                codes.RemoveAt(i - 1);
                codes.InsertRange(i - 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_targets),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    CodeInstruction.Call(typeof(AnimationRiggingPatches), nameof(CreateOrMoveAttachment))
                });
                i += 2;
            }
            else if (codes[i].Calls(mtd_layer))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_targets),
                    new CodeInstruction(OpCodes.Ldloc_S, 4),
                    CodeInstruction.Call(typeof(AnimationRiggingPatches), nameof(CheckAttachmentRefMerge))
                });
                i += 3;
            }
            else if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 5)
            {
                var lbl = generator.DefineLabel();
                var lbls = codes[i + 1].ExtractLabels();
                codes[i + 1].WithLabels(lbl);
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_3).WithLabels(lbls),
                    CodeInstruction.Call(typeof(Transform), nameof(Transform.GetComponent), new Type[0], new Type[]{ typeof(IgnoreTint)}),
                    new CodeInstruction(OpCodes.Ldnull),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), "op_Inequality")),
                    new CodeInstruction(OpCodes.Brfalse_S, lbl),
                    new CodeInstruction(OpCodes.Ret)
                });
                i += 6;
            }
        }
        codes.InsertRange(0, new[]
        {
            new CodeInstruction(OpCodes.Ldnull),
            new CodeInstruction(OpCodes.Stloc_S, lbd_targets)
        });
        return codes;
    }

    private static GameObject CreateOrMoveAttachment(GameObject go, AnimationTargetsAbs targets, string name)
    {
        GameObject res = null;
        if (targets)
        {
            res = targets.GetPrefab(name);
        }
        if (!res)
        {
            res = GameObject.Instantiate(go);
        }
        return res;
    }

    private static void CheckAttachmentRefMerge(AnimationTargetsAbs targets, GameObject attachmentReference)
    {
        if (targets && attachmentReference)
        {
            targets.AttachPrefab(attachmentReference);
        }
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

    [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnHoldingItemChanged))]
    [HarmonyPostfix]
    private static void Postfix_OnHoldingItemChanged_EntityAlive(EntityAlive __instance)
    {
        AnimationRiggingManager.OnHoldingItemIndexChanged(__instance as EntityPlayer);
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

    //[HarmonyPatch(typeof(ItemClass), nameof(ItemClass.StopHolding))]
    //[HarmonyPostfix]
    //private static void Postfix_StopHolding_ItemClass(Transform _modelTransform)
    //{
    //    if (_modelTransform != null && _modelTransform.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
    //    {
    //        targets.SetEnabled(false);
    //    }
    //}

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.createHeldItem))]
    [HarmonyPostfix]
    private static void Postfix_createHeldItem_Inventory(Inventory __instance, Transform __result, int _idx)
    {
        if (__result && __result.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
        {
            if (GameManager.IsDedicatedServer || !(__instance.entity is EntityPlayer player))
            {
                targets.Destroy();
            }
            else
            {
                if (player is EntityPlayerLocal localPlayer)
                {
                    targets.Init(localPlayer.emodel.avatarController.GetActiveModelRoot(), localPlayer.bFirstPersonView, _idx);
                }
                else
                {
                    targets.DestroyFpv();
                    targets.Init(player.emodel.avatarController.GetActiveModelRoot(), false, _idx);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.ForceHoldingItemUpdate))]
    [HarmonyPrefix]
    private static bool Prefix_ForceHoldingItemUpdate_Inventory(Inventory __instance)
    {
        if (__instance.entity is EntityPlayer)
            AnimationRiggingManager.OnClearInventorySlot(__instance, __instance.holdingItemIdx);
        return true;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionEffects_ItemActionRanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var fld_fpv = AccessTools.Field(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.bFirstPersonView));
        var fld_suppressed = AccessTools.Field(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.IsFlashSuppressed));
        var fld_smoke = AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.particlesMuzzleSmoke));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(fld_suppressed))
            {
                CodeInstruction jump_suppressed = codes[i + 1], jump_muzzle = codes[i + 5];
                var takeif = codes.GetRange(i + 2, 4);
                codes.RemoveRange(i + 2, 4);
                takeif[0].WithLabels(codes[i - 1].ExtractLabels());
                codes.InsertRange(i - 1, takeif);

                for (int j = i + 1; j < codes.Count; j++)
                {
                    // don't skip smoke creation even if suppressed
                    if (codes[j].LoadsField(fld_smoke))
                    {
                        jump_suppressed.operand = codes[j - 1].labels[0];
                        break;
                    }
                }

                for (int j = i + 1; j < codes.Count; j++)
                {
                    if (codes[j].opcode == OpCodes.Ldloc_2)
                    {
                        int fpvLocalIndex = Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxServer ? 5 : 7;
                        for (int k = j + 1; k < codes.Count; k++)
                        {
                            if (codes[k].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[k].operand).LocalIndex == fpvLocalIndex)
                            {
                                var lbd = (LocalBuilder)codes[k].operand;
                                var take = codes.GetRange(j, k - j + 1);
                                codes.RemoveRange(j, k - j + 1);
                                take[0].WithLabels(codes[i - 1].ExtractLabels());
                                // spawn fpv particles
                                codes.InsertRange(i - 1, new[]
                                {
                                    new CodeInstruction(OpCodes.Ldloc_S, lbd),
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
                                    new CodeInstruction(OpCodes.Brtrue, jump_muzzle.operand)
                                });
                                // move fpv check before suppressed check
                                codes.InsertRange(i - 1, take);
                                i += take.Count + 12;
                                break;
                            }
                        }
                        break;
                    }
                }
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
        if (__instance.entity is EntityPlayer)
            AnimationRiggingManager.OnClearInventorySlot(__instance, _idx);
        return true;
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.Update))]
    [HarmonyPostfix]
    private static void Postfix_Update_AvatarMultiBodyController(AvatarMultiBodyController __instance)
    {
        AnimationRiggingManager.UpdatePlayerAvatar(__instance);
        if (__instance is AvatarLocalPlayerController avatarLocalPlayer)
        {
            //if ((avatarLocalPlayer.entity as EntityPlayerLocal).bFirstPersonView && !avatarLocalPlayer.entity.inventory.GetIsFinishedSwitchingHeldItem())
            //{
            //    avatarLocalPlayer.UpdateInt(AvatarController.weaponHoldTypeHash, -1, false);
            //    avatarLocalPlayer.UpdateBool("Holstered", false, false);
            //    avatarLocalPlayer.FPSArms.Animator.Play("idle", 0, 0f);
            //}
            var mapping = MultiActionManager.GetMappingForEntity(__instance.entity.entityId);
            if (mapping != null)
            {
                avatarLocalPlayer.UpdateInt(MultiActionUtils.ExecutingActionIndexHash, mapping.CurActionIndex, true);
            }

            if (__instance.entity.inventory?.holdingItemData?.actionData != null)
            {
                foreach (var actionData in __instance.entity.inventory.holdingItemData.actionData)
                {
                    if (actionData is IModuleContainerFor<ActionModuleFireModeSelector.FireModeData> data)
                    {
                        avatarLocalPlayer.UpdateInt(ActionModuleFireModeSelector.FireModeParamHashes[actionData.indexInEntityOfAction], data.Instance.currentFireMode, true);
                    }
                }
            }
        }
        if (__instance.entity.AttachedToEntity)
        {
            __instance.SetVehicleAnimation(AvatarController.vehiclePoseHash, __instance.entity.vehiclePoseMode);
        }
    }

    [HarmonyPatch(typeof(LegacyAvatarController), nameof(LegacyAvatarController.Update))]
    [HarmonyPostfix]
    private static void Postfix_Update_LegacyAvatarController(LegacyAvatarController __instance)
    {
        AnimationRiggingManager.UpdatePlayerAvatar(__instance);
        if (__instance.entity && __instance.entity.AttachedToEntity)
        {
            __instance.SetVehicleAnimation(AvatarController.vehiclePoseHash, __instance.entity.vehiclePoseMode);
        }
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Update_EntityPlayerLocal(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var mtd_update = AccessTools.Method(typeof(AvatarController), nameof(AvatarController.UpdateBool), new[] {typeof(string), typeof(bool), typeof(bool)} );
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_update))
            {
                var lbl = generator.DefineLabel();
                codes[i + 1].WithLabels(lbl);
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.Call(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.IsHoldingRiggedWeapon)),
                    new CodeInstruction(OpCodes.Brfalse_S, lbl),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.emodel)),
                    CodeInstruction.LoadField(typeof(EModelBase), nameof(EModelBase.avatarController)),
                    new CodeInstruction(OpCodes.Ldstr, "Holstered"),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(codes[i].opcode, codes[i].operand)
                });
                break;
            }
        }
        return codes;
    }

    //[HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController.LateUpdate))]
    //[HarmonyPostfix]
    //private static void Postfix_LateUpdate_AvatarLocalPlayerController(AvatarLocalPlayerController __instance)
    //{
    //    var targets = AnimationRiggingManager.GetRigTargetsFromPlayer(__instance.entity as EntityPlayer);
    //    if (targets && !targets.Destroyed)
    //    {
    //        targets.UpdateTpvSpineRotation(__instance.entity as EntityPlayer);
    //    }
    //}

    //[HarmonyPatch(typeof(AvatarSDCSController), nameof(AvatarSDCSController.LateUpdate))]
    //[HarmonyPostfix]
    //private static void Postfix_LateUpdate_AvatarSDCSController(AvatarSDCSController __instance)
    //{
    //    var targets = AnimationRiggingManager.GetRigTargetsFromPlayer(__instance.entity as EntityPlayer);
    //    if (targets && !targets.Destroyed)
    //    {
    //        targets.UpdateTpvSpineRotation(__instance.entity as EntityPlayer);
    //    }
    //}

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
        if (__instance.HeldItemTransform != null && __instance.HeldItemTransform.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
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

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.SetFirstPersonView))]
    [HarmonyPrefix]
    private static bool Prefix_SetFirstPersonView_EntityPlayerLocal(EntityPlayerLocal __instance, bool _bFirstPersonView)
    {
        var targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(__instance);
        if (_bFirstPersonView != __instance.bFirstPersonView && targets && !targets.Destroyed)
        {
            //targets.SetEnabled(false);
            //targets.GraphBuilder.SetCurrentTarget(null);
            Log.Out($"Switch view destroy slot {__instance.inventory.holdingItemIdx}");
            targets.Destroy();
        }
        return true;
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController.SwitchModelAndView))]
    [HarmonyPostfix]
    private static void Postfix_SwitchModelAndView_AvatarLocalPlayerController(AvatarLocalPlayerController __instance, bool _bFPV)
    {
        if (_bFPV)
        {
            __instance.hasTurnRate = false;
        }
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController.SetInRightHand))]
    [HarmonyPostfix]
    private static void Postfix_SetInRightHand_AvatarLocalPlayerController(Transform _transform, AvatarLocalPlayerController __instance)
    {
        if (_transform != null && _transform.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
        {
            //targets.SetEnabled(true);
            targets.GraphBuilder.SetCurrentTarget(targets);
        }
        else if (__instance.PrimaryBody?.Animator && __instance.PrimaryBody.Animator.TryGetComponent<AnimationGraphBuilder>(out var builder))
        {
            builder.SetCurrentTarget(null);
        }
    }

    [HarmonyPatch(typeof(LegacyAvatarController), nameof(LegacyAvatarController.SetInRightHand))]
    [HarmonyPostfix]
    private static void Postfix_SetInRightHand_LegacyAvatarController(Transform _transform, LegacyAvatarController __instance)
    {
        if (_transform != null && _transform.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
        {
            //targets.SetEnabled(true);
            targets.GraphBuilder.SetCurrentTarget(targets);
        }
        else if (__instance.anim && __instance.anim.TryGetComponent<AnimationGraphBuilder>(out var builder))
        {
            builder.SetCurrentTarget(null);
        }
    }

    //[HarmonyPatch(typeof(Inventory), nameof(Inventory.setHoldingItemTransform))]
    //[HarmonyPrefix]
    //private static bool Prefix_setHoldingItemTransform_Inventory(Inventory __instance)
    //{
    //    if (__instance.lastdrawnHoldingItemTransform && __instance.lastdrawnHoldingItemTransform.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
    //    {
    //        targets.SetEnabled(false);
    //    }
    //    return true;
    //}

    [HarmonyPatch(typeof(vp_FPWeapon), nameof(vp_FPWeapon.Start))]
    [HarmonyPostfix]
    private static void Postfix_Start_vp_FPWeapon(vp_FPWeapon __instance)
    {
        var player = __instance.GetComponentInParent<EntityPlayerLocal>();
        if (player && player.inventory != null)
        {
            for (int i = 0; i < player.inventory.models.Length; i++)
            {
                Transform model = player.inventory.models[i];
                if (model != null && model.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
                {
                    if (i == player.inventory.holdingItemIdx)
                    {
                        player.inventory.ForceHoldingItemUpdate();
                    }
                    else
                    {
                        targets.Init(__instance.transform, true, i);
                    }
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
        var mtd_startholding = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.StartHolding));
        var mtd_showrighthand = AccessTools.Method(typeof(Inventory), nameof(Inventory.ShowRightHand));
        var mtd_holdingchanged = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.OnHoldingItemChanged));
        var prop_holdingitem = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItem));
        var fld_transform = AccessTools.Field(typeof(MinEventParams), nameof(MinEventParams.Transform));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_setparent))
            {
                codes[i - 1].opcode = OpCodes.Ldc_I4_1;
            }
            else if (codes[i].Calls(mtd_startholding))
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (codes[j].Calls(prop_holdingitem))
                    {
                        for (int k = i + 1; k < codes.Count; k++)
                        {
                            if (codes[k].StoresField(fld_transform))
                            {
                                codes.InsertRange(k + 1, new[]
                                {
                                    new CodeInstruction(OpCodes.Ldloc_0).WithLabels(codes[k + 1].ExtractLabels()),
                                    CodeInstruction.LoadField(typeof(CustomEnums), nameof(CustomEnums.onSelfHoldingItemAssemble)),
                                    new CodeInstruction(OpCodes.Ldarg_0),
                                    CodeInstruction.LoadField(typeof(Inventory), nameof(Inventory.entity)),
                                    CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.MinEventContext)),
                                    CodeInstruction.Call(typeof(ItemValue), nameof(ItemValue.FireEvent)),
                                });
                                k += 6;
                            }
                            else if (codes[k].Calls(mtd_showrighthand))
                            {
                                codes.InsertRange(k + 1, codes.GetRange(j - 1, i - j + 2));
                                codes[i + 1].WithLabels(codes[j - 1].ExtractLabels());
                                codes.RemoveRange(j - 1, i - j + 2);
                                break;
                            }
                        }
                        break;
                    }
                }
                i += 6;
            }
            else if (codes[i].Calls(mtd_holdingchanged))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[i + 1].ExtractLabels()),
                    CodeInstruction.Call(typeof(Inventory), nameof(Inventory.syncHeldItem))
                });
                break;
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.setHoldingItemTransform))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setHoldingItemTransform_Inventory(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_sync = AccessTools.Method(typeof(Inventory), nameof(Inventory.syncHeldItem));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_sync))
            {
                codes[i + 1].WithLabels(codes[i - 1].ExtractLabels());
                codes.RemoveRange(i - 1, 2);
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
            var targets = _entityItem.GetComponentInChildren<AnimationTargetsAbs>(true);
            if (targets && !targets.Destroyed)
            {
                targets.Destroy();
            }
        }
        return true;
    }

    [HarmonyPatch(typeof(World), nameof(World.SpawnEntityInWorld))]
    [HarmonyPostfix]
    private static void Postfix_SpawnEntityInWorld_World(Entity _entity)
    {
        if (_entity is EntityPlayer player && !(_entity is EntityPlayerLocal) && player.inventory != null)
        {
            for (int i = 0; i < player.inventory.models.Length; i++)
            {
                Transform model = player.inventory.models[i];
                if (model && model.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
                {
                    targets.DestroyFpv();
                    targets.Init(player.emodel.avatarController.GetActiveModelRoot(), false, i);
                }
            }
        }
    }

    [HarmonyPatch(typeof(EntityItem), nameof(EntityItem.createMesh))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_createMesh_EntityItem(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var fld_created = AccessTools.Field(typeof(EntityItem), nameof(EntityItem.bMeshCreated));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].StoresField(fld_created))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[i + 1].ExtractLabels()),
                    CodeInstruction.CallClosure<Action<EntityItem>>((entityItem) =>
                    {
                        if (entityItem.itemTransform)
                        {
                            entityItem.itemTransform.tag = "Item";
                            foreach (var collider in entityItem.itemTransform.GetComponentsInChildren<Collider>(true))
                            {
                                collider.transform.tag = "Item";
	                        }
                            if (entityItem.itemTransform.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
                            {
                                targets.Destroy();
                            }
                        }
                    })
                });
                break;
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(EModelBase), nameof(EModelBase.SwitchModelAndView))]
    [HarmonyPostfix]
    private static void Postfix_SwitchModelAndView_EModelBase(EModelBase __instance)
    {
        if (__instance.entity is EntityPlayerLocal player && player.inventory != null)
        {
            for (int i = 0; i < player.inventory.models.Length; i++)
            {
                Transform model = player.inventory.models[i];
                if (model && model.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
                {
                    targets.Init(player.emodel.avatarController.GetActiveModelRoot(), player.bFirstPersonView, i);
                }
            }
        }
    }

    [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.Detach))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Detach_EntityAlive(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var fld_inv = AccessTools.Field(typeof(EntityAlive), nameof(EntityAlive.inventory));
        var mtd_setindex = AccessTools.Method(typeof(Inventory), nameof(Inventory.SetHoldingItemIdxNoHolsterTime));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].StoresField(fld_inv))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.Call(typeof(AnimationRiggingPatches), nameof(AnimationRiggingPatches.DetachInitInventory))
                });
                i += 2;
            }
            else if (codes[i].Calls(mtd_setindex))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, fld_inv),
                    CodeInstruction.Call(typeof(Inventory), nameof(Inventory.ForceHoldingItemUpdate))
                });
                i += 3;
            }
        }
        return codes;
    }

    private static void DetachInitInventory(EntityAlive __instance)
    {
        if (!(__instance is EntityPlayer player))
        {
            return;
        }
        if (__instance.inventory != null)
        {
            for (int i = 0; i < __instance.inventory.models.Length; i++)
            {
                Transform model = __instance.inventory.models[i];
                if (model && model.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
                {
                    targets.Init(__instance.emodel.avatarController.GetActiveModelRoot(), player is EntityPlayerLocal localPlayer ? localPlayer.bFirstPersonView : false, i);
                }
            }
        }
    }

    [HarmonyPatch(typeof(SDCSUtils), nameof(SDCSUtils.cleanupEquipment))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_cleanupEquipment_SDCSUtils(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_removeat = AccessTools.Method(typeof(List<RigLayer>), nameof(List<RigLayer>.RemoveAt));
        MethodInfo mtd_destroy;
        if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 4))
        {
            mtd_destroy = AccessTools.Method(typeof(GameUtils), nameof(GameUtils.DestroyAllChildrenBut), new[] { typeof(Transform), typeof(List<string>) });
        }
        else
        {
            mtd_destroy = AccessTools.Method(typeof(GameUtils), nameof(GameUtils.DestroyAllChildrenImmediatelyBut), new[] { typeof(Transform), typeof(List<string>) });
        }

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

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction))]
    [HarmonyPostfix]
    private static void Postfix_ExecuteAction_ItemActionRanged(ItemActionRanged __instance, ItemActionData _actionData)
    {
        if (_actionData is ItemActionRanged.ItemActionDataRanged rangedData)
        {
            int burstCount = __instance.GetBurstCount(_actionData);
            _actionData.invData.holdingEntity.emodel.avatarController._setBool("TriggerPulled", rangedData.bPressed && rangedData.state > ItemActionFiringState.Off && rangedData.curBurstCount < burstCount, true);
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

    //[HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._setTrigger))]
    //[HarmonyPostfix]
    //private static void Postfix_AvatarLocalPlayerController_SetTrigger(int _pid, AvatarLocalPlayerController __instance)
    //{
    //    AnimationRiggingManager.SetTrigger(_pid, __instance.entity as EntityPlayer);
    //}

    //[HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._resetTrigger))]
    //[HarmonyPostfix]
    //private static void Postfix_AvatarLocalPlayerController_ResetTrigger(int _pid, AvatarLocalPlayerController __instance)
    //{
    //    AnimationRiggingManager.ResetTrigger(_pid, __instance.entity as EntityPlayer);
    //}

    //[HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._setFloat))]
    //[HarmonyPostfix]
    //private static void Postfix_AvatarLocalPlayerController_SetFloat(int _pid, float _value, AvatarLocalPlayerController __instance)
    //{
    //    AnimationRiggingManager.SetFloat(_pid, _value, __instance.entity as EntityPlayer);
    //}

    //[HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._setBool))]
    //[HarmonyPostfix]
    //private static void Postfix_AvatarLocalPlayerController_SetBool(int _pid, bool _value, AvatarLocalPlayerController __instance)
    //{
    //    AnimationRiggingManager.SetBool(_pid, _value, __instance.entity as EntityPlayer);
    //}

    //[HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._setInt))]
    //[HarmonyPostfix]
    //private static void Postfix_AvatarLocalPlayerController_SetInt(int _pid, int _value, AvatarLocalPlayerController __instance)
    //{
    //    AnimationRiggingManager.SetInt(_pid, _value, __instance.entity as EntityPlayer);
    //}

    //[HarmonyPatch(typeof(LegacyAvatarController), nameof(LegacyAvatarController._setTrigger))]
    //[HarmonyPostfix]
    //private static void Postfix_LegacyAvatarController_SetTrigger(int _propertyHash, LegacyAvatarController __instance)
    //{
    //    AnimationRiggingManager.SetTrigger(_propertyHash, __instance.entity as EntityPlayer);
    //}

    //[HarmonyPatch(typeof(LegacyAvatarController), nameof(LegacyAvatarController._resetTrigger))]
    //[HarmonyPostfix]
    //private static void Postfix_LegacyAvatarController_ResetTrigger(int _propertyHash, LegacyAvatarController __instance)
    //{
    //    AnimationRiggingManager.ResetTrigger(_propertyHash, __instance.entity as EntityPlayer);
    //}

    //[HarmonyPatch(typeof(LegacyAvatarController), nameof(LegacyAvatarController._setFloat))]
    //[HarmonyPostfix]
    //private static void Postfix_LegacyAvatarController_SetFloat(int _propertyHash, float _value, LegacyAvatarController __instance)
    //{
    //    AnimationRiggingManager.SetFloat(_propertyHash, _value, __instance.entity as EntityPlayer);
    //}

    //[HarmonyPatch(typeof(LegacyAvatarController), nameof(LegacyAvatarController._setBool))]
    //[HarmonyPostfix]
    //private static void Postfix_LegacyAvatarController_SetBool(int _propertyHash, bool _value, LegacyAvatarController __instance)
    //{
    //    AnimationRiggingManager.SetBool(_propertyHash, _value, __instance.entity as EntityPlayer);
    //}

    //[HarmonyPatch(typeof(LegacyAvatarController), nameof(LegacyAvatarController._setInt))]
    //[HarmonyPostfix]
    //private static void Postfix_LegacyAvatarController_SetInt(int _propertyHash, int _value, LegacyAvatarController __instance)
    //{
    //    AnimationRiggingManager.SetInt(_propertyHash, _value, __instance.entity as EntityPlayer);
    //}

    [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController._resetTrigger), typeof(int), typeof(bool))]
    [HarmonyReversePatch(HarmonyReversePatchType.Original)]
    public static void VanillaResetTrigger(AvatarLocalPlayerController __instance, int _pid, bool _netsync = true)
    {

    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController.TryGetTrigger), new[] { typeof(int), typeof(bool) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_TryGetTrigger_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController.TryGetBool), new[] { typeof(int), typeof(bool) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_TryGetBool_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController.TryGetInt), new[] { typeof(int), typeof(int) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_TryGetInt_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetInteger), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedInt)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController.TryGetFloat), new[] { typeof(int), typeof(float) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_TryGetFloat_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetFloat), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedFloat)));
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.TryGetTrigger), new[] { typeof(int), typeof(bool) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_TryGetTrigger_AvatarMultiBodyController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)));
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.TryGetBool), new[] { typeof(int), typeof(bool) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_TryGetBool_AvatarMultiBodyController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)));
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.TryGetInt), new[] { typeof(int), typeof(int) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_TryGetInt_AvatarMultiBodyController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetInteger), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedInt)));
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.TryGetFloat), new[] { typeof(int), typeof(float) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_TryGetFloat_AvatarMultiBodyController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetFloat), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedFloat)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController._setBool), new[] { typeof(int), typeof(bool), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setBool_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetBool), new[] { typeof(int), typeof(bool) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedBool)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController._setTrigger), new[] { typeof(int), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setTrigger_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetTrigger), new[] { typeof(int)}),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedTrigger)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController._resetTrigger), new[] { typeof(int), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_resetTrigger_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.ResetTrigger), new[] { typeof(int) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.ResetWrappedTrigger)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController._setInt), new[] { typeof(int), typeof(int), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setInt_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetInteger), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedInt)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetInteger), new[] { typeof(int), typeof(int) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedInt)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController._setFloat), new[] { typeof(int), typeof(float), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setFloat_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetFloat), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedFloat)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetFloat), new[] { typeof(int), typeof(float) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedFloat)));
    }

    [HarmonyPatch(typeof(AvatarCharacterController), nameof(AvatarCharacterController._setBool), new[] { typeof(int), typeof(bool), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setBool_AvatarCharacterController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetBool), new[] { typeof(int), typeof(bool) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedBool)));
    }

    [HarmonyPatch(typeof(AvatarCharacterController), nameof(AvatarCharacterController._setTrigger), new[] { typeof(int), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setTrigger_AvatarCharacterController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetTrigger), new[] { typeof(int)}),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedTrigger)));
    }

    [HarmonyPatch(typeof(AvatarCharacterController), nameof(AvatarCharacterController._resetTrigger), new[] { typeof(int), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_resetTrigger_AvatarCharacterController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.ResetTrigger), new[] { typeof(int) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.ResetWrappedTrigger)));
    }

    [HarmonyPatch(typeof(AvatarCharacterController), nameof(AvatarCharacterController._setInt), new[] { typeof(int), typeof(int), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setInt_AvatarCharacterController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetInteger), new[] { typeof(int), typeof(int) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedInt)));
    }

    [HarmonyPatch(typeof(AvatarCharacterController), nameof(AvatarCharacterController._setFloat), new[] { typeof(int), typeof(float), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setFloat_AvatarCharacterController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetFloat), new[] { typeof(int), typeof(float) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedFloat)));
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController._setBool), new[] { typeof(int), typeof(bool), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setBool_AvatarMultiBodyController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetBool), new[] { typeof(int), typeof(bool) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedBool)));
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController._setTrigger), new[] { typeof(int), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setTrigger_AvatarMultiBodyController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetTrigger), new[] { typeof(int) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedTrigger)));
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController._resetTrigger), new[] { typeof(int), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_resetTrigger_AvatarMultiBodyController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.ResetTrigger), new[] { typeof(int) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.ResetWrappedTrigger)));
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController._setInt), new[] { typeof(int), typeof(int), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setInt_AvatarMultiBodyController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetInteger), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedInt)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetInteger), new[] { typeof(int), typeof(int) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedInt)));
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController._setFloat), new[] { typeof(int), typeof(float), typeof(bool) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setFloat_AvatarMultiBodyController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetFloat), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedFloat)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetFloat), new[] { typeof(int), typeof(float) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedFloat)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController.GetParameterName))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_GetParameterName_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.PropertyGetter(typeof(Animator), nameof(Animator.parameters)),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedParameters)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController.SyncAnimParameters))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_SyncAnimParameters_AvatarController(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.MethodReplacer(
                                    AccessTools.PropertyGetter(typeof(Animator), nameof(Animator.parameters)), 
                                    AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedParameters)))
                                .MethodReplacer(
                                    AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) }),
                                    AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)))
                                .MethodReplacer(
                                    AccessTools.Method(typeof(Animator), nameof(Animator.GetInteger), new[] { typeof(int) }), 
                                    AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedInt)))
                                .MethodReplacer(
                                    AccessTools.Method(typeof(Animator), nameof(Animator.GetFloat), new[] { typeof(int) }), 
                                    AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedFloat)))
                                .ToList();

        //var lbd_wrapper = generator.DeclareLocal(typeof(IAnimatorWrapper));

        //var fld_anim = AccessTools.Field(typeof(AvatarController), nameof(AvatarController.anim));

        //for (int i = 1; i < codes.Count; i++)
        //{
        //    if (codes[i].opcode == OpCodes.Stloc_0)
        //    {
        //        codes.InsertRange(i + 1, new[]
        //        {
        //            new CodeInstruction(OpCodes.Ldarg_0),
        //            CodeInstruction.LoadField(typeof(AvatarController), nameof(AvatarController.anim)),
        //            CodeInstruction.Call(typeof(KFExtensions), nameof(KFExtensions.GetAnimatorWrapper)),
        //            new CodeInstruction(OpCodes.Stloc_S, lbd_wrapper)
        //        });
        //        i += 4;
        //    }
        //    else if (codes[i].opcode == OpCodes.Ldloc_3 && codes[i - 1].LoadsField(fld_anim))
        //    {
        //        codes.Insert(i - 2, new CodeInstruction(OpCodes.Ldloc_S, lbd_wrapper).WithLabels(codes[i - 2].ExtractLabels()));
        //        codes.RemoveRange(i - 1, 2);
        //        i--;
        //    }
        //}

        return codes;
    }

    [HarmonyPatch(typeof(AvatarSDCSController), nameof(AvatarSDCSController.LateUpdate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_LateUpdate_AvatarSDCSController(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var mtd_getbool = AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(int) });
        var mtd_getint = AccessTools.Method(typeof(Animator), nameof(Animator.GetInteger), new[] { typeof(int) });
        var mtd_getfloat = AccessTools.Method(typeof(Animator), nameof(Animator.GetFloat), new[] { typeof(int) });
        var mtd_istransition = AccessTools.Method(typeof(Animator),nameof(Animator.IsInTransition), new[] { typeof(int) });
        var mtd_updatespine = AccessTools.Method(typeof(LegacyAvatarController), nameof(LegacyAvatarController.updateSpineRotation));
        var fld_reload = AccessTools.Field(typeof(AvatarController), nameof(AvatarController.reloadHash));
        var mtd_getvanillabool = AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool));
        var mtd_getvanillaint = AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedInt));
        var mtd_getvanillafloat = AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedFloat));
        var mtd_isvanillatransition = AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.IsVanillaInTransition));
        var codes = instructions.Manipulator(ins => ins.opcode == OpCodes.Ldstr, ins =>
        {
            switch (ins.operand)
            {
                case "Reload":
                    ins.opcode = OpCodes.Ldsfld;
                    ins.operand = fld_reload;
                    break;
            }
        }).MethodReplacer(AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(string) }), mtd_getbool)
          .MethodReplacer(AccessTools.Method(typeof(Animator), nameof(Animator.GetInteger), new[] { typeof(string) }), mtd_getint)
          .MethodReplacer(AccessTools.Method(typeof(Animator), nameof(Animator.GetFloat), new[] { typeof(string) }), mtd_getfloat)
          .MethodReplacer(AccessTools.Method(typeof(Animator), nameof(Animator.SetBool), new[] { typeof(string), typeof(bool) }), AccessTools.Method(typeof(Animator), nameof(Animator.SetBool), new[] { typeof(int), typeof(bool) }))
          .MethodReplacer(mtd_getbool, mtd_getvanillabool)
          .MethodReplacer(mtd_getint, mtd_getvanillaint)
          .MethodReplacer(mtd_getfloat, mtd_getvanillafloat)
          .MethodReplacer(mtd_istransition, mtd_isvanillatransition)
          .ToList();

        //var lbd_wrapper = generator.DeclareLocal(typeof(IAnimatorWrapper));

        //for (var i = 0; i < codes.Count; i++)
        //{
        //    if (codes[i].Calls(mtd_updatespine))
        //    {
        //        codes.InsertRange(i + 1, new[]
        //        {
        //            new CodeInstruction(OpCodes.Ldarg_0),
        //            CodeInstruction.LoadField(typeof(AvatarController), nameof(AvatarController.anim)),
        //            CodeInstruction.Call(typeof(KFExtensions), nameof(KFExtensions.GetItemAnimatorWrapper)),
        //            new CodeInstruction(OpCodes.Stloc_S, lbd_wrapper)
        //        });
        //        i += 4;
        //    }
        //    else if (codes[i].Calls(mtd_getvanillabool) || codes[i].Calls(mtd_getvanillafloat) || codes[i].Calls(mtd_getvanillaint) || codes[i].Calls(mtd_isvanillatransition))
        //    {
        //        codes.Insert(i - 3, new CodeInstruction(OpCodes.Ldloc_S, lbd_wrapper).WithLabels(codes[i - 3].ExtractLabels()));
        //        codes.RemoveRange(i - 2, 2);
        //        i--;
        //    }
        //}
        //foreach (var code in codes)
        //{
        //    Log.Out(code.ToString());
        //}
        return codes;
    }

    [HarmonyPatch(typeof(AvatarSDCSController), nameof(AvatarSDCSController.updateLayerStateInfo))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_updateLayerStateInfo_AvatarSDCSController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetCurrentAnimatorStateInfo)), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetCurrentVanillaStateInfo)));
    }

    [HarmonyPatch(typeof(AvatarUMAController), nameof(AvatarUMAController.updateLayerStateInfo))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_updateLayerStateInfo_AvatarUMAController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetCurrentAnimatorStateInfo)), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetCurrentVanillaStateInfo)));
    }

    [HarmonyPatch(typeof(LegacyAvatarController), nameof(LegacyAvatarController.updateLayerStateInfo))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_updateLayerStateInfo_LegacyAvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetCurrentAnimatorStateInfo)), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetCurrentVanillaStateInfo)));
    }

    [HarmonyPatch(typeof(AvatarSDCSController), nameof(AvatarSDCSController.setLayerWeights))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setLayerWeights_AvatarSDCSController(IEnumerable<CodeInstruction> instructions)
    {
        int id = Animator.StringToHash("MinibikeIdle");
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetLayerWeight)),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetVanillaLayerWeight)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.IsInTransition)),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.IsVanillaInTransition)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetBool), new[] { typeof(string) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedBool)))
                           .Manipulator(ins => ins.opcode == OpCodes.Ldstr, ins => { ins.opcode = OpCodes.Ldc_I4; ins.operand = id; });
    }

    [HarmonyPatch(typeof(LegacyAvatarController), nameof(LegacyAvatarController.setLayerWeights))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setLayerWeights_LegacyAvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetLayerWeight)), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetVanillaLayerWeight)));
    }

    [HarmonyPatch(typeof(AvatarUMAController), nameof(AvatarUMAController.setLayerWeights))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_setLayerWeights_AvatarUMAController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetLayerWeight)), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetVanillaLayerWeight)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetInteger), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedInt)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.IsInTransition)), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.IsVanillaInTransition)));
    }

    [HarmonyPatch(typeof(UMACharacterBodyAnimator), nameof(UMACharacterBodyAnimator.assignLayerWeights))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_assignLayerWeights_UMACharacterBodyAnimator(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetLayerWeight)), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetVanillaLayerWeight)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.GetInteger), new[] { typeof(int) }), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.GetWrappedInt)))
                           .MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.IsInTransition)), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.IsVanillaInTransition)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Update_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetLayerWeight)), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetVanillaLayerWeight)));
    }

    [HarmonyPatch(typeof(AvatarController), nameof(AvatarController.InitHitDuration))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_InitHitDuration_AvatarController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetLayerWeight)), 
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetVanillaLayerWeight)));
    }

    private static int drunkHash = Animator.StringToHash("drunk");
    [HarmonyPatch(typeof(FirstPersonAnimator), nameof(FirstPersonAnimator.SetDrunk))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_SetDrunk_FirstPersonAnimator(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetFloat), new[] { typeof(string), typeof(float) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedFloat)))
                           .Manipulator(ins => ins.LoadsConstant("drunk"),
                            ins => 
                            {
                                ins.opcode = OpCodes.Ldsfld;
                                ins.operand = AccessTools.Field(typeof(AnimationRiggingPatches), nameof(AnimationRiggingPatches.drunkHash));
                            });
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.SetVehicleAnimation))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_SetVehicleAnimation_AvatarMultiBodyController(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
                AccessTools.Method(typeof(Animator), nameof(Animator.SetInteger), new[] { typeof(string), typeof(int) }),
                AccessTools.Method(typeof(KFExtensions), nameof(KFExtensions.SetWrappedInt)));
    }
    //BodyAnimator.LateUpdate
    //UMACharacterBodyAnimator.LateUpdate
    //BodyAnimator.cacheLayerStateInfo
    //UMACharacterBodyAnimator.cacheLayerStateInfo
    //not used
}
