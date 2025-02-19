using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged)), ActionDataTarget(typeof(MultiBarrelData))]
public class ActionModuleMultiBarrel
{
    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationChanged(ItemActionData _data, MultiBarrelData __customData, ItemActionRanged __instance)
    {
        int actionIndex = _data.indexInEntityOfAction;
        string originalValue = false.ToString();
        __instance.Properties.ParseString("MuzzleIsPerRound", ref originalValue);
        __customData.muzzleIsPerRound = bool.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("MuzzleIsPerRound", originalValue, actionIndex));

        originalValue = false.ToString();
        __instance.Properties.ParseString("OneRoundMultiShot", ref originalValue);
        __customData.oneRoundMultishot = bool.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("OneRoundMultiShot", originalValue, actionIndex));

        originalValue = 1.ToString();
        __instance.Properties.ParseString("RoundsPerShot", ref originalValue);
        __customData.roundsPerShot = int.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("RoundsPerShot", originalValue, actionIndex));

        originalValue = 1.ToString();
        __instance.Properties.ParseString("BarrelCount", ref originalValue);
        __customData.barrelCount = int.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("BarrelCount", originalValue, actionIndex));

        //Log.Out($"MuzzleIsPerRound: {__customData.muzzleIsPerRound} OneRoundMultiShot: {__customData.oneRoundMultishot} RoundsPerShot: {__customData.roundsPerShot} BarrelCount: {__customData.barrelCount}");

        __customData.muzzles = new Transform[__customData.barrelCount];
        __customData.projectileJoints = new Transform[__customData.barrelCount];

        for (int i = 0; i < __customData.barrelCount; i++)
        {
            string muzzleName = _data.invData.itemValue.GetPropertyOverrideForAction($"MBMuzzle{i}_Name", $"MBMuzzle{i}", actionIndex);
            __customData.muzzles[i] = AnimationRiggingManager.GetTransformOverrideByName(_data.invData.model, muzzleName);
            string jointName = _data.invData.itemValue.GetPropertyOverrideForAction($"MBProjectileJoint{i}_Name", $"MBProjectileJoint{i}", actionIndex);
            __customData.projectileJoints[i] = AnimationRiggingManager.GetTransformOverrideByName(_data.invData.model, jointName);
        }

        int meta = MultiActionUtils.GetMetaByActionIndex(_data.invData.itemValue, actionIndex);
        __customData.SetCurrentBarrel(meta);
        ((ItemActionRanged.ItemActionDataRanged)_data).IsDoubleBarrel = false;
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemAction.StartHolding)), MethodTargetPrefix]
    public void Prefix_StartHolding_ItemActionLauncher(ItemActionData _data, ItemActionLauncher __instance, MultiBarrelData __customData)
    {
        ItemActionLauncher.ItemActionDataLauncher launcherData = _data as ItemActionLauncher.ItemActionDataLauncher;
        launcherData.projectileJoint = __customData.projectileJoints[0];
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemAction.StartHolding)), MethodTargetPostfix]
    public void Postfix_StartHolding_ItemActionLauncher(ItemActionData _data, ItemActionLauncher __instance, MultiBarrelData __customData)
    {
        ItemActionLauncher.ItemActionDataLauncher launcherData = _data as ItemActionLauncher.ItemActionDataLauncher;
        if (launcherData?.projectileInstance != null && __customData.oneRoundMultishot && __customData.roundsPerShot > 1)
        {
            int count = launcherData.projectileInstance.Count;
            int times = __customData.roundsPerShot - 1;
            for (int i = 0; i < times; i++)
            {
                launcherData.projectileJoint = __customData.projectileJoints[i + 1];
                for (int j = 0; j < count; j++)
                {
                    launcherData.projectileInstance.Add(__instance.instantiateProjectile(_data));
                }
            }
        }
        launcherData.projectileJoint = __customData.projectileJoints[__customData.curBarrelIndex];
    }

    [HarmonyPatch(nameof(ItemActionRanged.getUserData)), MethodTargetPostfix]
    public void Postfix_getUserData(MultiBarrelData __customData, ref int __result)
    {
        __result |= ((byte)__customData.curBarrelIndex) << 8;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemAction.ItemActionEffects)), MethodTargetPrefix]
    public bool Prefix_ItemActionEffects_ItemActionRanged(ItemActionData _actionData, int _userData, int _firingState, MultiBarrelData __customData)
    {
        ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
        if (rangedData != null && _firingState != 0)
        {
            byte index = (byte)(_userData >> 8);
            rangedData.muzzle = __customData.muzzles[index];
            __customData.SetAnimatorParam(index);
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemAction.ItemActionEffects)), MethodTargetPrefix]
    public bool Prefix_ItemActionEffects_ItemActionLauncher(ItemActionData _actionData, int _userData, int _firingState, MultiBarrelData __customData)
    {
        ItemActionLauncher.ItemActionDataLauncher launcherData = _actionData as ItemActionLauncher.ItemActionDataLauncher;
        if (launcherData != null)
        {
            launcherData.projectileJoint = __customData.projectileJoints[(byte)(_userData >> 8)];
        }
        return Prefix_ItemActionEffects_ItemActionRanged(_actionData, _userData, _firingState, __customData);
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemAction.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemActionRanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var mtd_getmax = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.GetMaxAmmoCount));
        var mtd_consume = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.ConsumeAmmo));
        var prop_instance = AccessTools.PropertyGetter(typeof(IModuleContainerFor<MultiBarrelData>), nameof(IModuleContainerFor<MultiBarrelData>.Instance));

        Label loopStart = generator.DefineLabel();
        Label loopCondi = generator.DefineLabel();
        LocalBuilder lbd_data_module = generator.DeclareLocal(typeof(ActionModuleMultiBarrel.MultiBarrelData));
        LocalBuilder lbd_i = generator.DeclareLocal(typeof(int));
        LocalBuilder lbd_rounds = generator.DeclareLocal(typeof(int));
        for (int i = 0; i < codes.Count; i++)
        {
            //prepare loop and store local variables
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 6)
            {
                codes[i + 1].WithLabels(loopStart);
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<MultiBarrelData>)),
                    new CodeInstruction(OpCodes.Callvirt, prop_instance),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_data_module),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_i),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                    CodeInstruction.LoadField(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.roundsPerShot)),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_rounds),
                    new CodeInstruction(OpCodes.Br_S, loopCondi),
                });
                i += 11;
            }
            //one round multi shot check
            else if (codes[i].Calls(mtd_consume))
            {
                Label lbl = generator.DefineLabel();
                codes[i - 5].WithLabels(lbl);
                codes.InsertRange(i - 5, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                    CodeInstruction.LoadField(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.oneRoundMultishot)),
                    new CodeInstruction(OpCodes.Brfalse_S, lbl),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_i),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Bgt_S, codes[i - 3].operand)
                });
                i += 6;
            }
            //loop conditions and cycle barrels
            else if (codes[i].Calls(mtd_getmax))
            {
                Label lbl_pre = generator.DefineLabel();
                Label lbl_post = generator.DefineLabel();
                CodeInstruction origin = codes[i - 2];
                codes.InsertRange(i - 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module).WithLabels(origin.ExtractLabels()),
                    CodeInstruction.LoadField(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.muzzleIsPerRound)),
                    new CodeInstruction(OpCodes.Brfalse_S, lbl_pre),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                    CodeInstruction.Call(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.CycleBarrels)),
                    new CodeInstruction(OpCodes.Ldloc_S, 6).WithLabels(lbl_pre),
                    CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.inventory)),
                    CodeInstruction.Call(typeof(Inventory), nameof(Inventory.CallOnToolbeltChangedInternal)),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_i),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Add),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_i),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_i).WithLabels(loopCondi),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_rounds),
                    new CodeInstruction(OpCodes.Blt_S, loopStart),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                    CodeInstruction.LoadField(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.muzzleIsPerRound)),
                    new CodeInstruction(OpCodes.Brtrue_S, lbl_post),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                    CodeInstruction.Call(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.CycleBarrels))
                });
                origin.WithLabels(lbl_post);
                break;
            }
        }

        return codes;
    }

    private static void LogInfo(int cur, int max) => Log.Out($"max rounds {max}, cur {cur}");

    public class MultiBarrelData
    {
        public ItemInventoryData invData;
        public int actionIndex;
        public ActionModuleMultiBarrel module;
        public bool muzzleIsPerRound;
        public bool oneRoundMultishot;
        public int roundsPerShot;
        public int barrelCount;
        public int curBarrelIndex;
        public Transform[] muzzles;
        public Transform[] projectileJoints;

        public MultiBarrelData(ItemInventoryData _invData, int _indexInEntityOfAction, ActionModuleMultiBarrel _module)
        {
            invData = _invData;
            actionIndex = _indexInEntityOfAction;
            module = _module;
        }

        public void CycleBarrels()
        {
            curBarrelIndex = ++curBarrelIndex >= barrelCount ? 0 : curBarrelIndex;
            //Log.Out($"cycle barrel index {curBarrelIndex}");
        }

        public void SetCurrentBarrel(int roundLeft)
        {
            if (muzzleIsPerRound)
            {
                int totalSwitches;
                if (oneRoundMultishot)
                {
                    totalSwitches = roundLeft * roundsPerShot;
                }
                else
                {
                    totalSwitches = roundLeft;
                }
                int lastCycleSwitches = totalSwitches % barrelCount;
                int barrelGroup = barrelCount / roundsPerShot;
                curBarrelIndex = (barrelCount - lastCycleSwitches) / barrelGroup * barrelGroup;
            }
            else
            {
                if (oneRoundMultishot)
                {
                    curBarrelIndex = barrelCount - (roundLeft % barrelCount);
                }
                else
                {
                    curBarrelIndex = barrelCount - ((roundLeft + 1) / roundsPerShot) % barrelCount;
                }
            }
            if (curBarrelIndex >= barrelCount)
            {
                curBarrelIndex = 0;
            }
            SetAnimatorParam(curBarrelIndex);
            //Log.Out($"set barrel index {curBarrelIndex}");
        }

        public void SetAnimatorParam(int barrelIndex)
        {
            invData.holdingEntity.emodel.avatarController.UpdateInt("barrelIndex", barrelIndex, true);
            //Log.Out($"set param index {barrelIndex}");
        }
    }
}

