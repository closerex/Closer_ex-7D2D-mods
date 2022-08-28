using System.Collections.Generic;
using InControl;

public class PlayerActionsVehicleWeapon : CustomPlayerActionVersionBase
{
    public PlayerAction HoldToggleActivated;
    public PlayerAction FireShot;
    //public PlayerAction Test1;
    public PlayerActionsVehicleWeapon()
    {
        Name = "vehicleWeapon";
        Version = 1;
        Instance = this;
        Enabled = false;
        var vehicleActions = Platform.PlatformManager.NativePlatform.Input.PrimaryPlayer.VehicleActions;
        var permaActions = Platform.PlatformManager.NativePlatform.Input.PrimaryPlayer.PermanentActions;
        UserData = new PlayerActionData.ActionSetUserData(new PlayerActionsBase[] { vehicleActions, permaActions,  });
        vehicleActions.AddUniConflict(this);
        permaActions.AddUniConflict(this);
    }

    public override void InitActionSetRelations()
    {
        base.InitActionSetRelations();
        PlayerActionsVehicleExtra.Instance.AddBiConflict(this);
    }

    protected override void CreateActions()
    {
        HoldToggleActivated = CreatePlayerAction("HoldToggleActivated");
        HoldToggleActivated.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponHoldToggleActivatedName", null, PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.Both, true);
        FireShot = CreatePlayerAction("FireShot");
        FireShot.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponFireShotName", null, PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.Both, true);

        //Test1 = CreatePlayerAction("Test1");
        //Test1.UserData = new PlayerActionData.ActionUserData("inpActTest1", null, PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.KbdMouseOnly, true);
    }

    protected override void CreateDefaultJoystickBindings()
    {
        HoldToggleActivated.AddDefaultBinding(InputControlType.LeftStickButton);
        FireShot.AddDefaultBinding(InputControlType.DPadRight);
    }

    protected override void CreateDefaultKeyboardBindings()
    {
        HoldToggleActivated.AddDefaultBinding(new Key[] { Key.LeftControl });
        FireShot.AddDefaultBinding(new Key[] { Key.G });
        //Test1.AddDefaultBinding(new Key[] { Key.LeftAlt });
    }

    public static PlayerActionsVehicleWeapon Instance { get; private set; }
}

