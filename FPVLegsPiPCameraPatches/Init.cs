using FPVLegs;
using HarmonyLib;
using KFCommonUtilityLib.KFAttached.Render;

namespace FPVLegsPiPCameraPatches
{
    public class Init : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (!inited)
            {
                inited = true;
                Log.Out("Loading Patch: " + GetType());
                var harmony = new HarmonyLib.Harmony(GetType().ToString());
                harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            }
        }
    }

    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPatch(typeof(MagnifyScope), "CreateCamera")]
        [HarmonyPostfix]
        private static void Postfix_MagnifyScope_CreateCamera(MagnifyScope __instance, EntityPlayerLocal ___player)
        {
            if (__instance.pipCamera)
            {
                __instance.pipCamera.transform.AddMissingComponent<FPVLegCameraCallback>().Init(___player.vp_FPCamera, ___player, ___player.emodel?.avatarController?.GetAnimator());
            }
        }
    }
}
