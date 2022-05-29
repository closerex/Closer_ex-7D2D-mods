public class MinEventActionBroadcastPlaySoundLocal : MinEventActionPlaySound
{
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        return !targets[0].isEntityRemote && base.CanExecute(_eventType, _params);
    }
}

