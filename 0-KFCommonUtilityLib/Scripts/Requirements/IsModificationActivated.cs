using UniLinq;
using System.Xml.Linq;
using UnityEngine;

public class IsModificationActivated : RequirementBase
{
    private string modName;
    private int modId = -1;

    public override bool IsValid(MinEventParams _params)
    {
        if (!base.IsValid(_params))
        {
            return false;
        }
        if (modId < 0)
        {
            modId = ItemClass.GetItemClass(modName)?.Id ?? -1;
            //Log.Out($"modId {modId}");
            if (modId < 0)
                return false;
        }

        //Log.Out($"modName {modName} modId {modId} item {_params?.ItemValue?.ItemClass?.Name ?? "null"} mods{(_params?.ItemValue?.Modifications == null ? ": null" : _params.ItemValue.Modifications.Select(v => $"\n{(v == null || v.IsEmpty() ? "null" : $"item {v.ItemClass.Name} type {v.type} activated {v.Activated}")}").Join())} \ncos{(_params?.ItemValue?.CosmeticMods == null ? ": null" : _params.ItemValue.CosmeticMods.Select(v => $"\n{(v == null || v.IsEmpty() ? "null" : $"item {v.ItemClass.Name} type {v.type} activated {v.Activated}")}").Join())} \n{StackTraceUtility.ExtractStackTrace()}");
        if (_params.ItemValue != null)
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