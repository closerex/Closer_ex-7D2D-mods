using HarmonyLib;
using KFCommonUtilityLib.KFAttached.Render;
using Rainstorm;
using UnityEngine;

namespace RainstormPatches
{
    public class Init : IModApi
    {
        public void InitMod(Mod _modInstance)
        {
            Log.Out("Loading KFLib RainStorm Patch");
            // Register the patch
            Harmony harmony = new Harmony("com.example.rainstorm.patch");
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
                ___pipCamera.gameObject.GetOrAddComponent<RainstormRenderer>();
            }
        }
    }
}
