﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

public class ActionIndexIs : RequirementBase
{
    protected int index;
    public override bool IsValid(MinEventParams _params)
    {
        return index == 0 || _params.ItemActionData?.indexInEntityOfAction == index;
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