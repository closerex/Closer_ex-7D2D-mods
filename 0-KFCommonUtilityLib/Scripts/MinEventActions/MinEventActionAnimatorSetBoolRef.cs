using System;
using System.Xml.Linq;
using KFCommonUtilityLib;

public class MinEventActionAnimatorSetBoolRef : MinEventActionTargetedBase
{
    public string property;
    public bool value;
    public string valueStr;
    public ValueRefStatType statType;

    public override void Execute(MinEventParams _params)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].emodel != null && targets[i].emodel.avatarController != null && GetValue(_params, targets[i], out bool value))
            {
                targets[i].emodel.avatarController.UpdateBool(property, value, true);
            }
        }
    }

    private bool GetValue(MinEventParams _params, EntityAlive target, out bool realValue)
    {
        switch(statType)
        {
            case ValueRefStatType.Value:
                realValue = value;
                return true;
            case ValueRefStatType.Metadata:
                if (_params.ItemValue != null && _params.ItemValue.Metadata != null)
                {
                    var metadata = _params.ItemValue.GetMetadata(valueStr);
                    if (metadata != null && metadata is not string)
                    {
                        realValue = Convert.ToBoolean(metadata);
                        return true;
                    }
                }
                break;
            case ValueRefStatType.Cvar:
                realValue = target.GetCVar(valueStr) > 0;
                return true;
        }
        realValue = false;
        return false;
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        bool flag = base.ParseXmlAttribute(_attribute);
        if (!flag)
        {
            string localName = _attribute.Name.LocalName;
            if (localName == "property")
            {
                property = _attribute.Value;
                return true;
            }
            if (localName == "value")
            {
                if (bool.TryParse(_attribute.Value, out value))
                {
                    statType = ValueRefStatType.Value;
                    return true;
                }
                else if (_attribute.Value.StartsWith('#'))
                {
                    valueStr = _attribute.Value.Substring(1);
                    statType = ValueRefStatType.Metadata;
                    return true;
                }
                else if (_attribute.Value.StartsWith('@'))
                {
                    valueStr = _attribute.Value.Substring(1);
                    statType = ValueRefStatType.Cvar;
                    return true;
                }
            }
        }
        return flag;
    }
}