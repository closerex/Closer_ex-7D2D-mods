﻿using HarmonyLib;
using KFCommonUtilityLib.Scripts.StaticManagers;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using UnityEngine;

[HarmonyPatch]
public static class CommonUtilityPatch
{
    //fix reloading issue and onSelfRangedBurstShot timing
    public static void FakeReload(EntityAlive holdingEntity, ItemActionRanged.ItemActionDataRanged _actionData)
    {
        if (!holdingEntity)
            return;
        _actionData.isReloading = true;
        _actionData.isWeaponReloading = true;
        holdingEntity.MinEventContext.ItemActionData = _actionData;
        holdingEntity.FireEvent(MinEventTypes.onReloadStart, true);
        _actionData.isReloading = false;
        _actionData.isWeaponReloading = false;
        _actionData.isReloadCancelled = false;
        _actionData.isWeaponReloadCancelled = false;
        holdingEntity.FireEvent(MinEventTypes.onReloadStop);

        if (holdingEntity is EntityPlayerLocal && AnimationRiggingManager.FpvTransformReference != null)
        {
            AnimationAmmoUpdateState.SetAmmoCountForEntity(holdingEntity, holdingEntity.inventory.holdingItemIdx);
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.SwapAmmoType))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_SwapAmmoType_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0; i < codes.Count; ++i)
        {
            if (codes[i].opcode == OpCodes.Ret)
            {
                codes.InsertRange(i, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(FakeReload))
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var mtd_fire_event = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.FireEvent));
        var mtd_get_model_layer = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.GetModelLayer));
        var mtd_get_perc_left = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.PercentUsesLeft));

        int take = -1, insert = -1;
        for (int i = 0; i < codes.Count; ++i)
        {
            if (codes[i].opcode == OpCodes.Ldc_I4_S && codes[i].OperandIs((int)MinEventTypes.onSelfRangedBurstShotStart) && codes[i + 2].Calls(mtd_fire_event))
                take = i - 3;
            else if (codes[i].Calls(mtd_get_model_layer))
                insert = i + 2;
        }

        if (take < insert)
        {
            var list = codes.GetRange(take, 6);
            codes.InsertRange(insert, list);
            codes.RemoveRange(take, 6);
        }

        return codes;
    }
    //fix recoil animation does not match weapon RPM
    private static int weaponFireHash = Animator.StringToHash("WeaponFire");
    private static int aimHash = Animator.StringToHash("IsAiming");
    private static HashSet<int> hash_shot_state = new HashSet<int>();
    private static HashSet<int> hash_aimshot_state = new HashSet<int>();

    public static void InitShotStates()
    {
        string[] weapons =
        {
            "fpvAK47",
            "fpvMagnum",
            "fpvRocketLauncher",
            "fpvSawedOffShotgun",
            "fpvBlunderbuss",
            "fpvCrossbow",
            "fpvPistol",
            "fpvHuntingRifle",
            "fpvSMG",
            "fpvSniperRifle",
            "M60",
            "fpvDoubleBarrelShotgun",
            //"fpvJunkTurret",
            "fpvTacticalAssaultRifle",
            "fpvDesertEagle",
            "fpvAutoShotgun",
            "fpvSharpShooterRifle",
            "fpvPipeMachineGun",
            "fpvPipeRifle",
            "fpvPipeRevolver",
            "fpvPipeShotgun",
            "fpvLeverActionRifle",
        };
        foreach (string weapon in weapons)
        {
            hash_shot_state.Add(Animator.StringToHash(weapon + "Fire"));
            hash_aimshot_state.Add(Animator.StringToHash(weapon + "AimFire"));
        }
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.OnFired))]
    [HarmonyPostfix]
    private static void Postfix_OnFired_EntityPlayerLocal(EntityPlayerLocal __instance)
    {
        if (!__instance.bFirstPersonView)
            return;

        ItemActionRanged.ItemActionDataRanged _rangedData;
        if ((_rangedData = __instance.inventory.holdingItemData.actionData[0] as ItemActionRanged.ItemActionDataRanged) == null && (_rangedData = __instance.inventory.holdingItemData.actionData[1] as ItemActionRanged.ItemActionDataRanged) == null)
            return;

        var anim = (__instance.emodel.avatarController as AvatarLocalPlayerController).FPSArms.Animator;
        if (anim.IsInTransition(0))
            return;

        var curState = anim.GetCurrentAnimatorStateInfo(0);
        if (curState.length > _rangedData.Delay)
        {
            bool aimState = anim.GetBool(aimHash);
            short shotState = 0;
            if (hash_shot_state.Contains(curState.shortNameHash))
                shotState = 1;
            else if (hash_aimshot_state.Contains(curState.shortNameHash))
                shotState = 2;
            if (shotState == 0 || (shotState == 1 && aimState) || (shotState == 2 && !aimState))
            {
                if (shotState > 0)
                    anim.ResetTrigger(weaponFireHash);
                return;
            }

            //current state, layer 0, offset 0
            anim.PlayInFixedTime(0, 0, 0);
            anim.ResetTrigger(weaponFireHash);
            if (_rangedData.invData.itemValue.Meta == 0)
            {
                __instance.emodel.avatarController.CancelEvent(weaponFireHash);
                Log.Out("Cancel fire event because meta is 0");
            }
        }
    }

    //[HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
    //[HarmonyPostfix]
    //private static void Postfix_ItemActionEffects_ItemActionRanged(ItemActionData _actionData, int _firingState)
    //{
    //    if (_firingState == 0 && _actionData.invData.holdingEntity is EntityPlayerLocal && !(_actionData.invData.itemValue.ItemClass.Actions[0] is ItemActionCatapult))
    //    {
    //        _actionData.invData.holdingEntity?.emodel.avatarController.CancelEvent(weaponFireHash);
    //        //Log.Out("Cancel fire event because firing state is 0\n" + StackTraceUtility.ExtractStackTrace());
    //    }
    //}

    //[HarmonyPatch(typeof(GameManager), "gmUpdate")]
    //[HarmonyTranspiler]
    //private static IEnumerable<CodeInstruction> Transpiler_gmUpdate_GameManager(IEnumerable<CodeInstruction> instructions)
    //{
    //    var codes = new List<CodeInstruction>(instructions);
    //    var mtd_unload = AccessTools.Method(typeof(Resources), nameof(Resources.UnloadUnusedAssets));
    //    var fld_duration = AccessTools.Field(typeof(GameManager), "unloadAssetsDuration");

    //    for (int i = 0; i < codes.Count; ++i)
    //    {
    //        if (codes[i].opcode == OpCodes.Call && codes[i].Calls(mtd_unload))
    //        {
    //            for (int j = i; j >= 0; --j)
    //            {
    //                if (codes[j].opcode == OpCodes.Ldfld && codes[j].LoadsField(fld_duration) && codes[j + 1].opcode == OpCodes.Ldc_R4)
    //                    codes[j + 1].operand = (float)codes[j + 1].operand / 2;
    //            }
    //            break;
    //        }
    //    }

    //    return codes;
    //}

    //internal static void ForceUpdateGC()
    //{
    //    if (GameManager.IsDedicatedServer)
    //        return;
    //    if (GameManager.frameCount % 18000 == 0)
    //    {
    //        long rss = GetRSS.GetCurrentRSS();
    //        if (rss / 1024 / 1024 > 6144)
    //        {
    //            Log.Out("Memory usage exceeds threshold, now performing garbage collection...");
    //            GC.Collect();
    //        }
    //    }
    //}

    //altmode workarounds
    //deprecated by action module
    private static void ParseAltRequirements(XElement _node)
    {
        string itemName = _node.GetAttribute("name");
        if (string.IsNullOrEmpty(itemName))
        {
            return;
        }
        ItemClass item = ItemClass.GetItemClass(itemName);
        for (int i = 0; i < item.Actions.Length; i++)
        {
            if (item.Actions[i] is ItemActionAltMode _alt)
                _alt.ParseAltRequirements(_node, i);
        }
    }

    [HarmonyPatch(typeof(ItemClassesFromXml), nameof(ItemClassesFromXml.parseItem))]
    [HarmonyPostfix]
    private static void Postfix_parseItem_ItemClassesFromXml(XElement _node)
    {
        ParseAltRequirements(_node);
    }

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.ExecuteAction))]
    [HarmonyPrefix]
    private static bool Prefix_ExecuteAction_ItemClass(ItemClass __instance, int _actionIdx, ItemInventoryData _data, bool _bReleased)
    {
        if (!_bReleased && __instance.Actions[_actionIdx] is ItemActionAltMode _alt)
            _alt.SetAltRequirement(_data.actionData[_actionIdx]);

        return true;
    }

    [HarmonyPatch(typeof(DynamicProperties), nameof(DynamicProperties.Parse))]
    [HarmonyPrefix]
    private static bool Prefix_Parse_DynamicProperties(XElement elementProperty)
    {
        if (elementProperty.Name.LocalName != "property")
            return false;
        return true;
    }

    //MinEventParams workarounds
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.fireShot))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_fireShot_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
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

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.OnHoldingUpdate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_OnHoldingUpdate_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var mtd_release = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.triggerReleased));
        var codes = instructions.ToList();

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_release))
            {
                codes[i + 1].labels.Clear();
                codes[i + 1].MoveLabelsFrom(codes[i - 20]);
                codes.RemoveRange(i - 20, 21);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.triggerReleased))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_triggerReleased_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var mtd_effect = AccessTools.Method(typeof(IGameManager), nameof(IGameManager.ItemActionEffectsServer));
        var mtd_data = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.getUserData));
        var codes = instructions.ToList();

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_effect))
            {
                codes.InsertRange(i, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Callvirt, mtd_data)
                });
                codes.RemoveAt(i - 1);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
    [HarmonyPrefix]
    private static bool Prefix_StartGame_GameManager()
    {
        CustomEffectEnumManager.InitFinal();
        return true;
    }

    [HarmonyPatch(typeof(PassiveEffect), nameof(PassiveEffect.ParsePassiveEffect))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ParsePassiveEffect_PassiveEffect(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        MethodInfo mtd_enum_parse = AccessTools.Method(typeof(EnumUtils), nameof(EnumUtils.Parse), new[] { typeof(string), typeof(bool) }, new[] { typeof(PassiveEffects) });

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_enum_parse))
            {
                codes.Insert(i + 1, CodeInstruction.Call(typeof(CustomEffectEnumManager), nameof(CustomEffectEnumManager.RegisterOrGetEnum), new[] { typeof(string), typeof(bool) }, new[] { typeof(PassiveEffects) }));
                codes.RemoveAt(i);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(MinEventActionBase), nameof(MinEventActionBase.ParseXmlAttribute))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ParseXmlAttribute_MinEventActionBase(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        MethodInfo mtd_enum_parse = AccessTools.Method(typeof(EnumUtils), nameof(EnumUtils.Parse), new[] { typeof(string), typeof(bool) }, new[] { typeof(MinEventTypes) });

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_enum_parse))
            {
                codes.Insert(i + 1, CodeInstruction.Call(typeof(CustomEffectEnumManager), nameof(CustomEffectEnumManager.RegisterOrGetEnum), new[] { typeof(string), typeof(bool) }, new[] { typeof(MinEventTypes) }));
                codes.RemoveAt(i);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.OnHoldingUpdate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_OnHoldingUpdate_ItemActionDynamicMelee(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Is(OpCodes.Ldc_R4, 0.1f))
            {
                codes.RemoveRange(i, 2);
                break;
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.canStartAttack))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_canStartAttack_ItemActionDynamicMelee(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Is(OpCodes.Ldc_R4, 0.1f))
            {
                codes.RemoveRange(i, 2);
                break;
            }
        }
        return codes;
    }

    /// <summary>
    /// projectile direct hit damage percent
    /// removed due to new explosion damage passives
    /// </summary>
    /// <param name="instructions"></param>
    /// <returns></returns>
    //[HarmonyPatch(typeof(ProjectileMoveScript), nameof(ProjectileMoveScript.checkCollision))]
    //[HarmonyTranspiler]
    //private static IEnumerable<CodeInstruction> Transpiler_checkCollision_ProjectileMoveScript(IEnumerable<CodeInstruction> instructions)
    //{
    //    var codes = instructions.ToList();
    //    var fld_strain = AccessTools.Field(typeof(ItemActionLauncher.ItemActionDataLauncher), nameof(ItemActionLauncher.ItemActionDataLauncher.strainPercent));
    //    var mtd_block = AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetDamageBlock));

    //    for (int i = 0; i < codes.Count; i++)
    //    {
    //        if (codes[i].LoadsField(fld_strain))
    //        {
    //            codes.InsertRange(i + 1, new CodeInstruction[]
    //            {
    //                new CodeInstruction(OpCodes.Ldarg_0),
    //                CodeInstruction.LoadField(typeof(ProjectileMoveScript), nameof(ProjectileMoveScript.itemValueProjectile)),
    //                new CodeInstruction(OpCodes.Ldloc_S, 4),
    //                CodeInstruction.Call(typeof(CommonUtilityPatch), codes[i - 3].Calls(mtd_block) ? nameof(GetProjectileBlockDamagePerc) : nameof(GetProjectileEntityDamagePerc)),
    //                new CodeInstruction(OpCodes.Mul)
    //            });
    //        }
    //    }

    //    return codes;
    //}

    //public static float GetProjectileBlockDamagePerc(ItemValue _itemValue, EntityAlive _holdingEntity)
    //{
    //    return EffectManager.GetValue(CustomEnums.ProjectileImpactDamagePercentBlock, _itemValue, 1, _holdingEntity, null);
    //}

    //public static float GetProjectileEntityDamagePerc(ItemValue _itemValue, EntityAlive _holdingEntity)
    //{
    //    return EffectManager.GetValue(CustomEnums.ProjectileImpactDamagePercentEntity, _itemValue, 1, _holdingEntity, null);
    //}

    /// <summary>
    /// force tpv crosshair
    /// </summary>
    /// <param name="instructions"></param>
    /// <returns></returns>
    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.guiDrawCrosshair))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_guiDrawCrosshair_EntityPlayerLocal(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        FieldInfo fld_debug = AccessTools.Field(typeof(ItemAction), nameof(ItemAction.ShowDistanceDebugInfo));

        for ( int i = 0; i < codes.Count; i++ )
        {
            if (codes[i].LoadsField(fld_debug))
            {
                var label = codes[i - 1].operand;
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.bFirstPersonView)),
                    new CodeInstruction(OpCodes.Brfalse_S, label)
                });
                break;
            }
        }

        return codes;
    }

    /// <summary>
    /// correctly apply muzzle flash silence with modifications
    /// </summary>
    /// <param name="_data"></param>
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.OnModificationsChanged))]
    [HarmonyPostfix]
    private static void Postfix_OnModificationsChanged_ItemActionRanged(ItemActionData _data)
    {
        ItemActionRanged.ItemActionDataRanged itemActionDataRanged = _data as ItemActionRanged.ItemActionDataRanged;
        if (itemActionDataRanged.SoundStart.Contains("silenced"))
        {
            itemActionDataRanged.IsFlashSuppressed = true;
        }

        //should fix stuck on switching item?
        itemActionDataRanged.isReloadCancelled = false;
        itemActionDataRanged.isWeaponReloadCancelled = false;
        itemActionDataRanged.isReloading = false;
        itemActionDataRanged.isWeaponReloading = false;
        itemActionDataRanged.isChangingAmmoType = false;
    }

    #region item tags modifier

    /// <summary>
    /// should handle swapping mod
    /// first check if the mod to install can be installed after current mod is removed
    /// then check if any other mod requires or conflicts current mod
    /// </summary>
    /// <param name="instructions"></param>
    /// <param name="generator"></param>
    /// <returns></returns>
    [HarmonyPatch(typeof(XUiC_ItemPartStack), nameof(XUiC_ItemPartStack.CanSwap))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_CanSwap_XUiC_ItemPartStack(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        LocalBuilder lbd_tags_if_remove_prev = generator.DeclareLocal(typeof(FastTags<TagGroup.Global>));
        LocalBuilder lbd_tags_if_install_new = generator.DeclareLocal(typeof(FastTags<TagGroup.Global>));
        MethodInfo mtd_get_item_class = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.ItemClass));
        MethodInfo mtd_has_any_tags = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.HasAnyTags));
        MethodInfo mtd_test_any_set = AccessTools.Method(typeof(FastTags<TagGroup.Global>), nameof(FastTags<TagGroup.Global>.Test_AnySet));
        FieldInfo fld_mod = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Modifications));
        FieldInfo fld_installable_tags = AccessTools.Field(typeof(ItemClassModifier), nameof(ItemClassModifier.InstallableTags));

        for (int i = 3; i < codes.Count; i++)
        {
            //get current tags
            if (codes[i].opcode == OpCodes.Stloc_2)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(XUiC_ItemPartStack), "itemValue"),
                    CodeInstruction.Call(typeof(LocalItemTagsManager), nameof(LocalItemTagsManager.GetTagsAsIfNotInstalled)),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_tags_if_remove_prev),
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.Call(typeof(LocalItemTagsManager), nameof(LocalItemTagsManager.GetTagsAsIfInstalled)),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_tags_if_install_new)
                });
                i += 10;
                Log.Out("mod 1!!!");
            }
            //replace checking tags
            else if (codes[i].Calls(mtd_has_any_tags) && codes[i - 3].opcode == OpCodes.Ldloc_2)
            {
                if (codes[i - 1].LoadsField(fld_installable_tags) && (codes[i + 1].opcode == OpCodes.Brtrue || codes[i + 1].opcode == OpCodes.Brtrue_S))
                {
                    var lbl_prev = codes[i + 4].ExtractLabels();
                    var lbl_jump = generator.DefineLabel();
                    codes[i + 4].WithLabels(lbl_jump);
                    codes.InsertRange(i + 4, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_1).WithLabels(lbl_prev),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(XUiC_ItemPartStack), "itemValue"),
                        new CodeInstruction(OpCodes.Ldloc_0),
                        CodeInstruction.Call(typeof(LocalItemTagsManager), nameof(LocalItemTagsManager.CanSwapMod)),
                        new CodeInstruction(OpCodes.Brtrue, lbl_jump),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Ret)
                    });
                }
                codes[i - 3].opcode = OpCodes.Ldloca_S;
                codes[i - 3].operand = lbd_tags_if_remove_prev;
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = mtd_test_any_set;
                Log.Out("mod 2!!!");
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(XUiC_ItemCosmeticStack), nameof(XUiC_ItemCosmeticStack.CanSwap))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_CanSwap_XUiC_ItemCosmeticStack(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        LocalBuilder lbd_tags_if_remove_prev = generator.DeclareLocal(typeof(FastTags<TagGroup.Global>));
        LocalBuilder lbd_tags_if_install_new = generator.DeclareLocal(typeof(FastTags<TagGroup.Global>));
        LocalBuilder lbd_item_being_assembled = generator.DeclareLocal(typeof(ItemValue));
        MethodInfo mtd_get_item_class = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.ItemClass));
        MethodInfo mtd_has_any_tags = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.HasAnyTags));
        MethodInfo mtd_test_any_set = AccessTools.Method(typeof(FastTags<TagGroup.Global>), nameof(FastTags<TagGroup.Global>.Test_AnySet));
        MethodInfo mtd_get_xui = AccessTools.PropertyGetter(typeof(XUiController), nameof(XUiController.xui));
        MethodInfo mtd_get_cur_item = AccessTools.PropertyGetter(typeof(XUiM_AssembleItem), nameof(XUiM_AssembleItem.CurrentItem));
        FieldInfo fld_cos = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.CosmeticMods));
        FieldInfo fld_installable_tags = AccessTools.Field(typeof(ItemClassModifier), nameof(ItemClassModifier.InstallableTags));

        for (int i = 3; i < codes.Count; i++)
        {
            //get current tags
            if ((codes[i].opcode == OpCodes.Brtrue || codes[i].opcode == OpCodes.Brtrue_S) && codes[i - 1].opcode == OpCodes.Ldloc_0)
            {
                codes.InsertRange(i + 3, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[i + 3]),
                    new CodeInstruction(OpCodes.Call, mtd_get_xui),
                    CodeInstruction.LoadField(typeof(XUi), nameof(XUi.AssembleItem)),
                    new CodeInstruction(OpCodes.Callvirt, mtd_get_cur_item),
                    CodeInstruction.LoadField(typeof(ItemStack), nameof(ItemStack.itemValue)),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_item_being_assembled),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_item_being_assembled),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(XUiC_ItemCosmeticStack), "itemValue"),
                    CodeInstruction.Call(typeof(LocalItemTagsManager), nameof(LocalItemTagsManager.GetTagsAsIfNotInstalled)),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_tags_if_remove_prev),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_item_being_assembled),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.Call(typeof(LocalItemTagsManager), nameof(LocalItemTagsManager.GetTagsAsIfInstalled)),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_tags_if_install_new)
                });
                i += 18;
                Log.Out("cos 1!!!");
            }
            //replace checking tags
            else if (codes[i].Calls(mtd_has_any_tags) && codes[i - 3].Calls(mtd_get_item_class))
            {
                if (codes[i - 1].LoadsField(fld_installable_tags) && (codes[i + 1].opcode == OpCodes.Brtrue || codes[i + 1].opcode == OpCodes.Brtrue_S))
                {
                    var lbl_prev = codes[i + 4].ExtractLabels();
                    var lbl_jump = generator.DefineLabel();
                    codes[i + 4].WithLabels(lbl_jump);
                    codes.InsertRange(i + 4, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_item_being_assembled).WithLabels(lbl_prev),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(XUiC_ItemPartStack), "itemValue"),
                        new CodeInstruction(OpCodes.Ldloc_0),
                        CodeInstruction.Call(typeof(LocalItemTagsManager), nameof(LocalItemTagsManager.CanSwapMod)),
                        new CodeInstruction(OpCodes.Brtrue, lbl_jump),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Ret)
                    });
                }
                codes[i - 8].MoveLabelsTo(codes[i - 3]);
                codes[i - 3].opcode = OpCodes.Ldloca_S;
                codes[i - 3].operand = lbd_tags_if_remove_prev;
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = mtd_test_any_set;
                codes.RemoveRange(i - 8, 5);
                i -= 5;
                Log.Out("cos 2!!!");
            }
        }

        return codes;
    }

    /// <summary>
    /// check if other mods relies on this one
    /// </summary>
    /// <param name="__result"></param>
    /// <param name="__instance"></param>
    /// <param name="___itemValue"></param>
    [HarmonyPatch(typeof(XUiC_ItemPartStack), "CanRemove")]
    [HarmonyPostfix]
    private static void Postfix_CanRemove_XUiC_ItemPartStack(ref bool __result, XUiC_ItemPartStack __instance)
    {
        if (__result && __instance.xui?.AssembleItem?.CurrentItem?.itemValue is ItemValue itemValue)
        {
            ItemClass itemClass = itemValue.ItemClass;
            FastTags<TagGroup.Global> tagsAfterRemove = LocalItemTagsManager.GetTagsAsIfNotInstalled(itemValue, __instance.itemValue);
            if (tagsAfterRemove.IsEmpty)
            {
                __result = false;
                return;
            }

            foreach (var mod in itemValue.Modifications)
            {
                if (mod.IsEmpty())
                    continue;
                ItemClassModifier modClass = mod.ItemClass as ItemClassModifier;
                if (modClass == null || !tagsAfterRemove.Test_AnySet(modClass.InstallableTags) || tagsAfterRemove.Test_AnySet(modClass.DisallowedTags))
                {
                    __result = false;
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(XUiC_ItemCosmeticStack), "CanRemove")]
    [HarmonyPostfix]
    private static void Postfix_CanRemove_XUiC_ItemCosmeticStack(ref bool __result, XUiC_ItemCosmeticStack __instance)
    {
        if (__result && __instance.xui?.AssembleItem?.CurrentItem?.itemValue is ItemValue itemValue)
        {
            ItemClass itemClass = itemValue.ItemClass;
            FastTags<TagGroup.Global> tagsAfterRemove = LocalItemTagsManager.GetTagsAsIfNotInstalled(itemValue, __instance.itemValue);
            if (tagsAfterRemove.IsEmpty)
            {
                __result = false;
                return;
            }

            foreach (var mod in itemValue.CosmeticMods)
            {
                if (mod.IsEmpty())
                    continue;
                ItemClassModifier modClass = mod.ItemClass as ItemClassModifier;
                if (modClass == null || !tagsAfterRemove.Test_AnySet(modClass.InstallableTags) || tagsAfterRemove.Test_AnySet(modClass.DisallowedTags))
                {
                    __result = false;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// should update the gear icon?
    /// </summary>
    /// <param name="instructions"></param>
    /// <param name="generator"></param>
    /// <returns></returns>
    [HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.updateLockTypeIcon))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_updateLockTypeIcon_XUiC_ItemStack(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        LocalBuilder lbd_tags = generator.DeclareLocal(typeof(FastTags<TagGroup.Global>));
        MethodInfo mtd_has_any_tags = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.HasAnyTags));
        MethodInfo mtd_test_any_set = AccessTools.Method(typeof(FastTags<TagGroup.Global>), nameof(FastTags<TagGroup.Global>.Test_AnySet));
        MethodInfo mtd_get_item_class = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.ItemClass));
        MethodInfo mtd_get_cur_item = AccessTools.PropertyGetter(typeof(XUiM_AssembleItem), nameof(XUiM_AssembleItem.CurrentItem));
        MethodInfo mtd_get_xui = AccessTools.PropertyGetter(typeof(XUiController), nameof(XUiController.xui));

        for (int i = 3; i < codes.Count; i++)
        {
            //get current tags
            if ((codes[i].opcode == OpCodes.Brfalse_S || codes[i].opcode == OpCodes.Brfalse) && codes[i - 1].Calls(mtd_get_cur_item))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, mtd_get_xui),
                    CodeInstruction.LoadField(typeof(XUi), nameof(XUi.AssembleItem)),
                    new CodeInstruction(OpCodes.Callvirt, mtd_get_cur_item),
                    CodeInstruction.LoadField(typeof(ItemStack), nameof(ItemStack.itemValue)),
                    CodeInstruction.Call(typeof(LocalItemTagsManager), nameof(LocalItemTagsManager.GetTags)),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_tags)
                });
                i += 7;
            }
            //do not touch check on the modification item
            else if (codes[i].Calls(mtd_has_any_tags) && codes[i - 3].Calls(mtd_get_item_class))
            {
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = mtd_test_any_set;
                var insert = new CodeInstruction(OpCodes.Ldloca_S, lbd_tags);
                codes[i - 8].MoveLabelsTo(insert);
                codes.RemoveRange(i - 8, 6);
                codes.Insert(i - 8, insert);
                i -= 5;
            }
        }

        return codes;
    }

    /// <summary>
    /// when installing new mod, use modified tags to check for compatibility
    /// </summary>
    /// <param name="instructions"></param>
    /// <param name="generator"></param>
    /// <returns></returns>
    [HarmonyPatch(typeof(XUiM_AssembleItem), nameof(XUiM_AssembleItem.AddPartToItem))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_AddPartToItem_XUiM_AssembleItem(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        LocalBuilder lbd_tags_cur = generator.DeclareLocal(typeof(FastTags<TagGroup.Global>));
        LocalBuilder lbd_tags_after_install = generator.DeclareLocal(typeof(FastTags<TagGroup.Global>));
        MethodInfo mtd_has_any_tags = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.HasAnyTags));
        MethodInfo mtd_test_any_set = AccessTools.Method(typeof(FastTags<TagGroup.Global>), nameof(FastTags<TagGroup.Global>.Test_AnySet));
        MethodInfo mtd_get_item_class = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.ItemClass));
        MethodInfo mtd_get_cur_item = AccessTools.PropertyGetter(typeof(XUiM_AssembleItem), nameof(XUiM_AssembleItem.CurrentItem));
        MethodInfo mtd_is_empty = AccessTools.Method(typeof(ItemValue), nameof(ItemValue.IsEmpty));
        FieldInfo fld_cos = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.CosmeticMods));
        FieldInfo fld_mod = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Modifications));
        FieldInfo fld_installable_tags = AccessTools.Field(typeof(ItemClassModifier), nameof(ItemClassModifier.InstallableTags));
        
        for (int i = 3; i < codes.Count; i++)
        {
            //get current tags
            if (codes[i].opcode == OpCodes.Stloc_0)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, mtd_get_cur_item),
                    CodeInstruction.LoadField(typeof(ItemStack), nameof(ItemStack.itemValue)),
                    new CodeInstruction(OpCodes.Dup),
                    CodeInstruction.Call(typeof(LocalItemTagsManager), nameof(LocalItemTagsManager.GetTags)),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_tags_cur),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.Call(typeof(LocalItemTagsManager), nameof(LocalItemTagsManager.GetTagsAsIfInstalled)),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_tags_after_install)
                });
                i += 9;
            }
            //do not touch check on the modification item, check if current mod can be installed
            else if (codes[i].Calls(mtd_has_any_tags) && codes[i - 3].Calls(mtd_get_item_class))
            {
                if (codes[i - 1].LoadsField(fld_installable_tags))
                {
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call, mtd_get_cur_item),
                        CodeInstruction.LoadField(typeof(ItemStack), nameof(ItemStack.itemValue)),
                        new CodeInstruction(OpCodes.Ldloc_0),
                        CodeInstruction.Call(typeof(LocalItemTagsManager), nameof(LocalItemTagsManager.CanInstallMod)),
                        new CodeInstruction(OpCodes.Brfalse, codes[i + 1].operand)
                    });
                }
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = mtd_test_any_set;
                var insert = new CodeInstruction(OpCodes.Ldloca_S, lbd_tags_cur);
                codes[i - 6].MoveLabelsTo(insert);
                codes.RemoveRange(i - 6, 4);
                codes.Insert(i - 6, insert);
                i -= 3;
            }
        }

        return codes;
    }

    #endregion

    //change when aiming events are fired
    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.SetMoveState))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_SetMoveState_EntityPlayerLocal(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        FieldInfo fld_msa = AccessTools.Field(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.moveStateAiming));

        for (int i = 0; i < codes.Count - 2; i++)
        {
            if (codes[i].LoadsField(fld_msa) && codes[i + 2].opcode == OpCodes.Ldloc_1)
            {
                codes[i - 2].MoveLabelsTo(codes[i + 13]);
                codes.RemoveRange(i - 2, 15);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.AimingGun), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool Prefix_AimingGun_EntityAlive(bool value, EntityAlive __instance)
    {
        if (__instance is EntityPlayerLocal && __instance.inventory != null)
        {
            bool isAimingGun = __instance.AimingGun;
            if (value != isAimingGun)
            {
                __instance.FireEvent(value ? MinEventTypes.onSelfAimingGunStart : MinEventTypes.onSelfAimingGunStop, true);
#if DEBUG
                Log.Out(value ? "START AIMING GUN FIRED" : "STOP AIMING GUN FIRED");
#endif
            }
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.HasRadial))]
    [HarmonyPostfix]
    private static void Postfix_HasRadial_ItemActionAttack(ref bool __result)
    {
        EntityPlayerLocal player = GameManager.Instance.World?.GetPrimaryPlayer();
        int index = MultiActionManager.GetActionIndexForEntity(player);
        List<ItemActionData> actionDatas = player.inventory?.holdingItemData?.actionData;
        if (actionDatas != null && actionDatas.Count > index && actionDatas[index] is ItemActionRanged.ItemActionDataRanged rangedData && (rangedData.isReloading || rangedData.isWeaponReloading))
        {
            __result = false;
        }
    }

    [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.SetupRadial))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_SetupRadial_ItemActionAttack(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var fld_usable = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.UsableUnderwater));

        var lbd_states = generator.DeclareLocal(typeof(bool[]));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_0)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Ldc_I4_M1),
                    CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(CommonUtilityPatch.GetUnusableItemEntries)),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_states)
                });
                i += 4;
            }
            else if (codes[i].LoadsField(fld_usable))
            {
                codes.InsertRange(i + 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_states).WithLabels(codes[i + 2].ExtractLabels()),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(CommonUtilityPatch.IsAmmoDisabled)),
                    new CodeInstruction(OpCodes.Brtrue_S, codes[i + 1].operand)
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemAction), nameof(ItemAction.StartHolding))]
    [HarmonyPostfix]
    private static void Postfix_StartHolding_ItemAction(ItemActionData _data, ItemAction __instance)
    {
        if (__instance is ItemActionAttack itemActionAttack && _data.invData.holdingEntity is EntityPlayerLocal player)
        {
            var arr_disabled_ammo = GetUnusableItemEntries(itemActionAttack.MagazineItemNames, player, _data.indexInEntityOfAction);
            if (arr_disabled_ammo == null)
            {
                return;
            }
            var itemValue = _data.invData.itemValue;
            int cur_index = itemValue.GetSelectedAmmoIndexByActionIndex(_data.indexInEntityOfAction);
            if (arr_disabled_ammo[cur_index])
            {
                int first_enabled_index = Mathf.Max(Array.IndexOf(arr_disabled_ammo, false), 0);

                var mapping = MultiActionManager.GetMappingForEntity(player.entityId);
                if (mapping != null)
                {
                    if (_data.indexInEntityOfAction == mapping.CurMetaIndex)
                    {
                        itemValue.SelectedAmmoTypeIndex = (byte)first_enabled_index;
                    }
                    else
                    {
                        itemValue.SetMetadata(MultiActionUtils.ActionSelectedAmmoNames[mapping.indices.GetMetaIndexForActionIndex(_data.indexInEntityOfAction)], first_enabled_index, TypedMetadataValue.TypeTag.Integer);
                    }
                    _data.invData.holdingEntity.inventory.CallOnToolbeltChangedInternal();
                }
                else
                {
                    itemValue.SelectedAmmoTypeIndex = (byte)(first_enabled_index);
                }
            }
        }
    }

    public static bool[] GetUnusableItemEntries(string[] ammoNames, EntityPlayerLocal player, int actionIndex = -1)
    {
        if (ammoNames == null)
        {
            return null;
        }
        if (actionIndex < 0)
        {
            actionIndex = MultiActionManager.GetActionIndexForEntity(player);
        }
        string str_disabled_ammo_names = player.inventory.holdingItemItemValue.GetPropertyOverrideForAction("DisableAmmo", "", actionIndex);
        //Log.Out($"checking disabled ammo: {str_disabled_ammo_names}\n{StackTraceUtility.ExtractStackTrace()}");
        bool[] arr_disable_states = new bool[ammoNames.Length];
        if(!string.IsNullOrEmpty(str_disabled_ammo_names))
        {
            string[] arr_disabled_ammo_names = str_disabled_ammo_names.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var name in arr_disabled_ammo_names)
            {
                int index = Array.IndexOf(ammoNames, name.Trim());
                if (index >= 0)
                {
                    arr_disable_states[index] = true;
                    if (ConsoleCmdReloadLog.LogInfo)
                        Log.Out($"ammo {ammoNames[index]} is disabled");
                }
            }
        }
        return arr_disable_states;
    }

    private static bool IsAmmoDisabled(bool[] ammoStates, int index)
    {
        if (ammoStates == null || ammoStates.Length <= index)
        {
            return false;
        }
        return ammoStates[index];
    }

    //dont spread onSelfItemActivate/onSelfItemDeactivate to attachments
    //handle start holding
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.syncHeldItem))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_syncHeldItem_Inventory(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var prop_itemvalue = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItemItemValue));
        var mtd_fireevent = AccessTools.Method(typeof(ItemValue), nameof(ItemValue.FireEvent));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_fireevent) && codes[i - 5].Calls(prop_itemvalue))
            {
                codes[i] = CodeInstruction.Call(typeof(MinEffectController), nameof(MinEffectController.FireEvent));
                codes.InsertRange(i - 4, new[]
                {
                    CodeInstruction.Call(typeof(ItemValue), "get_ItemClass"),
                    CodeInstruction.LoadField(typeof(ItemClass), nameof(ItemClass.Effects))
                });
                i += 2;
            }
        }

        return codes;
    }

    //handle radial activation
    [HarmonyPatch(typeof(XUiC_Radial), nameof(XUiC_Radial.handleActivatableItemCommand))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_handleActivatableItemCommand_XUiC_Radial(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_fireevent = AccessTools.Method(typeof(ItemValue), nameof(ItemValue.FireEvent));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_fireevent))
            {
                codes[i] = CodeInstruction.Call(typeof(MinEffectController), nameof(MinEffectController.FireEvent));
                codes.InsertRange(i - 2, new[]
                {
                    CodeInstruction.Call(typeof(ItemValue), "get_ItemClass"),
                    CodeInstruction.LoadField(typeof(ItemClass), nameof(ItemClass.Effects))
                });
                i += 2;
            }
        }
        return codes;
    }

    //handle equipments
    [HarmonyPatch(typeof(Equipment), nameof(Equipment.SetSlotItem))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_SetSlotItem_Equipment(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_fireevent = AccessTools.Method(typeof(ItemValue), nameof(ItemValue.FireEvent));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_fireevent) && codes[i - 5].opcode == OpCodes.Ldloc_0 && codes[i - 4].OperandIs((int)MinEventTypes.onSelfItemDeactivate))
            {
                codes[i] = CodeInstruction.Call(typeof(MinEffectController), nameof(MinEffectController.FireEvent));
                codes.InsertRange(i - 4, new[]
                {
                    CodeInstruction.Call(typeof(ItemValue), "get_ItemClass"),
                    CodeInstruction.LoadField(typeof(ItemClass), nameof(ItemClass.Effects))
                });
                i += 2;
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.DropContentOfLootContainerServer))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_DropContentOfLootContainerServer_GameManager(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var setter_localscale = AccessTools.PropertySetter(typeof(Transform), nameof(Transform.localScale));

        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(setter_localscale))
            {
                codes.RemoveRange(i - 6, 7);
                break;
            }
        }

        return codes;
    }

    //[HarmonyPatch(typeof(Inventory), nameof(Inventory.Execute))]
    //[HarmonyPrefix]
    //private static void Prefix_Execute_Inventory(Inventory __instance, int _actionIdx, bool _bReleased, PlayerActionsLocal _playerActions = null)
    //{
    //    Log.Out($"Execute Inventory holding item {__instance.holdingItem.Name} slot {__instance.holdingItemIdx} action index {_actionIdx} released {_bReleased} is holster delay {__instance.IsHolsterDelayActive()} is unholster delay {__instance.IsUnholsterDelayActive()}");
    //}

    //[HarmonyPatch(typeof(Inventory), nameof(Inventory.updateHoldingItem))]
    //[HarmonyTranspiler]
    //private static IEnumerable<CodeInstruction> Transpiler_updateHoldingItem_Inventory(IEnumerable<CodeInstruction> instructions)
    //{
    //    var codes = instructions.ToList();

    //    var mtd_setholdingtrans = AccessTools.Method(typeof(Inventory), nameof(Inventory.setHoldingItemTransfrom));
    //    var mtd_showrighthand = AccessTools.Method(typeof(Inventory), nameof(Inventory.ShowRightHand));
    //    int insert = -1, take = -1;

    //    for (int i = 0; i < codes.Count; i++)
    //    {
    //        if (codes[i].Calls(mtd_showrighthand))
    //        {
    //            insert = i + 1;
    //        }
    //        else if (codes[i].Calls(mtd_setholdingtrans))
    //        {
    //            take = i - 6;
    //        }
    //    }

    //    if (take > insert)
    //    {
    //        var list_take = codes.GetRange(take, 7);
    //        codes.RemoveRange(take, 7);
    //        codes.InsertRange(insert, list_take);
    //    }

    //    return codes;
    //}
    //private static bool exported = false;
    //[HarmonyPatch(typeof(EModelSDCS), nameof(EModelSDCS.createModel))]
    //[HarmonyPostfix]
    //private static void Postfix_test(EModelSDCS __instance)
    //{
    //    if (!exported)
    //    {
    //        exported = true;
    //        var objects = new[] { __instance.entity.RootTransform.gameObject.GetComponentsInChildren<Animator>()[1] };
    //        Log.Out($"exporting objs: {objects.Length} avatar {objects[0].avatar.name} is human {objects[0].avatar.isHuman}");
    //        FbxExporter07.OnExport(objects, @"E:\Unity Projects\AnimationPlayground\Assets\ExportedProject\example_skinned_mesh_with_bones.fbx");
    //        Application.Quit();
    //    }
    //}
}

