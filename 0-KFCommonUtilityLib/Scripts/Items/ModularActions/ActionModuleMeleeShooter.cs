using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using static ItemModuleMultiItem;

[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(MeleeShooterData))]
public class ActionModuleMeleeShooter
{
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_ExecuteAction(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var fld_started = AccessTools.Field(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.burstShotStarted));

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
                break;
            }
        }

        return codes;
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
            return false;
        }

        if (_rangedData.invData.holdingEntity is EntityPlayerLocal player && player.inventory.holdingItemData is IModuleContainerFor<MultiItemInvData> dataModule)
        {
            customData.animationRequested = ItemModuleMultiItem.CheckAltMelee(player, dataModule.Instance, false, player.playerInput, 1, false);
            if (customData.animationRequested)
            {
                ItemModuleMultiItem.CheckAltMelee(player, dataModule.Instance, true, player.playerInput, 1, false);
            }
        }
        __instance.triggerReleased(_rangedData, __instance.ActionIndex);
        return false;
    }

    [HarmonyPatch(nameof(ItemActionRanged.ExecuteAction)), MethodTargetPrefix]
    public bool Prefix_ExecuteAction(ItemActionData _actionData, bool _bReleased, MeleeShooterData __customData)
    {
        if (!__customData.targets || __customData.targets.Destroyed || !__customData.targets.IsAnimationSet)
        {
            return false;
        }
        return true;
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

        public MeleeShooterData(ItemActionData actionData, ItemInventoryData invData, int actionIndex, ActionModuleMeleeShooter module)
        {
            this.invData = invData;
        }

        public void ResetRequest()
        {
            animationRequested = false;
            executionRequested = false;
        }
    }
}