using GearsAPI.Settings.Global;
using GearsAPI.Settings.World;
using GearsAPI.Settings;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using UniLinq;
using System.Reflection.Emit;
using UnityEngine;

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
            ___RenderingFieldOfView = GearsImpl.CurrentFov;
        }

        [HarmonyPatch(typeof(ItemActionZoom), nameof(ItemActionZoom.OnHoldingUpdate))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ItemActionZoom_OnHoldingUpdate(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_original = AccessTools.Field(typeof(vp_FPWeapon), nameof(vp_FPWeapon.originalRenderingFieldOfView));
            var fld_cur = AccessTools.Field(typeof(vp_FPWeapon), nameof(vp_FPWeapon.RenderingFieldOfView));
            var prop_value = AccessTools.PropertyGetter(typeof(GearsImpl), nameof(GearsImpl.CurrentFov));

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
    }

    public class GearsImpl : IGearsModApi
    {
        public static float CurrentFov { get; private set; } = 45f;
        public void InitMod(IGearsMod modInstance)
        {

        }

        public void OnGlobalSettingsLoaded(IModGlobalSettings modSettings)
        {
            ISliderGlobalSetting fovSettings = modSettings.GetTab("FovSettings").GetCategory("Main").GetSetting("FovValue") as ISliderGlobalSetting;
            if (float.TryParse(fovSettings.CurrentValue, out float cur))
            {
                CurrentFov = cur;
            }
            fovSettings.OnSettingChanged += (settings, value) =>
            {
                if (float.TryParse(value, out float curFov))
                {
                    CurrentFov = curFov;
                    if (GameManager.Instance.World != null)
                    {
                        EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
                        if (player && player.vp_FPWeapon)
                        {
                            CurrentFov = curFov;
                            player.vp_FPWeapon.RenderingFieldOfView = curFov;
                        }
                    }
                }
            };
        }

        public void OnWorldSettingsLoaded(IModWorldSettings worldSettings)
        {

        }
    }
}
