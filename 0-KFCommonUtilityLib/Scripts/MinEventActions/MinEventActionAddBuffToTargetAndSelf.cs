public class MinEventActionAddBuffToTargetAndSelf : MinEventActionAddBuff
{
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        bool flag = base.CanExecute(_eventType, _params);
        if (targetType == TargetTypes.selfAOE)
            flag = true;
        if (flag && targetType != TargetTypes.self)
            targets.Add(_params.Self);
        return flag;
    }
}
