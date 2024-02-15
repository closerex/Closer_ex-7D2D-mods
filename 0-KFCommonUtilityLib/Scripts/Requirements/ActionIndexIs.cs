﻿using System;
using System.Xml.Linq;

public class ActionIndexIs : RequirementBase
{
    protected int index;
    public override bool IsValid(MinEventParams _params)
    {
        //if (!res)
        //{
        //    Log.Out($"Action index is not {index} : {(_params.ItemActionData == null ? "null" : _params.ItemActionData.indexInEntityOfAction.ToString())}\n{StackTraceUtility.ExtractStackTrace()}");
        //}
        return (_params.ItemActionData == null && index == 0) || _params.ItemActionData?.indexInEntityOfAction == index;
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
