using InControl;

public class PlayerActionKFLib : CustomPlayerActionVersionBase
{
    public static PlayerActionKFLib Instance { get; private set; }
    public override ControllerActionType ControllerActionDisplay => ControllerActionType.OnFoot;

    public PlayerAction ToggleFireMode;
    public PlayerAction ToggleActionMode;
    public PlayerAction ToggleZoom;
    public PlayerAction AltMelee;

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
    }

    public override void CreateDefaultJoystickBindings()
    {

    }

    public override void CreateDefaultKeyboardBindings()
    {
        ToggleFireMode.AddDefaultBinding(new Key[] { Key.Z });
        ToggleActionMode.AddDefaultBinding(new Key[] { Key.X });
        ToggleZoom.AddDefaultBinding(Mouse.MiddleButton);
        AltMelee.AddDefaultBinding(new Key[] { Key.V });
    }
}