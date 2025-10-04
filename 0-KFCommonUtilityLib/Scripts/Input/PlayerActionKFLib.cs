using InControl;

public class PlayerActionKFLib : CustomPlayerActionVersionBase
{
    public static PlayerActionKFLib Instance { get; private set; }
    public override ControllerActionType ControllerActionDisplay => ControllerActionType.OnFoot;

    public PlayerAction ToggleFireMode;
    public PlayerAction ToggleActionMode;
    public PlayerAction ToggleZoom;
    public PlayerAction AltMelee;
    public PlayerAction AltInspect;
    public PlayerAction WeaponBlocking;

    public PlayerActionKFLib()
    {
        Name = "KFLibPlayerActions";
        Version = 1;
        Instance = this;
        Enabled = true;
        var localActions = Platform.PlatformManager.NativePlatform.Input.PrimaryPlayer;
        var permaActions = localActions.PermanentActions;
        UserData = new PlayerActionData.ActionSetUserData(new PlayerActionsBase[] { localActions, permaActions });
        localActions.AddUniConflict(this);
        permaActions.AddUniConflict(this);
    }

    public override void CreateActions()
    {
        ToggleFireMode = CreatePlayerAction("ToggleFireMode");
        ToggleFireMode.UserData = new PlayerActionData.ActionUserData("inpActToggleFireModeName", "inpActToggleFireModeDesc", PlayerActionData.GroupPlayerControl);
        ToggleActionMode = CreatePlayerAction("ToggleMode");
        ToggleActionMode.UserData = new PlayerActionData.ActionUserData("inpActToggleWeaponModeName", "inpActToggleWeaponModeDesc", PlayerActionData.GroupPlayerControl);
        ToggleZoom = CreatePlayerAction("ToggleZoomLevel");
        ToggleZoom.UserData = new PlayerActionData.ActionUserData("inpActToggleZoomLevelName", "inpActToggleZoomLevelDesc", PlayerActionData.GroupPlayerControl);
        AltMelee = CreatePlayerAction("PerformAltMelee");
        AltMelee.UserData = new PlayerActionData.ActionUserData("inpActPerformAltMeleeName", "inpActPerformAltMeleeDesc", PlayerActionData.GroupPlayerControl);
        AltInspect = CreatePlayerAction("PerformAltInspect");
        AltInspect.UserData = new PlayerActionData.ActionUserData("inpActPerformAltInspectName", "inpActPerformAltInspectDesc", PlayerActionData.GroupPlayerControl);
        WeaponBlocking = CreatePlayerAction("PerformWeaponBlocking");
        WeaponBlocking.UserData = new PlayerActionData.ActionUserData("inpActPerformWeaponBlockingName", "inpActPerformWeaponBlockingDesc", PlayerActionData.GroupPlayerControl);
    }

    public override void CreateDefaultJoystickBindings()
    {
        ListenOptions.IncludeControllers = true;
    }

    public override void CreateDefaultKeyboardBindings()
    {
        ListenOptions.IncludeKeys = true;
        ListenOptions.IncludeMouseButtons = true;
        ListenOptions.IncludeMouseScrollWheel = true;
        ToggleFireMode.AddDefaultBinding(new Key[] { Key.Z });
        ToggleActionMode.AddDefaultBinding(new Key[] { Key.X });
        ToggleZoom.AddDefaultBinding(Mouse.MiddleButton);
        AltMelee.AddDefaultBinding(new Key[] { Key.V });
    }
}