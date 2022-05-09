using System.Xml;

class MinEventActionSetStringOnWeaponLabel : MinEventActionBase
{
    private int slot = 0;
    private string text;
    private bool isCvar = false;

    public override bool ParseXmlAttribute(XmlAttribute _attribute)
    {
        bool flag = base.ParseXmlAttribute(_attribute);
        if (!flag)
        {
            flag = true;
            string name = _attribute.Name;
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
        return !_params.Self.isEntityRemote && base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        NetPackageSyncWeaponLabelText.netSyncSetWeaponLabelText(_params.Self, slot, isCvar ? _params.Self.GetCVar(text).ToString() : text);
    }
}

