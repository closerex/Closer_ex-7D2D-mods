using Gears.SettingsManager;
using Gears.SettingsManager.Settings;
using GearsAPI.Settings;
using GearsAPI.Settings.Global;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UniLinq;

namespace GearsSettingsSave
{
    public class Init : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (inited)
            {
                return;
            }
            inited = true;

            ModEvents.GameAwake.RegisterHandler(GearsPatches.LoadModSettingsFromJson);

            Log.Out(" Loading Patch: " + GetType());
            var harmony = new HarmonyLib.Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch]
    public static class GearsPatches
    {
        private static readonly string SavePath = Path.Combine(GameIO.GetUserGameDataDir(), "GearsSettings");

        [HarmonyPatch(typeof(GlobalModSettings), nameof(GlobalModSettings.SaveSettings), new Type[] { })]
        [HarmonyPostfix]
        private static void Postfix_GlobalModSettings_SaveSettings(GearsMod ___gearsMod)
        {
            SaveModSettingsToJson(___gearsMod);
        }

        internal static void LoadModSettingsFromJson()
        {
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }

            foreach (var mod in GearsSettingsManager.GetMods())
            {
                if (!mod.HasGlobalSettings())
                {
                    continue;
                }
                string settingFilePath = Path.Combine(SavePath, mod.Mod.Name, "ModSettings.json");
                if (File.Exists(settingFilePath))
                {
                    Dictionary<string, IGlobalModSetting> dict_settings = mod.GlobalSettings.GetAllGlobalSettings().ToDictionary(setting => setting.UniqueSettingName());
                    using (StreamReader reader = File.OpenText(settingFilePath))
                    {
                        JObject saveObj = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
                        foreach (JProperty tabProp in saveObj.Properties())
                        {
                            var tab = mod.GlobalSettings.GetTab(tabProp.Name);
                            if (tab != null && tabProp.Value is JObject tabObj)
                            {
                                foreach (JProperty catProp in tabObj.Properties())
                                {
                                    var cat = tab.GetCategory(catProp.Name);
                                    if (cat != null && catProp.Value is JObject catObj)
                                    {
                                        foreach (JProperty settingProp in catObj.Properties())
                                        {
                                            var setting = cat.GetSetting(settingProp.Name);
                                            if(setting != null)
                                            {
                                                switch (setting)
                                                {
                                                    case IGlobalValueSetting globalValueSetting:
                                                        globalValueSetting.CurrentValue = (string)settingProp.Value;
                                                        break;
                                                    case ColorSelectorSetting colorSelectorSetting:
                                                        colorSelectorSetting.CurrentColor = StringParsers.ParseHexColor((string)settingProp.Value);
                                                        break;
                                                    default:
                                                        break;
                                                }
                                                dict_settings.Remove(setting.UniqueSettingName());
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    foreach (var setting in dict_settings.Values)
                    {
                        ResetSetting(setting);
                    }
                }
                else
                {
                    foreach (var setting in mod.GlobalSettings.GetAllGlobalSettings())
                    {
                        ResetSetting(setting);
                    }
                }
                mod.GlobalSettings.SaveSettings();
            }
        }

        private static void ResetSetting(IGlobalModSetting setting)
        {
            switch (setting)
            {
                case IGlobalValueSetting globalValueSetting:
                    globalValueSetting.CurrentValue = globalValueSetting.DefaultValue;
                    break;
                case ColorSelectorSetting colorSelectorSetting:
                    colorSelectorSetting.CurrentColor = colorSelectorSetting.DefaultColor;
                    break;
                default:
                    break;
            }
        }

        private static string UniqueSettingName(this IGlobalModSetting setting)
        {
            return (setting.Tab?.Name ?? "") + "_" + (setting.Category?.Name ?? "") + "_" + setting.Name;
        }

        private static void SaveAllModSettingsToJson()
        {
            foreach (var mod in GearsSettingsManager.GetMods())
            {
                if (mod.HasGlobalSettings())
                { 
                    SaveModSettingsToJson(mod);
                }
            }
        }

        private static void SaveModSettingsToJson(IGearsMod mod)
        {
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }

            JObject saveObj = new JObject();
            foreach (var tab in mod.GlobalSettings.GetTabs())
            {
                JObject tabObj = new JObject();
                saveObj.Add(new JProperty(tab.Name, tabObj));
                foreach (var cat in tab.GetAllCategories())
                {
                    JObject catObj = new JObject();
                    tabObj.Add(new JProperty(cat.Name, catObj));
                    foreach (var setting in cat.GetAllSettings())
                    {
                        string value = setting switch
                        {
                            IGlobalValueSetting globalValueSetting => globalValueSetting.CurrentValue,
                            ColorSelectorSetting colorSelectorSetting => colorSelectorSetting.CurrentColor.ToHexCode(),
                            _ => null
                        };
                        if (value != null)
                        {
                            catObj.Add(new JProperty(setting.Name, value));
                        }
                    }
                }
            }
            string path = Path.Combine(SavePath, mod.Mod.Name);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            File.WriteAllText(Path.Combine(path, "ModSettings.json"), saveObj.ToString());
        }
    }
}
