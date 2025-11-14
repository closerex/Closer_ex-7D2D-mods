using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(MeleeShooterData))]
public class ActionModuleMeleeShooter
{
    public bool ignoreAmmoCheck;
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_ExecuteAction(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var fld_started = AccessTools.Field(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.burstShotStarted));
        var mtd_checkammo = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.checkAmmo));
        var mtd_canreload = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.CanReload));
        bool canReloadPatched = false;

        for (int i = 1; i < codes.Count; i++)
        {
            if (codes[i].StoresField(fld_started) && codes[i - 1].opcode == OpCodes.Ldc_I4_1)
            {
                var lbl = generator.DefineLabel();
                var ins = codes[i - 2];
                codes.InsertRange(i - 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0).WithLabels(ins.ExtractLabels()),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldarg_2),
                    CodeInstruction.Call(typeof(ActionModuleMeleeShooter), nameof(CheckMelee)),
                    new CodeInstruction(OpCodes.Brtrue_S, lbl),
                    new CodeInstruction(OpCodes.Ret),
                });
                ins.WithLabels(lbl);
                i += 6;
            }
            else if (codes[i].Calls(mtd_checkammo))
            {
                codes.InsertRange(i - 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[i - 2].ExtractLabels()),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ActionModuleMeleeShooter>)),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<ActionModuleMeleeShooter>), nameof(IModuleContainerFor<ActionModuleMeleeShooter>.Instance))),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<MeleeShooterData>)),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<MeleeShooterData>), nameof(IModuleContainerFor<MeleeShooterData>.Instance))),
                    CodeInstruction.Call(typeof(ActionModuleMeleeShooter), nameof(ActionModuleMeleeShooter.ShouldCheckAmmo)),
                    new CodeInstruction(OpCodes.Brfalse_S, codes[i + 1].operand)
                });
                i += 8;
            }
            else if (codes[i].Calls(mtd_canreload) && !canReloadPatched)
            {
                canReloadPatched = true;
                codes.InsertRange(i - 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[i - 2].ExtractLabels()),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ActionModuleMeleeShooter>)),
                    CodeInstruction.LoadField(typeof(ActionModuleMeleeShooter), nameof(ActionModuleMeleeShooter.ignoreAmmoCheck)),
                    new CodeInstruction(OpCodes.Brtrue_S, codes[i + 1].operand)
                });
                i += 4;
            }
        }

        return codes;
    }

    public bool ShouldCheckAmmo(MeleeShooterData customData)
    {
        return !ignoreAmmoCheck || customData.executionRequested;
    }

    private static bool CheckMelee(ItemActionRanged __instance, ItemActionRanged.ItemActionDataRanged _rangedData, bool bReleased)
    {
        if (bReleased)
        {
            return false;
        }

        var customData = ((IModuleContainerFor<MeleeShooterData>)_rangedData).Instance;
        if (customData.executionRequested)
        {
            customData.executionRequested = false;
            return true;
        }
        else if (customData.animationRequested)
        {
            _rangedData.m_LastShotTime = Time.time;
            return false;
        }

        if (_rangedData.invData.holdingEntity is EntityPlayerLocal player && player.inventory.holdingItemData is IModuleContainerFor<ItemModuleMultiItem.MultiItemInvData> dataModule)
        {
            _rangedData.m_LastShotTime = 0f;
            customData.animationRequested = ItemModuleMultiItem.CheckAltMelee(player, dataModule.Instance, false, 1, false);
            _rangedData.m_LastShotTime = Time.time;
            if (customData.animationRequested)
            {
                ItemModuleMultiItem.CheckAltMelee(player, dataModule.Instance, true, 1, false);
            }
        }
        __instance.triggerReleased(_rangedData, __instance.ActionIndex);
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
            this.invData = _inventoryData;
        }

        public void ResetRequest()
        {
            animationRequested = false;
            executionRequested = false;
        }
    }
}