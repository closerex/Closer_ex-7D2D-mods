using System.Xml;

public class MinEventActionAmmoAccessBase : MinEventActionItemCountRandomBase
{
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        return !_params.Self.isEntityRemote && base.CanExecute(_eventType, _params) && _params.ItemValue.ItemClass.Actions[0] is ItemActionRanged;
    }
}
