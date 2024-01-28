using KFCommonUtilityLib.Scripts.Singletons;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Reflection;
using System.Runtime.InteropServices;

public class CommonUtilityLibInit : IModApi
{
    private static bool inited = false;
    public void InitMod(Mod _modInstance)
    {
        if (inited)
            return;
        inited = true;
        Log.Out(" Loading Patch: " + GetType());
        unsafe
        {
            Log.Out($"size of MultiActionIndice: {sizeof(MultiActionIndice)} marshal size: {Marshal.SizeOf<MultiActionIndice>()}");
        }
        CustomEffectEnumManager.RegisterEnumType<MinEventTypes>();
        CustomEffectEnumManager.RegisterEnumType<PassiveEffects>();
        ModEvents.GameAwake.RegisterHandler(CommonUtilityPatch.InitShotStates);
        ModEvents.GameAwake.RegisterHandler(CustomEffectEnumManager.InitDefault);
        ModEvents.GameAwake.RegisterHandler(DelayLoadModuleManager.DelayLoad);
        ModEvents.GameAwake.RegisterHandler(ItemActionModuleManager.ClearOutputFolder);
        //ModEvents.GameAwake.RegisterHandler(AssemblyLocator.Init);
        ModEvents.GameAwake.RegisterHandler(MultiActionUtils.SetMinEventArrays);
        ModEvents.GameStartDone.RegisterHandler(RegisterKFEnums);
        ModEvents.GameStartDone.RegisterHandler(AnimationRiggingManager.ParseItemIDs);
        ModEvents.GameStartDone.RegisterHandler(MultiActionManager.Cleanup);
        //ModEvents.GameStartDone.RegisterHandler(CustomEffectEnumManager.PrintResults);
        //ModEvents.GameUpdate.RegisterHandler(CommonUtilityPatch.ForceUpdateGC);
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    private static void RegisterKFEnums()
    {
        CustomEnums.onSelfMagzineDeplete = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onSelfMagzineDeplete");
        CustomEnums.onReloadAboutToStart = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onReloadAboutToStart");

        CustomEnums.ReloadSpeedRatioFPV2TPV = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("ReloadSpeedRatioFPV2TPV");
        CustomEnums.RecoilSnappiness = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("RecoilSnappiness");
        CustomEnums.RecoilReturnSpeed = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("RecoilReturnSpeed");
        CustomEnums.ProjectileImpactDamagePercentBlock = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("ProjectileImpactDamagePercentBlock");
        CustomEnums.ProjectileImpactDamagePercentEntity = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("ProjectileImpactDamagePercentEntity");
        CustomEnums.RechargeDataValue = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("RechargeDataValue");
        CustomEnums.RechargeDataDecrease = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("RechargeDataDecrease");
        CustomEnums.RechargeDataInterval = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("RechargeDataInterval");
        CustomEnums.RechargeDataMaximum = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("RechargeDataMaximum");
        CustomEnums.ConsumptionValue = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("ConsumptionValue");
    }
}

