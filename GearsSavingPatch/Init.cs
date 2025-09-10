using Gears.SettingsManager;
using Gears.SettingsManager.Settings;
using Gears.UI;
using HarmonyLib;
using KFCommonUtilityLib.Gears;
using System.Reflection;

namespace GearsSavingPatch
{
    public class Init : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (inited)
                return;
            inited = true;
            Log.Out("Loading Patch: " + GetType());
            var harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch]
    public static class GearsSavePatch
    {
        private static FieldInfo gearsModField = AccessTools.Field(typeof(GlobalModSettings), "gearsMod");
        [HarmonyPatch(typeof(GlobalModSettings), nameof(GlobalModSettings.SaveSettings))]
        [HarmonyPostfix]
        private static void Postfix_GlobalModSettings_SaveSettings(GearsMod ___gearsMod)
        {
            if (___gearsMod.Mod.Name == "CommonUtilityLib")
            {
                GearsImpl.SaveGlobalSettings(___gearsMod.GlobalSettings);
                Log.Out("GearsSavingPatch: GlobalModSettings saved.");
            }
        }

        [HarmonyPatch(typeof(XUiC_ModSettings), "ClearSettings")]
        [HarmonyPostfix]
        private static void Postfix_XUiC_ModSettings_ClearSettings(GlobalModSettings ___modSettings)
        {
            if (___modSettings != null)
            {
                var gearsMod = (GearsMod)gearsModField.GetValue(___modSettings);
                if (gearsMod.Mod.Name == "CommonUtilityLib")
                {
                    GearsImpl.CloseGlobalSettings(___modSettings);
                    ___modSettings.SaveSettings();
                    Log.Out("GearsSavingPatch: XUiC_ModSettings GlobalSettings cleared: " + gearsMod.Mod.Name);
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_ModSettings), nameof(XUiC_ModSettings.GlobalSettings), MethodType.Setter)]
        [HarmonyPrefix]
        private static void Prefix_XUiC_ModSettings_GlobalSettings(GlobalModSettings ___modSettings, GlobalModSettings value)
        {
            if (___modSettings != null)
            {
                var curGearsMod = ___modSettings == null ? null : (GearsMod)gearsModField.GetValue(___modSettings);
                var nextGearsMod = value == null ? null : (GearsMod)gearsModField.GetValue(value);
                if (curGearsMod?.Mod?.Name == "CommonUtilityLib" && curGearsMod?.Mod?.Name != nextGearsMod.Mod.Name)
                {
                    GearsImpl.CloseGlobalSettings(___modSettings);
                    ___modSettings.SaveSettings();
                    Log.Out("GearsSavingPatch: XUiC_ModSettings GlobalSettings closed on opening: " + nextGearsMod.Mod.Name);
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_ModSettings), nameof(XUiC_ModSettings.GlobalSettings), MethodType.Setter)]
        [HarmonyPostfix]
        private static void Postfix_XUiC_ModSettings_GlobalSettings(GlobalModSettings ___modSettings)
        {
            if (___modSettings != null)
            {
                var gearsMod = (GearsMod)gearsModField.GetValue(___modSettings);
                if (gearsMod.Mod.Name == "CommonUtilityLib")
                {
                    GearsImpl.OpenGlobalSettings(___modSettings);
                    Log.Out("GearsSavingPatch: XUiC_ModSettings GlobalSettings opened: " + gearsMod.Mod.Name);
                }
            }
        }
    }
}
