using HarmonyLib;
using KFCommonUtilityLib.KFAttached.Render;
using System;
using UnityEngine;

namespace TorchPatches
{
    public class Init : IModApi
    {
        public void InitMod(Mod _modInstance)
        {
            Log.Out("Loading KFLib Torch Patch");
            // Register the patch
            Harmony harmony = new Harmony("com.example.torch.patch");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch]
    public static class MagnifyScopePatches
    {
        [HarmonyPatch(typeof(MagnifyScope), "CreateCamera")]
        [HarmonyPostfix]
        private static void Postfix_MagnifyScope_CreateCamera(MagnifyScope __instance, Camera ___pipCamera)
        {
            if (___pipCamera)
            {
                ___pipCamera.gameObject.GetOrAddComponent<Torch.Lights.ShadowCaching.TorchLightRenderer>();
            }
        }
    }
}