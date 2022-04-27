using System.Reflection;

public class CustomParticleLoaderMultiExplosionInit : IModApi
{
    [System.Obsolete]
    public void InitMod(Mod _modInstance)
    {
        Log.Out(" Loading Patch: " + GetType());
        HarmonyLib.Harmony.DEBUG = true;
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

