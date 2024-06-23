using System;

public class CustomPlayerActionVersionBase : PlayerActionsBase
{
    public override void CreateActions()
    {
        throw new NotImplementedException();
    }

    public override void CreateDefaultJoystickBindings()
    {
        throw new NotImplementedException();
    }

    public override void CreateDefaultKeyboardBindings()
    {
        throw new NotImplementedException();
    }

    public virtual void InitActionSetRelations()
    {

    }
    public int Version { get; protected set; } = 1;
}
