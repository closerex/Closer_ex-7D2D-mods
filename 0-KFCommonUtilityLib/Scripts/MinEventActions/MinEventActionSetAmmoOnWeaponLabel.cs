using KFCommonUtilityLib;
using System.Xml.Linq;

public class MinEventActionSetAmmoOnWeaponLabel : MinEventActionRemoteHoldingBase
{
    private int slot = 0;
    private bool maxAmmo = false;
    private bool useHoldingItemValue = false;
    private string[] wrap;
    private bool usePattern = false;
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
                case "pattern":
                    string str = _attribute.Value;
                    wrap = str.Split(new string[] { "[ammo]" }, System.StringSplitOptions.None);
                    usePattern = true;
                    break;
                case "max_ammo":
                    maxAmmo = bool.Parse(_attribute.Value);
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
        //consume_ammo = _eventType == MinEventTypes.onSelfRangedBurstShot;
        return base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        int meta;
        var inv = _params.Self.inventory;
        var value = useHoldingItemValue ? inv.holdingItemItemValue : _params.ItemValue;
        if (!maxAmmo)
            meta = value.Meta;
        else
            meta = (int)EffectManager.GetValue(PassiveEffects.MagazineSize, value, inv.GetHoldingGun().BulletsPerMagazine, _params.Self);
        string str = usePattern ? string.Join(meta.ToString(), wrap) : meta.ToString();
        //int num = consume_ammo ? meta - 1 : meta;
        if (isRemoteHolding || localOnly)
            NetPackageSyncWeaponLabelText.SetWeaponLabelText(_params.Self, slot, str);
        else if (!_params.Self.isEntityRemote)
            NetPackageSyncWeaponLabelText.NetSyncSetWeaponLabelText(_params.Self, slot, str);
    }
}

