using System.Reflection;

public class CustomPlayerActionManagerInit : IModApi
{
    private static bool inited = false;
    public void InitMod(Mod _modInstance)
    {
        if(inited)
            return;
        inited = true;
        Log.Out(" Loading Patch: " + GetType());
        ModEvents.GameAwake.RegisterHandler(CustomPlayerActionManager.InitCustomControls);
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

