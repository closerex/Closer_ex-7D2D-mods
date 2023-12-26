using System;
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
        return _params.ItemActionData?.indexInEntityOfAction == index || index == 0;
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
