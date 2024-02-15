using HarmonyLib;
using KFCommonUtilityLib.Scripts.StaticManagers;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class ItemActionModulePatch
    {
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        [HarmonyPrefix]
        private static bool Prefix_StartGame_GameManager()
        {
            ItemActionModuleManager.InitNew();
            return true;
        }

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.Init))]
        [HarmonyPostfix]
        private static void Postfix_Init_ItemClass(ItemClass __instance)
        {
            ItemActionModuleManager.CheckItem(__instance);
        }

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.LateInitAll))]
        [HarmonyPrefix]
        private static bool Prefix_LateInitAll_ItemClass()
        {
            ItemActionModuleManager.FinishAndLoad();
            return true;
        }
    }
}
