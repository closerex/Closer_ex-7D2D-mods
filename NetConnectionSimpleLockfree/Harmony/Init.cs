using HarmonyLib.Tools;
using System.Reflection;

public class NetConnectionSimpleLockfreeInit : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Log.Out(" Loading Patch: " + GetType());
        HarmonyFileLog.Enabled = true;
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

