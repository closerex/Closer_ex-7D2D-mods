using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
public class HideMarkerOnAimPatch
{
    //[HarmonyPatch(typeof(XUiC_OnScreenIcons), nameof(XUiC_OnScreenIcons.Init))]
    //[HarmonyPostfix]
    //private static void Postfix_Init_XUiC_OnScreenIcons(XUiC_OnScreenIcons __instance)
    //{
    //    GameObject iconParent = new GameObject("AllIconParent");
    //    iconParent.transform.SetParent(__instance.ViewComponent.UiTransform);
    //    iconParent.transform.localScale = Vector3.one;
    //}

    //[HarmonyPatch(typeof(XUiC_OnScreenIcons), nameof(XUiC_OnScreenIcons.RegisterIcon))]
    //[HarmonyPostfix]
    //private static void Postfix_RegisterIcon_XUiC_OnScreenIcons(XUiC_OnScreenIcons __instance, NavObject newNavObject)
    //{
    //    if (__instance.ViewComponent?.UiTransform)
    //    {
    //        newNavObject.TrackedTransform?.SetParent(__instance.ViewComponent.UiTransform.Find("AllIconParent"));
    //    }
    //}

    [HarmonyPatch(typeof(XUiC_OnScreenIcons), nameof(XUiC_OnScreenIcons.Update))]
    [HarmonyPrefix]
    private static bool Prefix_Update_XUiC_OnScreenIcons(XUiC_OnScreenIcons __instance)
    {
        GameObject iconParent = __instance.ViewComponent.UiTransform.gameObject;
        if (__instance.xui.playerUI.entityPlayer.bAimingGun)
        {
            iconParent.SetActive(false);
        }
        else
        {
            iconParent.SetActive(true);
        }
        return true;
    }
}
