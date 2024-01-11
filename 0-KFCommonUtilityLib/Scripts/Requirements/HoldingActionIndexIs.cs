using KFCommonUtilityLib.Scripts.Singletons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class HoldingActionIndexIs : ActionIndexIs
{
    public override bool IsValid(MinEventParams _params)
    {
        return index == 0 || MultiActionManager.GetActionIndexForEntityID(_params.Self?.entityId ?? -1) == index;
    }
}
