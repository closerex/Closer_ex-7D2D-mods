using HarmonyLib;

namespace KFCommonUtilityLib.Harmony
{
    //[HarmonyPatch]
    //public static class DisplayMetaAsBuffPatch
    //{
    //    [HarmonyPatch(typeof(XUiM_PlayerBuffs), nameof(XUiM_PlayerBuffs.GetBuffDisplayInfo))]
    //    [HarmonyPrefix]
    //    private static bool Prefix_GetBuffDisplayInfo_XUiM_PlayerBuffs(EntityUINotification notification, ref string __result)
    //    {
    //        if (notification is DisplayAsBuffEntityUINotification && notification.Buff != null)
    //        {
    //            __result = notification.CurrentValue.ToString();
    //            return false;
    //        }

    //        return true;
    //    }
    //}
}
