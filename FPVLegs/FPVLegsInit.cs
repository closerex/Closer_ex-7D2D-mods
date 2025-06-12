using GearsAPI.Settings;
using GearsAPI.Settings.Global;
using GearsAPI.Settings.World;
using System.Collections.Generic;

namespace FPVLegs
{
    public class FPVLegsInit : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (!inited)
            {
                inited = true;
                Log.Out("Loading Patch: " + GetType());
                var harmony = new HarmonyLib.Harmony(GetType().ToString());
                harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            }
        }
    }

    public class GearsInit : IGearsModApi
    {
        public void InitMod(IGearsMod modInstance)
        {
            FPVLegMode.gearsLoaded = true;
        }

        public void OnGlobalSettingsLoaded(IModGlobalSettings modSettings)
        {
            ISwitchGlobalSetting modeSettings = modSettings.GetTab("ModeSetting").GetCategory("Main").GetSetting("Mode") as ISwitchGlobalSetting;
            ISwitchGlobalSetting shadowSettings = modSettings.GetTab("ModeSetting").GetCategory("Main").GetSetting("Shadow") as ISwitchGlobalSetting;
            if (modeSettings.CurrentValue == "Old")
            {
                FPVLegMode.newMode = false;
            }
            else
            {
                FPVLegMode.newMode = true;
            }
            if (shadowSettings.CurrentValue == "Yes")
            {
                FPVLegMode.disableShadow = true;
            }
            else
            {
                FPVLegMode.disableShadow = false;
            }
            modeSettings.OnSettingChanged += (settings, value) =>
            {
                if (modeSettings.CurrentValue == "Old")
                {
                    FPVLegMode.newMode = false;
                }
                else
                {
                    FPVLegMode.newMode = true;
                }
            };
            shadowSettings.OnSettingChanged += (settings, value) =>
            {
                if (shadowSettings.CurrentValue == "Yes")
                {
                    FPVLegMode.disableShadow = true;
                }
                else
                {
                    FPVLegMode.disableShadow = false;
                }
                var player = GameManager.Instance?.World?.GetPrimaryPlayer();
                if (player)
                {
                    FPVLegPatches.UpdateTPVMeshState(player, !player.bFirstPersonView);
                }
            };
        }

        public void OnWorldSettingsLoaded(IModWorldSettings worldSettings)
        {
        }
    }

    public static class FPVLegMode
    {
        public static bool gearsLoaded = false;
        public static bool newMode = true;
        public static bool disableShadow = true;
    }

    public class ConsoleCmdToggleLegMode : ConsoleCmdAbstract
    {
        public override int DefaultPermissionLevel => 1000;
        public override bool IsExecuteOnClient => true;

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (!FPVLegMode.gearsLoaded)
            {
                FPVLegMode.newMode = !FPVLegMode.newMode;
                Log.Out($"Current FPV Leg Mode: {(FPVLegMode.newMode ? "New" : "Old")}");
            }
            else
            {
                Log.Warning($"Gears is loaded. Please set the mode in Gears menu.");
            }
        }

        public override string[] getCommands()
        {
            return new[] { "togglelm" };
        }

        public override string getDescription()
        {
            return "Toggle Fpv leg display mode.";
        }
    }
}
