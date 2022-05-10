using System.Xml;

class MinEventActionSetStringOnWeaponLabel : MinEventActionRemoteHoldingBase
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

    public override void Execute(MinEventParams _params)
    {
        if (isRemoteHolding)
            NetPackageSyncWeaponLabelText.setWeaponLabelText(_params.Self, slot, isCvar ? _params.Self.GetCVar(text).ToString() : text);
        else if(!_params.Self.isEntityRemote)
            NetPackageSyncWeaponLabelText.netSyncSetWeaponLabelText(_params.Self, slot, isCvar ? _params.Self.GetCVar(text).ToString() : text);
    }
}

