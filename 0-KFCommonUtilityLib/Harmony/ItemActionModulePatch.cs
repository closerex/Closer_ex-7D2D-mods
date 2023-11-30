using HarmonyLib;
using KFCommonUtilityLib.Scripts.Singletons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class ItemActionModulePatch
    {
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.Init))]
        [HarmonyPostfix]
        private static void Postfix(ItemClass __instance)
        {
            ItemActionModuleManager.CheckItem(__instance);
        }
    }
}
