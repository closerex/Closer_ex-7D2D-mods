using System.Xml;

class MinEventActionSetAmmoOnWeaponLabel : MinEventActionRemoteHoldingBase
{
    private int slot = 0;
    private bool consume_ammo = false;
    private bool useHoldingItemValue = false;
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
                default:
                    flag = false;
                    break;
            }
        }

        return flag;
    }

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        //somehow when onSelfEquipStart is fired, holding item value is not successfully updated in MinEventParams
        useHoldingItemValue = _eventType == MinEventTypes.onSelfEquipStart;
        consume_ammo = _eventType == MinEventTypes.onSelfRangedBurstShot;
        return base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        int meta = useHoldingItemValue ? _params.Self.inventory.holdingItemItemValue.Meta : _params.ItemValue.Meta;
        int num = consume_ammo ? meta - 1 : meta;
        if (isRemoteHolding)
            NetPackageSyncWeaponLabelText.setWeaponLabelText(_params.Self, slot, num.ToString());
        else if(!_params.Self.isEntityRemote)
            NetPackageSyncWeaponLabelText.netSyncSetWeaponLabelText(_params.Self, slot, num.ToString());
    }
}

