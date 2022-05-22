using System.Reflection;

public class CustomPlayerActionManagerInit : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Log.Out(" Loading Patch: " + GetType());
        ModEvents.GameAwake.RegisterHandler(CustomPlayerActionManager.InitCustomControls);
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

