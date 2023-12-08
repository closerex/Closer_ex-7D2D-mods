using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class DisplayMetaAsBuffPatch
    {
        [HarmonyPatch(typeof(XUiM_PlayerBuffs), nameof(XUiM_PlayerBuffs.GetBuffDisplayInfo))]
        [HarmonyPrefix]
        private static bool Prefix_GetBuffDisplayInfo_XUiM_PlayerBuffs(EntityUINotification notification, ref string __result)
        {

            return true;
        }
    }
}
