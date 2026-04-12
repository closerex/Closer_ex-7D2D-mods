using GearsAPI.Settings;
using GearsAPI.Settings.Global;
using GearsAPI.Settings.World;
using HarmonyLib;
using KFCommonUtilityLib.KFAttached.Render;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public static class SharedAssets
    {
        public static Shader MagnifyScopeShader { get; private set; }
        public static Material DefaultLaserDotMaterial { get; private set; }

        public static void InitAssets()
        {
            if (!MagnifyScopeShader)
            {
                MagnifyScopeShader = LoadManager.LoadAsset<Shader>("#@modfolder(CommonUtilityLib):Resources/kf_shared_assets.unity3d?PIPScopeAimBlend.shadergraph", null, null, false, true).Asset;
            }
            if (!DefaultLaserDotMaterial)
            {
                DefaultLaserDotMaterial = LoadManager.LoadAsset<Material>("#@modfolder(CommonUtilityLib):Resources/kf_shared_assets.unity3d?DefaultLaserDotMaterial.mat", null, null, false, true).Asset;
            }
        }
    }
    public static class KFLibEvents
    {
        public static event Action onXmlLoadingStart;
        public static event Action onXmlLoadingFinish;

        internal static void XmlLoadingStart()
        {
            onXmlLoadingStart?.Invoke();
        }

        internal static void XmlLoadingFinish()
        {
            onXmlLoadingFinish?.Invoke();
        }
    }

    public class CommonUtilityLibInit : IModApi
    {
        private static bool inited = false;
        internal static HarmonyLib.Harmony HarmonyInstance { get; private set; }
        public void InitMod(Mod _modInstance)
        {
            if (inited)
                return;
            inited = true;
            SharedAssets.InitAssets();
            Log.Out(" Loading Patch: " + GetType());
            unsafe
            {
                Log.Out($"size of MultiActionIndice: {sizeof(MultiActionIndice)} marshal size: {Marshal.SizeOf<MultiActionIndice>()}");
                Log.Out($"{AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.StartHolding)).FullDescription()}");
            }
            //QualitySettings.streamingMipmapsMemoryBudget = 4096;
            DelayLoadModuleManager.RegisterDelayloadDll("FullautoLauncher", "FullautoLauncherAnimationRiggingCompatibilityPatch");
            DelayLoadModuleManager.RegisterDelayloadDll("Quartz", "QuartzUIPatch");
            DelayLoadModuleManager.RegisterDelayloadDll("Gears", "GearsSavingPatch");
            DelayLoadModuleManager.RegisterDelayloadDll("Rainstorm", "RainstormPatches");
            DelayLoadModuleManager.RegisterDelayloadDll("Torch", "TorchPatches");
            DelayLoadModuleManager.RegisterDelayloadDll("CustomFPVFov", "CustomAimFovCorrectionPatch");
            DelayLoadModuleManager.RegisterDelayloadDll("FPVLegs", "FPVLegsPiPCameraPatches");
            //DelayLoadModuleManager.RegisterDelayloadDll("SMXcore", "SMXMultiActionCompatibilityPatch");
            //DelayLoadModuleManager.RegisterDelayloadDll("SCore", "SCoreEntityHitCompatibilityPatch");
            CustomEffectEnumManager.RegisterEnumType<MinEventTypes>(true);
            CustomEffectEnumManager.RegisterEnumType<PassiveEffects>();

            ModuleManagers.Init();

            //ModEvents.GameAwake.RegisterHandler(CommonUtilityPatch.InitShotStates);
            ModEvents.GameAwake.RegisterHandler(CustomEffectEnumManager.InitDefault);
            ModEvents.GameAwake.RegisterHandler(DelayLoadModuleManager.DelayLoad);
            //ModEvents.GameAwake.RegisterHandler(AssemblyLocator.Init);
            ModEvents.GameAwake.RegisterHandler(MultiActionUtils.SetMinEventArrays);
            ModEvents.GameStartDone.RegisterHandler(MultiActionManager.PostloadCleanup);
            //ModEvents.GameStartDone.RegisterHandler(CustomEffectEnumManager.PrintResults);
            HarmonyInstance = new HarmonyLib.Harmony(GetType().ToString());
            HarmonyInstance.PatchAllUncategorized(Assembly.GetExecutingAssembly());
            //Test.Do();
        }

        public static void RegisterKFEnums()
        {
            CustomEnums.onSelfMagzineDeplete = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onSelfMagzineDeplete");
            CustomEnums.onReloadAboutToStart = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onReloadAboutToStart");
            CustomEnums.onPartialReloadAmmoSuccess = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onPartialReloadAmmoSuccess");
            CustomEnums.onPartialReloadAmmoFail = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onPartialReloadAmmoFail");
            CustomEnums.onRechargeValueUpdate = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onRechargeValueUpdate");
            CustomEnums.onSelfItemSwitchMode = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onSelfItemSwitchMode");
            CustomEnums.onSelfBurstModeChanged = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onSelfBurstModeChanged");
            CustomEnums.onSelfFirstCVarSync = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onSelfFirstCVarSync");
            CustomEnums.onSelfHoldingItemAssemble = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onSelfHoldingItemAssemble");
            CustomEnums.onSelfBlockingStart = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onSelfBlockingStart");
            CustomEnums.onSelfBlockingStop = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onSelfBlockingStop");
            CustomEnums.onSelfBlockingExit = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onSelfBlockingExit");
            CustomEnums.onSelfBlockingDamage = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onSelfBlockingDamage");
            CustomEnums.onSelfParryingDamage = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onSelfParryingDamage");
            CustomEnums.onAnimatorStateEntered = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onAnimatorStateEntered");
            CustomEnums.onAnimatorStateUpdate = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onAnimatorStateUpdate");
            CustomEnums.onAnimatorStateExit = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onAnimatorStateExit");
            CustomEnums.onAnimationEventTrigger = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onAnimationEventTrigger");
            CustomEnums.onThrowItemSelected = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onThrowItemSelected");
            CustomEnums.onThrowItemSwapped = CustomEffectEnumManager.RegisterOrGetEnum<MinEventTypes>("onThrowItemSwapped");

            CustomEnums.ReloadSpeedRatioFPV2TPV = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("ReloadSpeedRatioFPV2TPV");
            CustomEnums.RecoilSnappiness = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("RecoilSnappiness");
            CustomEnums.RecoilReturnSpeed = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("RecoilReturnSpeed");
            CustomEnums.PartialReloadCount = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("PartialReloadCount");

            CustomEnums.CustomTaggedEffect = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("CustomTaggedEffect");
            CustomEnums.KickDegreeHorizontalModifier = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("KickDegreeHorizontalModifier");
            CustomEnums.KickDegreeVerticalModifier = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("KickDegreeVerticalModifier");
            CustomEnums.WeaponErgonomics = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("WeaponErgonomics");
            CustomEnums.RecoilCameraShakeStrength = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("RecoilCameraShakeStrength");
            CustomEnums.BurstShotInterval = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("BurstShotInterval");
            CustomEnums.MaxWeaponSpread = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("MaxWeaponSpread");
            CustomEnums.HoldingItemDamageResistance = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("HoldingItemDamageResistance");
            CustomEnums.WeaponHolsterSpeed = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>("WeaponHolsterSpeed");
        }
    }
}

