using KFCommonUtilityLib.Scripts.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleInspectable
{
    public bool allowEmptyInspect;

    [MethodTargetPostfix(nameof(ItemAction.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        allowEmptyInspect = _props.GetBool("allowEmptyInspect");
    }
}
