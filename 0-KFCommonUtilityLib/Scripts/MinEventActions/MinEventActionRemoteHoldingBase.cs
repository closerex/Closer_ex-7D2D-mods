//workaround for inventory sync
//full toolbelt data is sent when holding item value changed or whatever, after a certain delay
//causing remote players to update current holding item constantly
//thus we need to handle some holding event for remote players on local player side
using System.Xml.Linq;

public class MinEventActionRemoteHoldingBase : MinEventActionBase
{
    protected bool isRemoteHolding = false;
    protected bool localOnly = true;

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if (base.ParseXmlAttribute(_attribute))
            return true;

        if (_attribute.Name == "local_only")
        {
            localOnly = bool.Parse(_attribute.Value);
            return true;
        }
        return false;
    }

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        isRemoteHolding = (_eventType == MinEventTypes.onSelfEquipStart && _params.Self.isEntityRemote);
        return (!localOnly || !_params.Self.isEntityRemote) && (!_params.Self.isEntityRemote || isRemoteHolding) && base.CanExecute(_eventType, _params);
    }
}

