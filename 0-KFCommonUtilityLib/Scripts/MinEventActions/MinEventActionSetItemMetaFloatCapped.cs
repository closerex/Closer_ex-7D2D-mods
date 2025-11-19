using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;

public class MinEventActionSetItemMetaFloatCapped : MinEventActionSetItemMetaFloat
{
    private float minValue = 0;
    private float maxValue = float.MaxValue;
    public override void Execute(MinEventParams _params)
    {
        ItemValue itemValue = _params.ItemValue;
        if (!itemValue.HasMetadata(metaKey, TypedMetadataValue.TypeTag.None))
        {
            itemValue.SetMetadata(metaKey, minValue, "float");
        }
        object metadata = itemValue.GetMetadata(metaKey);
        if (!(metadata is float))
        {
            return;
        }
        if (relative)
        {
            itemValue.SetMetadata(metaKey, Mathf.Clamp((float)metadata + change, minValue, maxValue), "float");
        }
        else
        {
            itemValue.SetMetadata(metaKey, Mathf.Clamp(change, minValue, maxValue), "float");
        }
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if (!base.ParseXmlAttribute(_attribute))
        {
            string localName = _attribute.Name.LocalName;
            if (localName == "min_cap")
            {
                minValue = float.Parse(_attribute.Value);
                return true;
            }
            else if (localName == "max_cap")
            {
                maxValue = float.Parse(_attribute.Value);
                return true;
            }
        }
        return false;
    }
}