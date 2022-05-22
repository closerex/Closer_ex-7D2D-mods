using System;

public class CustomPlayerActionVersionBase : PlayerActionsBase
{
    protected override void CreateActions()
    {
        throw new NotImplementedException();
    }

    protected override void CreateDefaultJoystickBindings()
    {
        throw new NotImplementedException();
    }

    protected override void CreateDefaultKeyboardBindings()
    {
        throw new NotImplementedException();
    }

    public virtual void InitActionSetRelations()
    {

    }
    public int Version { get; protected set; } = 1;
}
