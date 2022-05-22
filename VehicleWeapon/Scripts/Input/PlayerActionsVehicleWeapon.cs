using System.Collections.Generic;
using InControl;

public class PlayerActionsVehicleWeapon : CustomPlayerActionVersionBase
{
    PlayerAction ActivateSlot1;
    PlayerAction ActivateSlot2;
    PlayerAction ActivateSlot3;
    PlayerAction ActivateSlot4;
    PlayerAction ActivateSlot5;
    PlayerAction ActivateSlot6;
    PlayerAction ActivateSlot7;
    PlayerAction ActivateSlot8;
    PlayerAction ActivateSlot9;
    PlayerAction ActivateSlot10;
    public readonly List<PlayerAction> ActivateActions = new List<PlayerAction>();
    public PlayerAction HoldToggleActivated;
    public PlayerAction FireShot;
    //public PlayerAction Test1;

    public int ActivateSlotIsPressed
    {
        get
        {
            for(int i = 0; i < ActivateActions.Count; i++)
                if(ActivateActions[i].IsPressed)
                    return i;
            return -1;
        }
    }
    public int ActivateSlotWasPressed
    {
        get
        {
            for(int i = 0; i < ActivateActions.Count; i++)
                if(ActivateActions[i].WasPressed)
                    return i;
            return -1;
        }
    }
    public int ActivateSlotWasReleased
    {
        get
        {
            for(int i = 0; i < ActivateActions.Count; i++)
                if(ActivateActions[i].WasReleased)
                    return i;
            return -1;
        }
    }
    public PlayerActionsVehicleWeapon()
    {
        Name = "vehicleWeapon";
        Version = 1;
        Instance = this;
        Enabled = false;
        ActivateActions.Add(ActivateSlot1);
        ActivateActions.Add(ActivateSlot2);
        ActivateActions.Add(ActivateSlot3);
        ActivateActions.Add(ActivateSlot4);
        ActivateActions.Add(ActivateSlot5);
        ActivateActions.Add(ActivateSlot6);
        ActivateActions.Add(ActivateSlot7);
        ActivateActions.Add(ActivateSlot8);
        ActivateActions.Add(ActivateSlot9);
        ActivateActions.Add(ActivateSlot10);
        var vehicleActions = Platform.PlatformManager.NativePlatform.Input.PrimaryPlayer.VehicleActions;
        var permaActions = Platform.PlatformManager.NativePlatform.Input.PrimaryPlayer.PermanentActions;
        UserData = new PlayerActionData.ActionSetUserData(new PlayerActionsBase[] { vehicleActions, permaActions });
        vehicleActions.AddUniConflict(this);
        permaActions.AddUniConflict(this);
    }

    protected override void CreateActions()
    {
        ActivateSlot1 = CreatePlayerAction("ActivateSlot1");
        ActivateSlot1.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponActivateSlot1Name", "inpActVehicleWeaponActivateSlotDesc", PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.Both, true);
        ActivateSlot2 = CreatePlayerAction("ActivateSlot2");
        ActivateSlot2.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponActivateSlot2Name", "inpActVehicleWeaponActivateSlotDesc", PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.Both, true);
        ActivateSlot3 = CreatePlayerAction("ActivateSlot3");
        ActivateSlot3.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponActivateSlot3Name", "inpActVehicleWeaponActivateSlotDesc", PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.Both, true);
        ActivateSlot4 = CreatePlayerAction("ActivateSlot4");
        ActivateSlot4.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponActivateSlot4Name", "inpActVehicleWeaponActivateSlotDesc", PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.KbdMouseOnly, true);
        ActivateSlot5 = CreatePlayerAction("ActivateSlot5");
        ActivateSlot5.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponActivateSlot5Name", "inpActVehicleWeaponActivateSlotDesc", PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.KbdMouseOnly, true);
        ActivateSlot6 = CreatePlayerAction("ActivateSlot6");
        ActivateSlot6.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponActivateSlot6Name", "inpActVehicleWeaponActivateSlotDesc", PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.KbdMouseOnly, true);
        ActivateSlot7 = CreatePlayerAction("ActivateSlot7");
        ActivateSlot7.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponActivateSlot7Name", "inpActVehicleWeaponActivateSlotDesc", PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.KbdMouseOnly, true);
        ActivateSlot8 = CreatePlayerAction("ActivateSlot8");
        ActivateSlot8.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponActivateSlot8Name", "inpActVehicleWeaponActivateSlotDesc", PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.KbdMouseOnly, true);
        ActivateSlot9 = CreatePlayerAction("ActivateSlot9");
        ActivateSlot9.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponActivateSlot9Name", "inpActVehicleWeaponActivateSlotDesc", PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.KbdMouseOnly, true);
        ActivateSlot10 = CreatePlayerAction("ActivateSlot10");
        ActivateSlot10.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponActivateSlot10Name", "inpActVehicleWeaponActivateSlotDesc", PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.KbdMouseOnly, true);
        HoldToggleActivated = CreatePlayerAction("HoldToggleActivated");
        HoldToggleActivated.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponHoldToggleActivatedName", null, PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.Both, true);
        FireShot = CreatePlayerAction("FireShot");
        FireShot.UserData = new PlayerActionData.ActionUserData("inpActVehicleWeaponFireShotName", null, PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.Both, true);
        //Test1 = CreatePlayerAction("Test1");
        //Test1.UserData = new PlayerActionData.ActionUserData("inpActTest1", null, PlayerActionVehicleWeaponData.GroupVehicleWeapon, PlayerActionData.EAppliesToInputType.KbdMouseOnly, true);
    }

    protected override void CreateDefaultJoystickBindings()
    {
        ActivateSlot1.AddDefaultBinding(InputControlType.DPadUp);
        ActivateSlot2.AddDefaultBinding(InputControlType.DPadRight);
        ActivateSlot3.AddDefaultBinding(InputControlType.DPadDown);
        HoldToggleActivated.AddDefaultBinding(InputControlType.LeftStickButton);
        FireShot.AddDefaultBinding(InputControlType.DPadRight);
    }

    protected override void CreateDefaultKeyboardBindings()
    {
        ActivateSlot1.AddDefaultBinding(new Key[] { Key.Key1 });
        ActivateSlot2.AddDefaultBinding(new Key[] { Key.Key2 });
        ActivateSlot3.AddDefaultBinding(new Key[] { Key.Key3 });
        ActivateSlot4.AddDefaultBinding(new Key[] { Key.Key4 });
        ActivateSlot5.AddDefaultBinding(new Key[] { Key.Key5 });
        ActivateSlot6.AddDefaultBinding(new Key[] { Key.Key6 });
        ActivateSlot7.AddDefaultBinding(new Key[] { Key.Key7 });
        ActivateSlot8.AddDefaultBinding(new Key[] { Key.Key8 });
        ActivateSlot9.AddDefaultBinding(new Key[] { Key.Key9 });
        ActivateSlot10.AddDefaultBinding(new Key[] { Key.Key0 });
        HoldToggleActivated.AddDefaultBinding(new Key[] { Key.LeftControl });
        FireShot.AddDefaultBinding(new Key[] { Key.G });
        //Test1.AddDefaultBinding(new Key[] { Key.LeftAlt });
    }

    public static PlayerActionsVehicleWeapon Instance { get; private set; }
}