namespace KFCommonUtilityLib.Gears
{
    public class GearsImpl : IGearsModApi
    {
        public static event Action<IModGlobalSettings> OnGlobalSettingsSaved;
        public static event Action<IModGlobalSettings> OnGlobalSettingsOpened;
        public static event Action<IModGlobalSettings> OnGlobalSettingsClosed;

        public static void SaveGlobalSettings(IModGlobalSettings modSettings)
        {
            OnGlobalSettingsSaved?.Invoke(modSettings);
        }

        public static void OpenGlobalSettings(IModGlobalSettings modSettings)
        {
            OnGlobalSettingsOpened?.Invoke(modSettings);
        }

        public static void CloseGlobalSettings(IModGlobalSettings modSettings)
        {
            OnGlobalSettingsClosed?.Invoke(modSettings);
        }

        public void InitMod(IGearsMod modInstance)
        {

        }

        public void OnGlobalSettingsLoaded(IModGlobalSettings modSettings)
        {
            RecoilManager.InitRecoilSettings(modSettings);
            CameraAnimationEvents.InitModSettings(modSettings);
            InspectSettings.InitSettings(modSettings);
            AimingSettings.InitSettings(modSettings);
            PiPCameraSettings.InitSettings(modSettings);
        }

        public void OnWorldSettingsLoaded(IModWorldSettings worldSettings)
        {

        }
    }

    public enum SyncAAQualityMode
    {
        Disabled,
        Antialiasing,
        Upscaling
    }
    public class PiPCameraSettings
    {
        public static SyncAAQualityMode SyncAAQuality { get; private set; }

        public static void InitSettings(IModGlobalSettings modSettings)
        {
            var tab = modSettings.GetTab("MiscSettings");
            var category = tab.GetCategory("PiPCamera");
            var syncAASetting = category.GetSetting<ISelectorGlobalSetting>("SyncAAQuality");
            SyncAAQuality = EnumUtils.Parse<SyncAAQualityMode>(syncAASetting.CurrentValue);
            syncAASetting.OnSettingChanged += (s, e) =>
            {
                SyncAAQuality = EnumUtils.Parse<SyncAAQualityMode>(syncAASetting.CurrentValue);
            }; 
        }
    }
}