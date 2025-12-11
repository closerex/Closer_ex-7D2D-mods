using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using UnityEngine;
using KFCommonUtilityLib.Attributes;

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

        AnimationAmmoUpdateState.SetAmmoCountForEntity(holdingEntity, holdingEntity.inventory.holdingItemIdx);
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
    private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemActionRanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new List<CodeInstruction>(instructions);
        var mtd_fire_event = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.FireEvent));
        var mtd_get_model_layer = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.GetModelLayer));
        var mtd_get_perc_left = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.PercentUsesLeft));
        var mtd_check_ammo = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.checkAmmo));
        var mtd_getkick = AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetKickbackForce));

        int take = -1, insert = -1;
        for (int i = 0; i < codes.Count; ++i)
        {
            if (codes[i].opcode == OpCodes.Ldc_I4_S && codes[i].OperandIs((int)MinEventTypes.onSelfRangedBurstShotEnd) && codes[i + 2].Calls(mtd_fire_event))
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
        for (int i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].Calls(mtd_check_ammo))
            {
                var lbl = generator.DefineLabel();
                codes[i + 2].WithLabels(lbl);
                codes.InsertRange(i + 2, new[]
                {
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.state)),
                    new CodeInstruction(OpCodes.Brfalse, lbl),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.invData)),
                    CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.gameManager)),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.invData)),
                    CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.holdingEntity)),
                    CodeInstruction.LoadField(typeof(Entity), nameof(Entity.entityId)),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.invData)),
                    CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.slotIdx)),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.indexInEntityOfAction)),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    CodeInstruction.Call(typeof(Vector3), "get_zero"),
                    CodeInstruction.Call(typeof(Vector3), "get_zero"),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(IGameManager), nameof(IGameManager.ItemActionEffectsServer))),
                    CodeInstruction.LoadLocal(0),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    CodeInstruction.StoreField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.state))
                });
                i += 23;
            }
            else if (codes[i].Calls(mtd_getkick))
            {
                var lbl = generator.DefineLabel();
                codes[i + 2].WithLabels(lbl);
                codes.InsertRange(i + 2, new[]
                {
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.state)),
                    new CodeInstruction(OpCodes.Brfalse_S, lbl),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.curBurstCount)),
                    CodeInstruction.LoadLocal(3),
                    new CodeInstruction(OpCodes.Blt_S, lbl),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.invData)),
                    CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.gameManager)),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.invData)),
                    CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.holdingEntity)),
                    CodeInstruction.LoadField(typeof(Entity), nameof(Entity.entityId)),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.invData)),
                    CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.slotIdx)),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.indexInEntityOfAction)),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    CodeInstruction.Call(typeof(Vector3), "get_zero"),
                    CodeInstruction.Call(typeof(Vector3), "get_zero"),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(IGameManager), nameof(IGameManager.ItemActionEffectsServer))),
                    CodeInstruction.LoadLocal(0),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    CodeInstruction.StoreField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.state))
                });
                i += 27;
            }
        }

        return codes;
    }

    //fix recoil animation does not match weapon RPM
    private static int weaponFireHash = Animator.StringToHash("WeaponFire");
    private static int aimHash = Animator.StringToHash("IsAiming");
    private static HashSet<int> hash_shot_state = new HashSet<int>();
    private static HashSet<int> hash_aimshot_state = new HashSet<int>();

    public static void InitShotStates(ref ModEvents.SGameAwakeData _)
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

        if (_rangedData.invData.model.TryGetComponent<AnimationTargetsAbs>(out var targets) && targets.ItemFpv)
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
                codes.InsertRange(i + 1, new[]
                {
                    CodeInstruction.LoadLocal(0).WithLabels(codes[i - 4].ExtractLabels()),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.invData)),
                    CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.gameManager)),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.invData)),
                    CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.holdingEntity)),
                    CodeInstruction.LoadField(typeof(Entity), nameof(Entity.entityId)),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.invData)),
                    CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.slotIdx)),
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.indexInEntityOfAction)),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    CodeInstruction.Call(typeof(Vector3), "get_zero"),
                    CodeInstruction.Call(typeof(Vector3), "get_zero"),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(IGameManager), nameof(IGameManager.ItemActionEffectsServer))),
                    CodeInstruction.LoadLocal(0),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    CodeInstruction.StoreField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.state))
                });
                codes.RemoveRange(i - 4, 5);
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
        CommonUtilityLibInit.RegisterKFEnums();
        return true;
    }

    [HarmonyPatch(typeof(PassiveEffect), nameof(PassiveEffect.ParsePassiveEffect))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ParsePassiveEffect_PassiveEffect(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(AccessTools.Method(typeof(EnumUtils), nameof(EnumUtils.Parse), new[] { typeof(string), typeof(bool) }, new[] { typeof(PassiveEffects) }),
                                           AccessTools.Method(typeof(CustomEffectEnumManager), nameof(CustomEffectEnumManager.RegisterOrGetEnum), new[] { typeof(string), typeof(bool) }, new[] { typeof(PassiveEffects) }));

    }

    [HarmonyPatch(typeof(MinEventActionBase), nameof(MinEventActionBase.ParseXmlAttribute))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ParseXmlAttribute_MinEventActionBase(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(AccessTools.Method(typeof(EnumUtils), nameof(EnumUtils.Parse), new[] { typeof(string), typeof(bool) }, new[] { typeof(MinEventTypes) }),
                                           AccessTools.Method(typeof(CustomEffectEnumManager), nameof(CustomEffectEnumManager.RegisterOrGetEnum), new[] { typeof(string), typeof(bool) }, new[] { typeof(MinEventTypes) }));
    }

    //todo: patch Quartz
    [HarmonyPatch(typeof(UIDisplayInfoFromXml), nameof(UIDisplayInfoFromXml.ParseDisplayInfoEntry))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ParseDisplayInfoEntry_UIDisplayInfoFromXml(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(AccessTools.Method(typeof(EnumUtils), nameof(EnumUtils.Parse), new[] { typeof(string), typeof(bool) }, new[] { typeof(PassiveEffects) }), 
                                           AccessTools.Method(typeof(CustomEffectEnumManager), nameof(CustomEffectEnumManager.GetEnumOrThrow), new[] { typeof(string), typeof(bool) }, new[] { typeof(PassiveEffects) }));
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
        FieldInfo fld_tags = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.ItemTags));

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
            else if (codes[i].Calls(mtd_has_any_tags) && codes[i - 3].Calls(mtd_get_item_class) && !codes[i - 1].LoadsField(fld_tags))
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
                MultiActionUtils.SetMinEventParamsByEntityInventory(__instance);
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
            string[] arr_disabled_ammo_names = str_disabled_ammo_names.Split(',', StringSplitOptions.RemoveEmptyEntries);
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

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.onHoldingEntityFired))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_onHoldingEntityFired_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Ldc_R4 && codes[i].operand is 5f)
            {
                codes.RemoveAt(i);
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(CommonUtilityPatch.GetMaxSpread))
                });
                break;
            }
        }
        return codes;
    }

    private static float GetMaxSpread(ItemActionData _data)
    {
        return EffectManager.GetValue(CustomEnums.MaxWeaponSpread, _data.invData.itemValue, 5f, _data.invData.holdingEntity);
    }

    [HarmonyPatch(typeof(NetEntityDistributionEntry), nameof(NetEntityDistributionEntry.getSpawnPacket))]
    [HarmonyPrefix]
    private static bool Prefix_getSpawnPacket_NetEntityDistributionEntry(NetEntityDistributionEntry __instance, ref NetPackage __result)
    {
        if (__instance.trackedEntity is EntityAlive ea)
        { 
            __result = NetPackageManager.GetPackage<NetPackageEntitySpawnWithCVar>().Setup(new EntityCreationData(__instance.trackedEntity, true), (EntityAlive)__instance.trackedEntity);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(World), nameof(World.SpawnEntityInWorld))]
    [HarmonyPostfix]
    private static void Postfix_SpawnEntityInWorld_World(Entity _entity)
    {
        if (_entity is EntityAlive ea && !ea.isEntityRemote)
        {
            ea.FireEvent(CustomEnums.onSelfFirstCVarSync);
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionEffects_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var fld_end = AccessTools.Field(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.SoundEnd));
        var fld_meta = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Meta));

        int flashLocalIndex = GameManager.IsDedicatedServer && Application.platform == RuntimePlatform.LinuxServer ? 7 : 9;
        int smokeLocalIndex = GameManager.IsDedicatedServer && Application.platform == RuntimePlatform.LinuxServer ? 10 : 12;
        for (int i = 0; i < codes.Count - 2; i++)
        {
            if (codes[i].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == flashLocalIndex && codes[i + 2].Branches(out _))
            {
                codes.InsertRange(i + 3, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, codes[i].operand).WithLabels(codes[i + 3].ExtractLabels()),
                    CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(AddTmpMuzzleFlash)),
                });
                i += 5;
            }
            else if (codes[i].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == smokeLocalIndex && codes[i + 2].Branches(out _))
            {
                codes.InsertRange(i + 3, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, codes[i].operand).WithLabels(codes[i + 3].ExtractLabels()),
                    CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(AddTmpMuzzleFlash)),
                });
                i += 5;
            }
            else if (codes[i].LoadsField(fld_end))
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (codes[j].LoadsField(fld_meta))
                    {
                        codes.RemoveRange(j - 3, 5);
                        i -= 5;
                        break;
                    }
                }
            }
        }
        return codes;
    }

    private static void AddTmpMuzzleFlash(Transform trans)
    {
        if (trans.TryGetComponent<TemporaryObject>(out var tmp))
        {
            tmp.StopAllCoroutines();
            Component.Destroy(tmp);
        }
        tmp = trans.AddMissingComponent<TemporaryMuzzleFlash>();
        tmp.life = 5f;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.onHoldingEntityFired))]
    [HarmonyPostfix]
    private static void Postfix_onHoldingEntityFired_ItemActionRanged(ItemActionData _actionData)
    {
        if (!_actionData.invData.holdingEntity.isEntityRemote)
        {
            AnimationAmmoUpdateState.SetAmmoCountForEntity(_actionData.invData.holdingEntity, _actionData.invData.slotIdx);
        }
    }

    [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.UpdateShakes))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_UpdateShakes_vp_FPCamera(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var fld_shake = AccessTools.Field(typeof(vp_FPCamera), nameof(vp_FPCamera.m_Shake));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].StoresField(fld_shake))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(CheckShakeNaN))
                });
                break;
            }
        }
        return codes;
    }

    private static void CheckShakeNaN(vp_FPCamera fpcamera)
    {
        if (float.IsNaN(fpcamera.m_Shake.x) || float.IsNaN(fpcamera.m_Shake.y) || float.IsNaN(fpcamera.m_Shake.z))
        {
            Log.Warning("Shake1 NaN {0}, time {1}, speed {2}, amp {3}", new object[]
            {
                fpcamera.m_Shake,
                Time.time,
                fpcamera.ShakeSpeed,
                fpcamera.ShakeAmplitude
            });
            fpcamera.ShakeSpeed = 0f;
            fpcamera.m_Shake = Vector3.zero;
            fpcamera.m_Pitch += -1f;
        }
    }

    //fix vanilla nre where take all button is missing and player presses R
    [HarmonyPatch(typeof(XUiC_WorkstationOutputWindow), nameof(XUiC_WorkstationOutputWindow.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Update_XUiC_WorkstationOutputWindow(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_update = AccessTools.Method(typeof(XUiController), nameof(XUiController.Update));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_update))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(XUiC_WorkstationOutputWindow), nameof(XUiC_WorkstationOutputWindow.controls)),
                    new CodeInstruction(OpCodes.Brfalse_S, codes[codes.Count - 1].labels[0]),
                });
                break;
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(XUiC_SkillPerkLevel), nameof(XUiC_SkillPerkLevel.btnBuy_OnPress))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_btnBuy_OnPress(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_fire = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.FireEvent));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_fire))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.MinEventContext)),
                    CodeInstruction.CallClosure<Action<XUiC_SkillPerkLevel, MinEventParams>>((xui, par) =>
                    {
                        xui.CurrentSkill.ProgressionClass.FireEvent(MinEventTypes.onPerkLevelChanged, par);
                    }),
                });
                codes.RemoveRange(i - 3, 4);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(EntityAlive.EntityNetworkStats), nameof(EntityAlive.EntityNetworkStats.ToEntity))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ToEntity_EntityAlive_EntityNetworkStats(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var mtd_equal = AccessTools.Method(typeof(object), nameof(object.Equals), new[] { typeof(object) });
        var mtd_force_update = AccessTools.Method(typeof(Inventory), nameof(Inventory.ForceHoldingItemUpdate));

        var lbd = generator.DeclareLocal(typeof(bool));
        var lbl_false = generator.DefineLabel();
        var lbl_true = generator.DefineLabel();
        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_equal))
            {
                for (var j = i + 1; j < codes.Count; j++)
                {
                    if (codes[j].Calls(mtd_force_update))
                    {
                        codes.InsertRange(j - 2, new[]
                        {
                            new CodeInstruction(OpCodes.Ldloc_S, lbd).WithLabels(codes[j - 2].ExtractLabels()),
                            new CodeInstruction(OpCodes.Brfalse_S, codes[i + 1].operand)
                        });
                        break;
                    }
                }

                codes.InsertRange(i + 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1).WithLabels(codes[i + 2].ExtractLabels()),
                    CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.inventory)),
                    CodeInstruction.LoadField(typeof(Inventory), nameof(Inventory.m_HoldingItemIdx)),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(EntityAlive.EntityNetworkStats), nameof(EntityAlive.EntityNetworkStats.holdingItemIndex)),
                    new CodeInstruction(OpCodes.Bne_Un_S, lbl_false),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.inventory)),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(EntityAlive.EntityNetworkStats), nameof(EntityAlive.EntityNetworkStats.holdingItemIndex)),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Inventory), nameof(Inventory.GetItem))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(EntityAlive.EntityNetworkStats), nameof(EntityAlive.EntityNetworkStats.holdingItemStack)),
                    CodeInstruction.Call(typeof(EntityInventoryExtension), nameof(EntityInventoryExtension.ShouldUpdateItem), new[]{typeof(ItemStack), typeof(ItemStack)}),
                    new CodeInstruction(OpCodes.Brfalse_S, lbl_false),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Br_S, lbl_true),
                    new CodeInstruction(OpCodes.Ldc_I4_0).WithLabels(lbl_false),
                    new CodeInstruction(OpCodes.Stloc_S, lbd).WithLabels(lbl_true)
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(Equipment), nameof(Equipment.Apply))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Apply_Equipment(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var lbd = generator.DeclareLocal(typeof(bool));

        var mtd_update = AccessTools.Method(typeof(EModelSDCS), nameof(EModelSDCS.UpdateEquipment));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_update))
            {
                codes.InsertRange(i - 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, lbd),
                    new CodeInstruction(OpCodes.Brfalse_S, codes[i - 2].operand)
                });
                break;
            }
        }

        codes.InsertRange(0, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldarg_1),
            CodeInstruction.CallClosure<Func<Equipment, Equipment, bool>>(static (curEquip, tarEquip) =>
            {
                int slotCount = curEquip.GetSlotCount();
                bool isLocalPlayer = curEquip.m_entity is EntityPlayerLocal;
                for (int i = 0; i < slotCount; i++)
                {
                    int curItemType = -1, tarItemType = -1;
                    ItemClass curCosItem = curEquip.m_cosmeticSlots[i];
                    ItemClass tarCosItem = tarEquip.m_cosmeticSlots[i];
                    ItemValue curEquipItem = curEquip.m_slots[i];
                    ItemValue tarEquipItem = tarEquip.m_slots[i];
                    if (isLocalPlayer)
                    {
                        if (curCosItem != null && curEquip.HasCosmeticUnlocked(curCosItem).isUnlocked && curCosItem != ItemClass.MissingItem)
                        {
                            curItemType = curCosItem.Id;
                        }
                        else if (curEquipItem != null)
                        {
                            curItemType = curEquipItem.type;
                        }

                        if (tarCosItem != null && tarEquip.HasCosmeticUnlocked(tarCosItem).isUnlocked && tarCosItem != ItemClass.MissingItem)
                        {
                            tarItemType = tarCosItem.Id;
                        }
                        else if (tarEquipItem != null)
                        {
                            tarItemType = tarEquipItem.type;
                        }
                    }
                    else
                    {
                        if (curCosItem != null && curCosItem != ItemClass.MissingItem)
                        {
                            curItemType = curCosItem.Id;
                        }
                        else if (curEquipItem != null)
                        {
                            curItemType = curEquipItem.type;
                        }

                        if (tarCosItem != null && tarCosItem != ItemClass.MissingItem)
                        {
                            tarItemType = tarCosItem.Id;
                        }
                        else if (tarEquipItem != null)
                        {
                            tarItemType = tarEquipItem.type;
                        }
                    }

                    if (curItemType != tarItemType)
                    {
                        return true;
                    }
                }
                return false;
            }),
            new CodeInstruction(OpCodes.Stloc_S, lbd)
        });

        return codes;
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.CancelInventoryActions), MethodType.Enumerator)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_CancelInventoryActions_EntityPlayerLocal(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_isreloading = AccessTools.Method(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.IsReloading));
        var mtd_cancelaction = AccessTools.Method(typeof(ItemAction), nameof(ItemAction.CancelAction));
        bool vanillaCancelPatched = false;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_cancelaction))
            {
                if (!vanillaCancelPatched)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (codes[j].Branches(out _))
                        {
                            codes.InsertRange(j + 1, new[]
                            {
                                new CodeInstruction(OpCodes.Ldloc_1).WithLabels(codes[j + 1].ExtractLabels()),
                                new CodeInstruction(OpCodes.Ldloc_2),
                                CodeInstruction.CallClosure<Action<EntityPlayerLocal, int>>((player, index) =>
                                {
                                    if (player.inventory.holdingItemData.actionData[index] is IModuleContainerFor<ActionModuleAnimationInterruptable.AnimationInterruptableData> interruptData)
                                    {
                                        interruptData.Instance.interruptRequested = true;
                                    }
                                })
                            });
                            i += 3;
                            break;
                        }
                    }
                    vanillaCancelPatched = true;
                }
            }
            else if (codes[i].Calls(mtd_isreloading))
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (codes[j].opcode == OpCodes.Br_S || codes[j].opcode == OpCodes.Br)
                    {
                        codes.InsertRange(j, new[]
                        {
                            new CodeInstruction(codes[i - 1].opcode, codes[i - 1].operand).WithLabels(codes[j].ExtractLabels()),
                            CodeInstruction.CallClosure<Action<EntityPlayerLocal>>(static (player) =>
                            {
                                if (player.inventory.holdingItemData is IModuleContainerFor<ItemModuleMultiItem.MultiItemInvData> dataModule)
                                {
                                    var multiInvData = dataModule.Instance;
                                    if (multiInvData.boundInvData != null && multiInvData.boundItemClass != null && multiInvData.boundItemClass.Actions != null && multiInvData.boundInvData.actionData != null)
                                    {
                                        var prevData = player.MinEventContext.ItemActionData;
                                        multiInvData.SetBoundParams();
                                        for (int i = 0; i < multiInvData.boundInvData.actionData.Count; i++)
                                        {
                                            var action = multiInvData.boundItemClass.Actions[i];
                                            var actionData = multiInvData.boundInvData.actionData[i];
                                            if (action != null && actionData != null)
                                            {
                                                player.MinEventContext.ItemActionData = actionData;
                                                if (action.IsActionRunning(actionData))
                                                {
                                                    if (actionData is IModuleContainerFor<ActionModuleAnimationInterruptable.AnimationInterruptableData> interruptData)
                                                    {
                                                        interruptData.Instance.interruptRequested = true;
                                                    }
                                                    action.CancelAction(actionData);
                                                }
                                            }
                                        }
                                        multiInvData.RestoreParams(false);
                                        player.MinEventContext.ItemActionData = prevData;
                                    }
                                }
                            })
                        });
                        break;
                    }
                }
                break;
            }
        }
        return codes;
    }
    private static readonly int MeleeRunningHash = Animator.StringToHash("IsMeleeRunning");
    private static readonly int PowerAttackHash = Animator.StringToHash("PowerAttack");

    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.ExecuteAction))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemActionDynamicMelee(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var fld_released = AccessTools.Field(typeof(ItemActionDynamicMelee.ItemActionDynamicMeleeData), nameof(ItemActionDynamicMelee.ItemActionDynamicMeleeData.HasReleased));
        var mtd_updatebool = AccessTools.Method(typeof(AvatarController), nameof(AvatarController.UpdateBool), new[] { typeof(int), typeof(bool), typeof(bool) });
        var mtd_setfinished = AccessTools.Method(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.SetAttackFinished));

        for (int i = 1; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_3)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_3).WithLabels(codes[i + 1].ExtractLabels()),
                    CodeInstruction.LoadField(typeof(CommonUtilityPatch), nameof(MeleeRunningHash)),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Callvirt, mtd_updatebool),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    CodeInstruction.StoreField(typeof(ItemActionDynamicMelee.ItemActionDynamicMeleeData), nameof(ItemActionDynamicMelee.ItemActionDynamicMeleeData.HasReleased))
                });
                i += 5;
            }
            else if (codes[i].StoresField(fld_released))
            {
                if (codes[i - 1].opcode == OpCodes.Ldc_I4_1)
                {
                    for (int j = i + 1; j < codes.Count; j++)
                    {
                        if (codes[j].Calls(mtd_setfinished))
                        {
                            codes[j + 1].WithLabels(codes[j - 2].ExtractLabels());
                            codes.RemoveRange(j - 2, 3);
                            i -= 3;
                            break;
                        }
                    }
                    //codes.InsertRange(i + 1, new[]
                    //{
                    //    new CodeInstruction(OpCodes.Ldarg_1),
                    //    CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.invData)),
                    //    CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.holdingEntity)),
                    //    CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.emodel)),
                    //    CodeInstruction.LoadField(typeof(EModelBase), nameof(EModelBase.avatarController)),
                    //    CodeInstruction.LoadField(typeof(CommonUtilityPatch), nameof(MeleeRunningHash)),
                    //    new CodeInstruction(OpCodes.Ldc_I4_0),
                    //    new CodeInstruction(OpCodes.Ldc_I4_1),
                    //    new CodeInstruction(OpCodes.Callvirt, mtd_updatebool),
                    //});
                    //i += 8;
                }
                //else if (codes[i - 1].opcode == OpCodes.Ldc_I4_0)
                //{
                //    codes.InsertRange(i + 1, new[]
                //    {
                //        new CodeInstruction(OpCodes.Ldloc_0),
                //        CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(CheckMeleeRunning)),
                //    });
                //    i += 2;
                //}
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.SetAttackFinished))]
    [HarmonyPostfix]
    private static void Postfix_SetAttackFinished_ItemActionDynamicMelee(ItemActionData _actionData)
    {
        ItemActionDynamicMelee.ItemActionDynamicMeleeData meleeData = _actionData as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
        if (meleeData != null && meleeData.invData?.holdingEntity?.emodel?.avatarController != null)
        {
            meleeData.invData.holdingEntity.emodel.avatarController.UpdateBool(MeleeRunningHash, false, true);
            meleeData.HasExecuted = false;
            //Log.Out($"set attack finished on item {_actionData.invData.item.GetLocalizedItemName()} action {_actionData.indexInEntityOfAction}\n{StackTraceUtility.ExtractStackTrace()}");
        }
    }

    //private static void CheckMeleeRunning(ItemActionDynamicMelee.ItemActionDynamicMeleeData data)
    //{
    //    if (data.Attacking && (data.invData.itemValue.PercentUsesLeft <= 0f || data.invData.holdingEntity.Stamina < data.StaminaUsage || !data.invData.holdingEntity.inventory.GetIsFinishedSwitchingHeldItem()))
    //    {
    //        //data.invData.holdingEntity.emodel.avatarController.UpdateBool(MeleeRunningHash, false, true);
    //        data.HasExecuted = true;
    //    }
    //}

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.ExecuteAction))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemClass(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var fld_avatar = AccessTools.Field(typeof(EModelBase), nameof(EModelBase.avatarController));

        for (int i = 1; i < codes.Count - 1; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_1 && codes[i - 1].opcode == OpCodes.Ldarg_3)
            {
                for (int j = i + 1; j < codes.Count; j++)
                {
                    if (codes[j].opcode == OpCodes.Blt_S || codes[j].opcode == OpCodes.Blt)
                    {
                        codes.InsertRange(j + 1, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_3).WithLabels(codes[j + 1].ExtractLabels()),
                            new CodeInstruction(OpCodes.Brtrue_S, codes[j].operand)
                        });
                        i += 2;
                        break;
                    }
                }
            }
            else if (codes[i].LoadsField(fld_avatar) && codes[i + 1].opcode == OpCodes.Ldnull)
            {
                for (int j = i + 1; j < codes.Count; j++)
                {
                    if (codes[j].Branches(out var lbl))
                    {
                        codes.InsertRange(j + 1, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_3).WithLabels(codes[j + 1].ExtractLabels()),
                            new CodeInstruction(OpCodes.Brtrue_S, lbl)
                        });
                        i += 2;
                        break;
                    }
                }
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(MinEventActionSetItemMetaFloat), nameof(MinEventActionSetItemMetaFloat.Execute))]
    [HarmonyPostfix]
    private static void Postfix_Execute_MinEventActionSetItemMetaFloat(MinEventParams _params)
    {
        if (_params.Self is EntityPlayerLocal player && player.inventory != null && _params.ItemValue == player.inventory.holdingItemItemValue)
        {
            player.inventory.CallOnToolbeltChangedInternal();
        }
    }

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.GetCrosshairType))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_GetCrosshairType_ItemClass(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var mtd_getoverride = AccessTools.Method(typeof(ItemValue), nameof(ItemValue.GetPropertyOverride));
        var fld_empty = AccessTools.Field(typeof(string), nameof(string.Empty));

        var lbd = generator.DeclareLocal(typeof(string));

        for (int i = 1; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_getoverride) && codes[i - 1].LoadsField(fld_empty))
            {
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Ldstr, "False"),
                    CodeInstruction.StoreLocal(lbd.LocalIndex),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(ItemClass), nameof(ItemClass.Properties)),
                    CodeInstruction.LoadField(typeof(ItemClass), nameof(ItemClass.PropCrosshairOnAim)),
                    CodeInstruction.LoadLocal(lbd.LocalIndex, true),
                    CodeInstruction.Call(typeof(DynamicProperties), nameof(DynamicProperties.ParseString)),
                    CodeInstruction.LoadLocal(lbd.LocalIndex)
                });
                codes.RemoveAt(i - 1);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.FireEvent))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_FireEvent_EntityAlive(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_invevent = AccessTools.Method(typeof(Inventory), nameof(Inventory.FireEvent));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_invevent))
            {
                codes[i] = CodeInstruction.CallClosure<Action<Inventory, MinEventTypes, MinEventParams>>((inv, eventType, par) =>
                {
                    ItemValue prevItemValue = par.ItemValue;
                    ItemActionData prevActionData = par.ItemActionData;
                    ItemInventoryData prevInvData = par.ItemInventoryData;

                    par.ItemValue = inv.holdingItemItemValue;
                    par.ItemInventoryData = inv.holdingItemData;
                    par.ItemActionData = par.ItemInventoryData?.actionData?[MultiActionManager.GetActionIndexForEntity(inv.entity)];
                    par.ItemValue.FireEvent(eventType, par);

                    par.ItemValue = prevItemValue;
                    par.ItemActionData = prevActionData;
                    par.ItemInventoryData = prevInvData;
                });
                break;
            }
        }
        return codes;
    }

    public static void Test()
    {
        var player = GameManager.Instance.World.GetPrimaryPlayer();
        var localUI = player.PlayerUI;
        var xui = localUI.xui;
        GameObject parentObj = new GameObject("ScreenIconTest");
        Transform parentTrans = parentObj.transform;
        parentTrans.SetParent(xui.transform.parent);
        parentTrans.localPosition = new Vector3(0, 0, 0);
        parentTrans.localRotation = Quaternion.identity;
        parentTrans.localScale = Vector3.one;
        parentObj.layer = 12;
        GameObject spriteObj = new GameObject("Sprite");
        spriteObj.transform.SetParent(parentTrans, false);
        spriteObj.layer = 12;
        UISprite uisprite = spriteObj.AddComponent<UISprite>();
        uisprite.atlas = xui.GetAtlasByName("ItemIconAtlas", "icon_medic_darts_ammo");
        uisprite.spriteName = "icon_medic_darts_ammo";
        uisprite.color = Color.white;
        uisprite.pivot = UIWidget.Pivot.BottomLeft;
        uisprite.SetDimensions(16, 16);
        uisprite.depth = 300;
        uisprite.transform.localPosition = Vector3.zero;
        GameObject textTrans = new GameObject("Label");
        textTrans.transform.SetParent(parentTrans, false);
        textTrans.layer = 12;
        UILabel uilabel = textTrans.AddComponent<UILabel>();
        uilabel.font = xui.GetUIFontByName("ReferenceFont", true);
        uilabel.fontSize = 20;
        uilabel.pivot = UIWidget.Pivot.BottomLeft;
        uilabel.overflowMethod = UILabel.Overflow.ResizeFreely;
        uilabel.alignment = NGUIText.Alignment.Left;
        uilabel.effectStyle = UILabel.Effect.Shadow;
        uilabel.effectColor = new Color32(0, 0, 0, byte.MaxValue);
        uilabel.effectDistance = new Vector2(2f, 2f);
        uilabel.color = Color.white;
        uilabel.text = "65,535";
        uilabel.depth = 300;
        uilabel.width = 200;
        uilabel.transform.localPosition = new Vector2(20, -2.4f);
        parentTrans.localPosition = new Vector2(-1280 * XUi.UIRoot.pixelSizeAdjustment, 0);
    }

    //[HarmonyPatch(typeof(NetPackageDamageEntity), nameof(NetPackageDamageEntity.ProcessPackage)), HarmonyPostfix]
    //private static void Postfix_Test()
    //{
    //    Log.Out($"Damage Entity Package processed!");
    //}

    ////buff update tick hook network?
    //[HarmonyPatch(typeof(NetPackageEntityStatChanged), nameof(NetPackageEntityStatChanged.ProcessPackage))]
    ////buff update tick hook local?
    //[HarmonyPatch(typeof(EntityStats), nameof(EntityStats.UpdateNPCStatsOverTime))]
    ////damage processor
    //[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.ProcessDamageResponseLocal))]
    //private static void dumb() { }

    //[HarmonyPatch(typeof(ProgressionValue), nameof(ProgressionValue.Level), MethodType.Setter)]
    //[HarmonyPostfix]
    //private static void Postfix_Level_ProgressionValue(int value, ProgressionValue __instance)
    //{
    //    Log.Out($"ProgressionValue Level set to {value} for {__instance.Name}\n{StackTraceUtility.ExtractStackTrace()}");
    //}

    //[HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.AddBuff), typeof(string), typeof(Vector3i), typeof(int), typeof(bool), typeof(bool), typeof(float))]
    //[HarmonyPostfix]
    //private static void Postfix_AddBuff_EntityBuffs(string _name, EntityBuffs __instance, EntityBuffs.BuffStatus __result, bool _netSync)
    //{
    //    if (_name.StartsWith("eftZombieRandomArmor") || _name.StartsWith("eftZombieArmor"))
    //        Log.Out($"AddBuff [{_name}] on entity {__instance.parent.GetDebugName()} should sync {_netSync} result {__result.ToStringCached()}\n{StackTraceUtility.ExtractStackTrace()}");
    //}

    //[HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.RemoveBuff))]
    //[HarmonyPostfix]
    //private static void Postfix_RemoveBuff_EntityBuffs(string _name, EntityBuffs __instance, bool _netSync)
    //{
    //    if (_name.StartsWith("eftZombieRandomArmor") || _name.StartsWith("eftZombieArmor"))
    //        Log.Out($"RemoveBuff [{_name}] on entity {__instance.parent.GetDebugName()} should sync {_netSync}\n{StackTraceUtility.ExtractStackTrace()}");
    //}

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

