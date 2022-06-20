using System.Reflection;

public class CommonUtilityLibInit : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Log.Out(" Loading Patch: " + GetType());
        ModEvents.GameAwake.RegisterHandler(CommonUtilityPatch.InitShotStates);
        //ModEvents.GameUpdate.RegisterHandler(CommonUtilityPatch.ForceUpdateGC);
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

