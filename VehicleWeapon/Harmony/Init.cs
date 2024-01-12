using System.Reflection;

public class VehicleWeaponInit : IModApi
{
    private static bool inited = false;
    public void InitMod(Mod _modInstance)
    {
        if (inited)
            return;
        inited = true;
        Log.Out(" Loading Patch: " + GetType());
        ModEvents.GameAwake.RegisterHandler(() =>
        {
            XUiC_OptionsVideo.OnSettingsChanged += VehicleWeaponBase.OnVideoSettingChanged;
            VehicleWeaponBase.OnVideoSettingChanged();
        });
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

