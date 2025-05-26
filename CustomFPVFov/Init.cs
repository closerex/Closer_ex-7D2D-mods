using GearsAPI.Settings.Global;
using GearsAPI.Settings.World;
using GearsAPI.Settings;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using UniLinq;
using System.Reflection.Emit;
using UnityEngine;
using System.IO;

namespace CustomFPVFov
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
            FovOverrides.modPath = _modInstance.Path;
            Log.Out(" Loading Patch: " + GetType());
            var harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPatch(typeof(vp_FPWeapon), nameof(vp_FPWeapon.Start))]
        [HarmonyPostfix]
        private static void Postfix_vp_FPWeapon_Start(ref float ___RenderingFieldOfView)
        {
            ___RenderingFieldOfView = FovOverrides.CurrentFov;
        }

        [HarmonyPatch(typeof(ItemActionZoom), nameof(ItemActionZoom.OnHoldingUpdate))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ItemActionZoom_OnHoldingUpdate(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_original = AccessTools.Field(typeof(vp_FPWeapon), nameof(vp_FPWeapon.originalRenderingFieldOfView));
            var fld_cur = AccessTools.Field(typeof(vp_FPWeapon), nameof(vp_FPWeapon.RenderingFieldOfView));
            var prop_value = AccessTools.PropertyGetter(typeof(FovOverrides), nameof(FovOverrides.CurrentFov));

            for (int i = 1; i < codes.Count; i++)
            {
                if (codes[i - 1].LoadsField(fld_original) && codes[i].StoresField(fld_cur))
                {
                    codes[i - 1] = new CodeInstruction(OpCodes.Call, prop_value);
                    codes.RemoveAt(i - 2);
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(vp_FPWeapon), nameof(vp_FPWeapon.UpdateZoom))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_vp_FPWeapon_UpdateZoom(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var mtd_smooth = AccessTools.Method(typeof(Mathf), nameof(Mathf.SmoothStep));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_smooth))
                {
                    codes[i - 2].operand = 5f;
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.SetFirstPersonView))]
        [HarmonyPostfix]
        private static void Postfix_EntityPlayerLocal_SetFirstPersonView(EntityPlayerLocal __instance)
        {
            if (__instance.vp_FPWeapon)
            {
                __instance.vp_FPWeapon.CacheRenderers();
            }
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Awake))]
        [HarmonyPrefix]
        private static void Prefix_EntityPlayerLocal_Awake(EntityPlayerLocal __instance)
        {
            FovOverrides.LoadWeaponFovOverrides();
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.OnHoldingItemChanged))]
        [HarmonyPostfix]
        private static void Postfix_EntityPlayerLocal_OnHoldingItemChanged(EntityPlayerLocal __instance)
        {
            FovOverrides.UpdatePlayerFov(__instance);
        }
    }

    public class GearsImpl : IGearsModApi
    {
        public static bool gearsLoaded = false;

        public void InitMod(IGearsMod modInstance)
        {
            gearsLoaded = true;
        }

        public void OnGlobalSettingsLoaded(IModGlobalSettings modSettings)
        {
            ISliderGlobalSetting fovSettings = modSettings.GetTab("FovSettings").GetCategory("Main").GetSetting("FovValue") as ISliderGlobalSetting;
            if (float.TryParse(fovSettings.CurrentValue, out float cur))
            {
                FovOverrides.defaultFov = cur;
            }
            fovSettings.OnSettingChanged += (settings, value) =>
            {
                if (float.TryParse(value, out float curFov))
                {
                    FovOverrides.defaultFov = curFov;
                    FovOverrides.UpdatePlayerFov();
                }
            };
        }

        public void OnWorldSettingsLoaded(IModWorldSettings worldSettings)
        {

        }
    }

    public static class FovOverrides
    {
        public static float CurrentFov { get; set; } = 45f;
        public static float defaultFov = 45f;
        public static string modPath = "";
        public static Dictionary<int, float> dict_id_fov = new Dictionary<int, float>();

        public static void LoadWeaponFovOverrides()
        {
            dict_id_fov.Clear();
            if (string.IsNullOrEmpty(modPath))
            {
                return;
            }
            string configPath = Path.Combine(modPath, "FovOverrides.txt");
            if (File.Exists(configPath))
            {
                using (var reader = new StreamReader(configPath))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        {
                            continue; // Skip empty lines and comments
                        }
                        if (line.Contains(","))
                        {
                            var parts = line.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && float.TryParse(parts[1].Trim(), out float fov))
                            {
                                parts[0] = parts[0].Trim();
                                if (parts[0] == "*")
                                {
                                    if (!GearsImpl.gearsLoaded)
                                    {
                                        defaultFov = fov;
                                    }
                                }
                                else
                                {
                                    ItemClass itemClass = ItemClass.GetItemClass(parts[0]);
                                    if (itemClass != null)
                                    {
                                        dict_id_fov[itemClass.Id] = fov;
                                    }
                                    else
                                    {
                                        Log.Warning($"CustomFPVFov: Item class '{parts[0]}' not found for FOV override.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void UpdatePlayerFov(EntityPlayerLocal player = null)
        {
            if (!player)
            {
                player = GameManager.Instance?.World?.GetPrimaryPlayer();
                if (!player)
                {
                    return;
                }
            }
            if (dict_id_fov.TryGetValue(player.inventory.holdingItem.Id, out float fov))
            {
                CurrentFov = fov;
            }
            else
            {
                CurrentFov = defaultFov;
            }
            if (player.vp_FPWeapon)
            {
                player.vp_FPWeapon.RenderingFieldOfView = CurrentFov;
            }
        }
    }

    public class ConsoleCmdRefreshFovOverrides : ConsoleCmdAbstract
    {
        public override int DefaultPermissionLevel => 1000;
        public override bool IsExecuteOnClient => true;

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            FovOverrides.LoadWeaponFovOverrides();
            FovOverrides.UpdatePlayerFov();
            Log.Out("CustomFPVFov: FOV overrides have been refreshed.");
        }

        public override string[] getCommands()
        {
            return new[] { "rfov" };
        }

        public override string getDescription()
        {
            return "Refreshes the FOV overrides from the configuration file.";
        }
    }
}
