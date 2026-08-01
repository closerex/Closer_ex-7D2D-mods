using System;
using System.Xml.Linq;
using KFCommonUtilityLib;
using UnityEngine;

public class MinEventActionSetSkinMaterialProperty : MinEventActionTargetedBase
{
    private bool useHoldingItem;
    private bool useCvarRef;
    private string cvarRef;
    private float value;
    private int[] matIndexFpv;
    private int[] matIndexTpv;
    private string matProp;
    private int matPropId;
    private bool isIntProp;
    
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        if (useHoldingItem)
        {
            return base.CanExecute(_eventType, _params);
        }

        targets.Clear();
        targets.Add(_params.Self);
        return _params.ItemInventoryData != null && _params.ItemInventoryData.model;
    }

    public override void Execute(MinEventParams _params)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var invData = useHoldingItem ? target.inventory?.holdingItemData : _params.ItemInventoryData;
            if (!invData?.model || !invData.model.TryGetComponent<SkinRendererList>(out var skins))
                continue;
            
            var matIndexArray = target.emodel.IsFPV ? matIndexFpv : matIndexTpv;
            for (var j = 0; j < matIndexArray.Length; j++)
            {
                var matIndex = matIndexArray[j];
                var mat = skins.GetMaterial(matIndex, false);
                if (mat == null)
                    continue;
                
                if (useCvarRef)
                {
                    value = target.GetCVar(cvarRef);
                }
                    
                if (isIntProp)
                {
                    mat.SetInteger(matPropId, (int)value);
                }
                else
                {
                    mat.SetFloat(matPropId, value);
                }
            }

        }
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if (base.ParseXmlAttribute(_attribute))
        {
            return true;
        }

        switch (_attribute.Name.LocalName)
        {
            case "use_holding_item":
                useHoldingItem = bool.Parse(_attribute.Value);
                return true;
            case "value":
                useCvarRef = false;
                if (_attribute.Value.StartsWith("@"))
                {
                    useCvarRef = true;
                    cvarRef = _attribute.Value[1..];
                }
                else
                {
                    cvarRef = null;
                    value = float.Parse(_attribute.Value);
                }
                return true;
            case "mat_index_fpv":
                matIndexFpv = Array.ConvertAll(_attribute.Value.Split(','), int.Parse);
                return true;
            case "mat_index_tpv":
                matIndexTpv = Array.ConvertAll(_attribute.Value.Split(','), int.Parse);
                return true;
            case "mat_prop":
                matProp = _attribute.Value;
                matPropId = Shader.PropertyToID(matProp);
                return true;
            case "is_int":
                isIntProp = bool.Parse(_attribute.Value);
                return true;
        }
        return false;
    }
}