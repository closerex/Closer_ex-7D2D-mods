using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using KFCommonUtilityLib.Scripts.Utilities;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged), typeof(MultiBarrelData))]
public class ActionModuleMultiBarrel
{
    [MethodTargetPostfix(nameof(ItemAction.OnModificationsChanged))]
    private void Postfix_OnModificationChanged(ItemActionData _data, MultiBarrelData __customData, ItemActionRanged __instance)
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

    [MethodTargetPrefix(nameof(ItemAction.StartHolding), typeof(ItemActionLauncher))]
    private void Prefix_StartHolding_ItemActionLauncher(ItemActionData _data, ItemActionLauncher __instance, MultiBarrelData __customData)
    {
        ItemActionLauncher.ItemActionDataLauncher launcherData = _data as ItemActionLauncher.ItemActionDataLauncher;
        launcherData.projectileJoint = __customData.projectileJoints[0];
    }

    [MethodTargetPostfix(nameof(ItemAction.StartHolding), typeof(ItemActionLauncher))]
    private void Postfix_StartHolding_ItemActionLauncher(ItemActionData _data, ItemActionLauncher __instance, MultiBarrelData __customData)
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

    [MethodTargetPostfix(nameof(ItemActionRanged.getUserData))]
    private void Postfix_getUserData(MultiBarrelData __customData, ref int __result)
    {
        __result |= ((byte)__customData.curBarrelIndex) << 8;
    }

    [MethodTargetPrefix(nameof(ItemAction.ItemActionEffects), typeof(ItemActionRanged))]
    private bool Prefix_ItemActionEffects_ItemActionRanged(ItemActionData _actionData, int _userData, int _firingState, MultiBarrelData __customData)
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

    [MethodTargetPrefix(nameof(ItemAction.ItemActionEffects), typeof(ItemActionLauncher))]
    private bool Prefix_ItemActionEffects_ItemActionLauncher(ItemActionData _actionData, int _userData, int _firingState, MultiBarrelData __customData)
    {
        ItemActionLauncher.ItemActionDataLauncher launcherData = _actionData as ItemActionLauncher.ItemActionDataLauncher;
        if (launcherData != null)
        {
            launcherData.projectileJoint = __customData.projectileJoints[(byte)(_userData >> 8)];
        }
        return Prefix_ItemActionEffects_ItemActionRanged(_actionData, _userData, _firingState, __customData);
    }

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
