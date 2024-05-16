using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

public class IsModificationActivated : RequirementBase
{
    private string modName;
    private int modId = -1;

    public override bool IsValid(MinEventParams _params)
    {
        if (!base.IsValid(_params))
            return false;

        if (modId < 0)
        {
            modId = ItemClass.GetItemClass(modName)?.Id ?? -1;
            if (modId < 0)
                return false;
        }

        if(_params.ItemValue != null)
        {
            if (_params.ItemValue.Modifications != null)
            {
                foreach (var mod in _params.ItemValue.Modifications)
                {
                    if (mod != null && mod.type == modId && mod.Activated > 0)
                    {
                        return !invert;
                    }
                }
            }

            if (_params.ItemValue.CosmeticMods != null)
            {
                foreach (var cos in _params.ItemValue.CosmeticMods)
                {
                    if (cos != null && cos.type == modId && cos.Activated > 0)
                    {
                        return !invert;
                    }
                }
            }
        }

        return invert;
    }

    public override bool ParseXAttribute(XAttribute _attribute)
    {
        if (base.ParseXAttribute(_attribute))
            return true;

        switch (_attribute.Name.LocalName)
        {
            case "mod":
                modName = _attribute.Value;
                return true;
        }

        return false;
    }
}