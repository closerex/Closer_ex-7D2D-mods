using KFCommonUtilityLib.Scripts.Singletons;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Reflection;

public class CommonUtilityLibInit : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Log.Out(" Loading Patch: " + GetType());
        ModEvents.GameAwake.RegisterHandler(CommonUtilityPatch.InitShotStates);
        ModEvents.GameAwake.RegisterHandler(CustomEffectEnumManager.InitDefault);
        ModEvents.GameAwake.RegisterHandler(DelayLoadModuleManager.DelayLoad);
        ModEvents.GameAwake.RegisterHandler(ItemActionModuleManager.ClearOutputFolder);
        ModEvents.GameAwake.RegisterHandler(AssemblyLocator.Init);
        ModEvents.GameAwake.RegisterHandler(MultiActionUtils.SetMinEventArrays);
        ModEvents.GameStartDone.RegisterHandler(RegisterKFEnums);
        ModEvents.GameStartDone.RegisterHandler(AnimationRiggingManager.ParseItemIDs);
        //ModEvents.GameStartDone.RegisterHandler(CustomEffectEnumManager.PrintResults);
        //ModEvents.GameUpdate.RegisterHandler(CommonUtilityPatch.ForceUpdateGC);
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    private static void RegisterKFEnums()
    {
        CustomEnums.onSelfMagzineDeplete = CustomEffectEnumManager.RegisterOrGetTrigger("onSelfMagzineDeplete");
        CustomEnums.onReloadAboutToStart = CustomEffectEnumManager.RegisterOrGetTrigger("onReloadAboutToStart");

        CustomEnums.ReloadSpeedRatioFPV2TPV = CustomEffectEnumManager.RegisterOrGetPassive("ReloadSpeedRatioFPV2TPV");
        CustomEnums.RecoilSnappiness = CustomEffectEnumManager.RegisterOrGetPassive("RecoilSnappiness");
        CustomEnums.RecoilReturnSpeed = CustomEffectEnumManager.RegisterOrGetPassive("RecoilReturnSpeed");
        CustomEnums.ProjectileImpactDamagePercentBlock = CustomEffectEnumManager.RegisterOrGetPassive("ProjectileImpactDamagePercentBlock");
        CustomEnums.ProjectileImpactDamagePercentEntity = CustomEffectEnumManager.RegisterOrGetPassive("ProjectileImpactDamagePercentEntity");
        CustomEnums.RechargeDataValue = CustomEffectEnumManager.RegisterOrGetPassive("RechargeDataValue");
        CustomEnums.RechargeDataDecrease = CustomEffectEnumManager.RegisterOrGetPassive("RechargeDataDecrease");
        CustomEnums.RechargeDataInterval = CustomEffectEnumManager.RegisterOrGetPassive("RechargeDataInterval");
        CustomEnums.RechargeDataMaximum = CustomEffectEnumManager.RegisterOrGetPassive("RechargeDataMaximum");
        CustomEnums.ConsumptionValue = CustomEffectEnumManager.RegisterOrGetPassive("ConsumptionValue");
    }
}

