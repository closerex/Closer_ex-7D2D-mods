﻿using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using System.Xml.Linq;

public class HoldingFireModeIs : RequirementBase
{
    protected int index;
    public override bool IsValid(MinEventParams _params)
    {
        bool res = false;
        if (_params.Self && _params.Self?.inventory?.holdingItemData?.actionData[MultiActionManager.GetActionIndexForEntity(_params.Self)] is IModuleContainerFor<ActionModuleFireModeSelector.FireModeData> dataModule)
        {
            res = dataModule.Instance.currentFireMode == index;
        }
        return invert ? !res : res;
    }

    public override bool ParamsValid(MinEventParams _params)
    {
        return true;
    }

    public override bool ParseXAttribute(XAttribute _attribute)
    {
        if (_attribute.Name == "index")
        {
            index = Math.Max(int.Parse(_attribute.Value), 0);
            return true;
        }
        return false;
    }
}