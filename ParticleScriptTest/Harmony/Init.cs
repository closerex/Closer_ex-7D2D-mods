using HarmonyLib.Tools;
using System.Reflection;

public class ParticleScriptTestInit : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Log.Out(" Loading Patch: " + GetType());
        HarmonyFileLog.Enabled = true;
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

