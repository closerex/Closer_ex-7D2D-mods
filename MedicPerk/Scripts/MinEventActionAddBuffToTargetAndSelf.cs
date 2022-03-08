using System;
using System.Collections.Generic;
using UnityEngine;

public class MinEventActionAddBuffToTargetAndSelf : MinEventActionAddBuff
{
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        bool flag = base.CanExecute(_eventType, _params);
        if (flag)
            targets.Add(_params.Self);
        return flag;
    }
}

