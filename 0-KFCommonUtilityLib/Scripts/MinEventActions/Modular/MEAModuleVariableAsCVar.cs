using System.Xml.Linq;
using CodeWriter.ExpressionParser;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;

[TypeTarget(typeof(MinEventActionBase))]
public class MEAModuleVariableAsCVar : CVarReplacer
{
    protected VariableInfo info;
    
    public override bool ParseModuleAttribute(MinEventActionBase parent, XAttribute attr)
    {
        //this requires "var" attribute being defined before other variable attributes
        if (attr.Name.LocalName == "var")
        {
            VariableInfo.TryParse(attr.Value, out info, null, null, ParseCustomVariables);
            return true;
        }

        return info != null && info.ParseAdditionalAttributes(attr);
    }

    protected override float GetCVarOverride(float _originalValue, string _cvarName, MinEventParams _params, int _targetIndex)
    {
        return !validate(_params, _targetIndex) ? _originalValue : info.Evaluate(_params, targets[_targetIndex], _targetIndex, targets.Count);
    }

    private bool validate(MinEventParams _params, int _targetIndex)
    {
        return info != null && (!info.ValidationRequired || info.IsValid(_params, targets[_targetIndex], _targetIndex));
    }
}
