using FullautoLauncher.Scripts.ProjectileManager;
using System.Reflection;

public class FullautoLauncherInit : IModApi
{
    private static bool inited = false;
    public void InitMod(Mod _modInstance)
    {
        if(inited)
        {
            return;
        }
        inited = true;
        Log.Out(" Loading Patch: " + GetType());
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        ModEvents.UnityUpdate.RegisterHandler(CustomProjectileManager.Update);
    }
}

