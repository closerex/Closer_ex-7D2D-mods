using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(MeleeShooterData))]
public class ActionModuleMeleeShooter
{
    public bool ignoreAmmoCheck;
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.TryExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_TryExecuteAction(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var mtd_checkammo = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.checkAmmo));
        var mtd_canreload = AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.CanReload));
        bool shouldCheckAmmoPatched = false, canReloadPatched = false;

        for (int i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].Calls(mtd_checkammo))
            {
                if (shouldCheckAmmoPatched || 
                    (codes[i + 1].opcode != OpCodes.Brtrue && codes[i + 1].opcode != OpCodes.Brtrue_S) ||
                    codes[i + 1].operand is not Label lbl)
                {
                    throw new InvalidOperationException("MeleeShooter TryExecuteAction checkAmmo branch changed");
                }

                bool ammoSuccessPatched = false;
                for (int j = i + 2; j < codes.Count; j++)
                {
                    if (codes[j].labels.Contains(lbl))
                    {
                        ammoSuccessPatched = true;
                        var lbl_continue = generator.DefineLabel();
                        var ammoSuccess = codes[j];
                        codes.InsertRange(j, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_0).WithLabels(ammoSuccess.ExtractLabels()),
                            new CodeInstruction(OpCodes.Ldloc_0),
                            CodeInstruction.Call(typeof(ActionModuleMeleeShooter), nameof(CheckMelee)),
                            new CodeInstruction(OpCodes.Brtrue_S, lbl_continue),
                            new CodeInstruction(OpCodes.Ret),
                        });
                        ammoSuccess.WithLabels(lbl_continue);
                    }
                }
                
                if (!ammoSuccessPatched)
                {
                    throw new InvalidOperationException("MeleeShooter TryExecuteAction ammo success target not found");
                }
                codes.InsertRange(i - 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[i - 2].ExtractLabels()),
                    CodeInstruction.Call(typeof(ModuleExtensions), nameof(ModuleExtensions.LoadModule), null, new []{typeof(ActionModuleMeleeShooter)}),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.Call(typeof(ItemActionModuleProcessor), nameof(ItemActionModuleProcessor.LoadDataModule), null, new []{typeof(MeleeShooterData)}),
                    CodeInstruction.Call(typeof(ActionModuleMeleeShooter), nameof(ShouldCheckAmmo)),
                    new CodeInstruction(OpCodes.Brfalse_S, codes[i + 1].operand)
                });
                i += 6;
                shouldCheckAmmoPatched = true;
            }
            else if (codes[i].Calls(mtd_canreload) && !canReloadPatched)
            {
                canReloadPatched = true;
                codes.InsertRange(i - 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[i - 2].ExtractLabels()),
                    CodeInstruction.Call(typeof(ModuleExtensions), nameof(ModuleExtensions.LoadModule), null, new []{typeof(ActionModuleMeleeShooter)}),
                    CodeInstruction.LoadField(typeof(ActionModuleMeleeShooter), nameof(ignoreAmmoCheck)),
                    new CodeInstruction(OpCodes.Brtrue_S, codes[i + 1].operand)
                });
                i += 4;
            }
        }

        if (!shouldCheckAmmoPatched)
        {
            throw new InvalidOperationException("MeleeShooter TryExecuteAction checkAmmo not patched");
        }

        if (!canReloadPatched)
        {
            throw new InvalidOperationException("MeleeShooter TryExecuteAction CanReload not patched");
        }

        return codes;
    }

    public bool ShouldCheckAmmo(MeleeShooterData customData)
    {
        return !ignoreAmmoCheck || customData.executionRequested;
    }

    private static bool CheckMelee(ItemActionRanged action, ItemActionRanged.ItemActionDataRanged rangedData)
    {
        var customData = rangedData.LoadModule<MeleeShooterData>();
        if (customData.executionRequested)
        {
            customData.executionRequested = false;
            return true;
        }

        // Animation requests must not retain the native burst state created before checkAmmo.
        action.ResetBurstShot(rangedData);
        if (customData.animationRequested)
        {
            rangedData.m_LastShotTime = Time.time;
            return false;
        }

        if (rangedData.invData.holdingEntity is EntityPlayerLocal player && player.inventory.holdingItemData.IsModuleContainerFor(out ItemModuleMultiItem.MultiItemInvData dataModule))
        {
            rangedData.m_LastShotTime = 0f;
            customData.animationRequested = ItemModuleMultiItem.CheckAltMelee(player, dataModule, false, 1, false);
            rangedData.m_LastShotTime = Time.time;
            if (customData.animationRequested)
            {
                ItemModuleMultiItem.CheckAltMelee(player, dataModule, true, 1, false);
            }
        }
        action.triggerReleased(rangedData, action.ActionIndex);
        return false;
    }

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    public void Postfix_ReadFrom(DynamicProperties _props)
    {
        ignoreAmmoCheck = false;
        _props.ParseBool("IgnoreAmmoCheck", ref ignoreAmmoCheck);
    }

    [HarmonyPatch(nameof(ItemActionRanged.ExecuteAction)), MethodTargetPrefix]
    public bool Prefix_ExecuteAction(ItemActionData _actionData, bool _bReleased, MeleeShooterData __customData)
    {
        if (!__customData.targets || !__customData.targets.IsAnimationSet)
        {
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemAction.CancelAction))]
    [HarmonyPostfix]
    public void Postfix_CancelAction(ItemActionData _actionData, MeleeShooterData __customData)
    {
        if (!__customData.animationRequested)
        {
            __customData.executionRequested = false;
        }
    }

    [HarmonyPatch(nameof(ItemActionRanged.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemActionData _data, MeleeShooterData __customData)
    {
        __customData.targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(_data.invData.holdingEntity);
        __customData.ResetRequest();
    }

    [HarmonyPatch(nameof(ItemActionRanged.IsActionRunning)), MethodTargetPostfix]
    public void Postfix_IsActionRunning(ref bool __result, MeleeShooterData __customData)
    {
        __result |= __customData.animationRequested || __customData.executionRequested;
    }

    [HarmonyPatch(nameof(ItemActionRanged.StartHolding)), MethodTargetPostfix]
    public void Postfix_StartHolding(MeleeShooterData __customData)
    {
        __customData.ResetRequest();
    }

    [HarmonyPatch(nameof(ItemActionRanged.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(MeleeShooterData __customData)
    {
        __customData.ResetRequest();
    }

    [HarmonyPatch(nameof(ItemActionRanged.onHoldingEntityFired)), MethodTargetPostfix]
    public void Postfix_onHoldingEntityFired(ItemActionData _actionData)
    {
        if (!_actionData.invData.holdingEntity.isEntityRemote)
        {
            _actionData.invData.holdingEntity.emodel.avatarController._resetTrigger(AvatarController.weaponFireHash);
        }
    }

    public class MeleeShooterData
    {
        public bool animationRequested;
        public bool executionRequested;
        public ItemInventoryData invData;
        public AnimationTargetsAbs targets;

        public MeleeShooterData(ItemInventoryData _inventoryData)
        {
            invData = _inventoryData;
        }

        public void ResetRequest()
        {
            animationRequested = false;
            executionRequested = false;
        }
    }
}