[HarmonyPatch]
public class MultiBarrelPatches
{

    [HarmonyPatch(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.OnStateEnter))]
    [HarmonyPostfix]
    private static void Postfix_OnStateEnter_AnimatorRangedReloadState(AnimatorRangedReloadState __instance)
    {
        ItemActionLauncher.ItemActionDataLauncher launcherData = __instance.actionData as ItemActionLauncher.ItemActionDataLauncher;
        if (launcherData != null && launcherData is IModuleContainerFor<ActionModuleMultiBarrel.MultiBarrelData> dataModule && dataModule.Instance.oneRoundMultishot && dataModule.Instance.roundsPerShot > 1)
        {
            int count = launcherData.projectileInstance.Count;
            int times = dataModule.Instance.roundsPerShot - 1;
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < times; j++)
                {
                    launcherData.projectileJoint = dataModule.Instance.projectileJoints[j + 1];
                    launcherData.projectileInstance.Insert(i * (times + 1) + j + 1, ((ItemActionLauncher)__instance.actionRanged).instantiateProjectile(launcherData));
                }
            }
        }
    }

    [HarmonyPatch(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.OnStateExit))]
    [HarmonyPostfix]
    private static void Postfix_OnStateExit_AnimatorRangedReloadState(AnimatorRangedReloadState __instance)
    {
        if (__instance.actionData is IModuleContainerFor<ActionModuleMultiBarrel.MultiBarrelData> dataModule)
        {
            dataModule.Instance.SetCurrentBarrel(__instance.actionData.invData.itemValue.Meta);
        }
    }
}