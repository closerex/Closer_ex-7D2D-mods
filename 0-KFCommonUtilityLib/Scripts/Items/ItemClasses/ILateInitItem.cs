using HarmonyLib;

namespace KFCommonUtilityLib
{
    public interface ILateInitItem
    {
        void LateInitItem();
    }

    [HarmonyPatch]
    public static class LateInitItemPatches
    {
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.LateInit))]
        [HarmonyPostfix]
        private static void Postfix_ItemClass_LateInit(ItemClass __instance)
        {
            if (__instance is ILateInitItem lateinit)
            {
                lateinit.LateInitItem();
            }
        }
    }
}
