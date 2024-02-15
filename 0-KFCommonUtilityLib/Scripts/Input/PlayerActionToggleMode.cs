using InControl;

public class PlayerActionToggleMode : CustomPlayerActionVersionBase
{
    public PlayerAction Toggle;

    public PlayerActionToggleMode()
    {
        Name = "WeaponMode";
        Version = 1;
        Instance = this;
        Enabled = true;
        var localActions = Platform.PlatformManager.NativePlatform.Input.PrimaryPlayer;
        var permaActions = localActions.PermanentActions;
        UserData = new PlayerActionData.ActionSetUserData(new PlayerActionsBase[] { localActions, permaActions });
        localActions.AddUniConflict(this);
        permaActions.AddUniConflict(this);
    }

    protected override void CreateActions()
    {
        Toggle = CreatePlayerAction("ToggleMode");
        Toggle.UserData = new PlayerActionData.ActionUserData("inpActToggleWeaponModeName", "inpActToggleWeaponModeDesc", PlayerActionData.GroupPlayerControl);
    }

    protected override void CreateDefaultJoystickBindings()
    {

    }

    protected override void CreateDefaultKeyboardBindings()
    {
        Toggle.AddDefaultBinding(new Key[] { Key.X });
    }

    public static PlayerActionToggleMode Instance { get; private set; }
}
