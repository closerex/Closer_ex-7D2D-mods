public class MinEventActionBroadcastPlaySoundLocal : MinEventActionPlaySound
{
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        return targetType == TargetTypes.self && !_params.Self.isEntityRemote && base.CanExecute(_eventType, _params);
    }
}

