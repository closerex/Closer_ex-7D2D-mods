using InControl;

public class PlayerActionToggleFireMode : CustomPlayerActionVersionBase
{
    public static PlayerActionToggleFireMode Instance { get; private set; }
    public override ControllerActionType ControllerActionDisplay => ControllerActionType.OnFoot;

    public PlayerAction Toggle;

    public PlayerActionToggleFireMode()
    {
        Name = "ToggleFireMode";
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
        Toggle = CreatePlayerAction("ToggleFireMode");
        Toggle.UserData = new PlayerActionData.ActionUserData("inpActToggleFireModeName", "inpActToggleFireModeDesc", PlayerActionData.GroupPlayerControl);
    }

    public override void CreateDefaultJoystickBindings()
    {

    }

    public override void CreateDefaultKeyboardBindings()
    {
        Toggle.AddDefaultBinding(new Key[] { Key.Z });
    }
}
