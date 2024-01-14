using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ItemActionIndexIs : ActionIndexIs
{
    public override bool IsValid(MinEventParams _params)
    {
        return (_params.ItemValue == null && index == 0) || _params.ItemValue?.GetActionIndexForItemValue() == index;
    }
}