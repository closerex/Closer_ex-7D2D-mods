using System.Reflection;

public class VehicleWeaponInit : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Log.Out(" Loading Patch: " + GetType());
        ModEvents.GameAwake.RegisterHandler(() =>
        {
            XUiC_OptionsVideo.OnSettingsChanged += VehicleWeaponRotatorBase.OnVideoSettingChanged;
            VehicleWeaponRotatorBase.OnVideoSettingChanged();
        });
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

