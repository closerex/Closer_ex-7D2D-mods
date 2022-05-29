public class MinEventActionBroadcastPlaySoundLocal : MinEventActionPlaySound
{
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        return base.CanExecute(_eventType, _params) && targetType == TargetTypes.self && !_params.Self.isEntityRemote;
    }
}

