using HarmonyLib;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuartzUIPatch
{
    public class Init : IModApi
    {
        public void InitMod(Mod _modInstance)
        {
            Log.Out("Loading KFLib QuartzUI Patch");
            // Register the patch
            Harmony harmony = new Harmony("com.example.quartzui.patch");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch]
    static class Patches
    {
        [HarmonyPatch(typeof(UIDisplayInfoFromXmlPatch), nameof(UIDisplayInfoFromXmlPatch.ParseDisplayInfoEntry))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_UIDisplayInfoFromXmlPatch_ParseDisplayInfoEntry(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(AccessTools.Method(typeof(EnumUtils), nameof(EnumUtils.Parse), new[] { typeof(string), typeof(bool) }, new[] { typeof(PassiveEffects) }),
                                               AccessTools.Method(typeof(CustomEffectEnumManager), nameof(CustomEffectEnumManager.GetEnumOrThrow), new[] { typeof(string), typeof(bool) }, new[] { typeof(PassiveEffects) }));
        }
    }
}
