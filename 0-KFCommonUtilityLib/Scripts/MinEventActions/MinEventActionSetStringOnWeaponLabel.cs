using KFCommonUtilityLib;
using System.Xml.Linq;

public class MinEventActionSetStringOnWeaponLabel : MinEventActionRemoteHoldingBase
{
    private int slot = 0;
    private string text;
    private bool isCvar = false;
    private bool isMetadata = false;

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        bool flag = base.ParseXmlAttribute(_attribute);
        if (!flag)
        {
            flag = true;
            string name = _attribute.Name.LocalName;
            switch (name)
            {
                case "slot":
                    slot = int.Parse(_attribute.Value);
                    break;
                case "text":
                    text = _attribute.Value;
                    break;
                case "cvar":
                    text = _attribute.Value;
                    isCvar = true;
                    isMetadata = false;
                    break;
                case "metadata":
                    text = _attribute.Value;
                    isMetadata = true;
                    isCvar = false;
                    break;
                default:
                    flag = false;
                    break;
            }
        }

        return flag;
    }

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        if (isMetadata && (_params.ItemValue == null || !_params.ItemValue.HasMetadata(text)))
            return false;
        return base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        if (isRemoteHolding || localOnly)
            NetPackageSyncWeaponLabelText.SetWeaponLabelText(_params.Self, slot, isCvar ? _params.Self.GetCVar(text).ToString() : (isMetadata ? _params.ItemValue.GetMetadata(text).ToString() : text));
        else if (!_params.Self.isEntityRemote)
            NetPackageSyncWeaponLabelText.NetSyncSetWeaponLabelText(_params.Self, slot, isCvar ? _params.Self.GetCVar(text).ToString() : (isMetadata ? _params.ItemValue.GetMetadata(text).ToString() : text));
    }
}

