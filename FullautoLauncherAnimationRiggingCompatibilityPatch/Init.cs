using HarmonyLib;
using KFCommonUtilityLib.Scripts.Singletons;
using System;
using System.Reflection;

public class FLARCompatibilityPatchInit : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Log.Out(" Loading Patch: " + GetType());
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

[HarmonyPatch]
public static class FLARPatch
{
    [HarmonyPatch(typeof(ItemActionBetterLauncher.ItemActionDataBetterLauncher), MethodType.Constructor, new Type[] { typeof(ItemInventoryData), typeof(int) })]
    [HarmonyPostfix]
    private static void Postfix_ctor_ItemActionDataBetterLauncher(ItemActionBetterLauncher.ItemActionDataBetterLauncher __instance, ItemInventoryData _invData)
    {
        __instance.projectileJoint = AnimationRiggingManager.GetTransformOverrideByName("ProjectileJoint", _invData.model);
    }
}