using System;

public abstract class CustomPlayerActionVersionBase : PlayerActionsBase
{
    public enum ControllerActionType
    {
        None,
        OnFoot,
        Vehicle
    }

    public virtual void InitActionSetRelations()
    {

    }
    public int Version { get; protected set; } = 1;
    public virtual ControllerActionType ControllerActionDisplay { get; } = ControllerActionType.None;
}
