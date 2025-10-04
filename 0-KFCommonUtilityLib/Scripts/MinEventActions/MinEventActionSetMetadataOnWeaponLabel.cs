using KFCommonUtilityLib;
using System.Xml.Linq;

public class MinEventActionSetMetadataOnWeaponLabel : MinEventActionRemoteHoldingBase
{
    private int slot = 0;
    private bool useHoldingItemValue = false;
    private string[] wrap;
    private bool usePattern = false;
    private string metadata;

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
                    wrap = str.Split(new string[] { "[metadata]" }, System.StringSplitOptions.None);
                    usePattern = true;
                    break;
                case "metadata":
                    metadata = _attribute.Value;
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
        return !string.IsNullOrEmpty(metadata) && base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        var inv = _params.Self.inventory;
        var value = useHoldingItemValue ? inv.holdingItemItemValue : _params.ItemValue;
        object obj = value.GetMetadata(metadata);
        if (obj is false || obj is null)
        {
            return;
        }
        string meta = obj.ToString();
        string str = usePattern ? string.Join(meta.ToString(), wrap) : meta.ToString();
        if (isRemoteHolding || localOnly)
            NetPackageSyncWeaponLabelText.SetWeaponLabelText(_params.Self, slot, str);
        else if (!_params.Self.isEntityRemote)
            NetPackageSyncWeaponLabelText.NetSyncSetWeaponLabelText(_params.Self, slot, str);
    }
}