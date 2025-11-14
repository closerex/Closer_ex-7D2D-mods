using System;
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
        if (!base.IsValid(_params) || _params.ItemActionData == null)
            return false;

        var res = _params.ItemActionData.indexInEntityOfAction == index;
        return invert ? !res : res;
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
