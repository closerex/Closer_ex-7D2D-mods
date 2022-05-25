using System.Reflection;

public class CustomParticleEffectLoaderInit : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Log.Out(" Loading Patch: " + GetType());
        ModEvents.GameAwake.RegisterHandler(CustomExplosionManager.CreatePropertyParsers);
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

