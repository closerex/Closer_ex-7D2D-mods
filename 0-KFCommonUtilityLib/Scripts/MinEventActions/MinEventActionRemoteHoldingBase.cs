using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//workaround for inventory sync
//full toolbelt data is sent when holding item value changed or whatever, after a certain delay
//causing remote players to update current holding item constantly
//thus we need to handle some holding event for remote players on local player side
class MinEventActionRemoteHoldingBase : MinEventActionBase
{
    protected bool isRemoteHolding = false;

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        isRemoteHolding = (_eventType == MinEventTypes.onSelfEquipStart && _params.Self.isEntityRemote);
        return (!_params.Self.isEntityRemote || isRemoteHolding) && base.CanExecute(_eventType, _params);
    }
}

