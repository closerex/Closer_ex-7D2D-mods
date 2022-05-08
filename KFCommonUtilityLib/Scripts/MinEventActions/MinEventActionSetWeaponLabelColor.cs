using System.Xml;
using UnityEngine;

class MinEventActionSetWeaponLabelColor : MinEventActionBase
{
    private bool isText = true;
    private int slot0 = 0;
    private int slot1 = 0;
    private Color color;
    private int nameId;

    public override bool ParseXmlAttribute(XmlAttribute _attribute)
    {
        bool flag = base.ParseXmlAttribute(_attribute);
        if(!flag)
        {
            string value = _attribute.Value;
            flag = true;
            switch(_attribute.Name)
            {
                case "is_text":
                    isText = bool.Parse(value);
                    break;
                case "slot0":
                    slot0 = int.Parse(value);
                    break;
                case "slot1":
                    slot1 = int.Parse(value);
                    break;
                case "color":
                    flag = ColorUtility.TryParseHtmlString(value, out color);
                    break;
                case "name":
                    nameId = Shader.PropertyToID(value);
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
        NetPackageSyncWeaponLabelColor.netSyncSetWeaponLabelColor(_params.Self, isText, slot0, color, slot1, nameId);
    }
}

