using System.Xml.Linq;
using CodeWriter.ExpressionParser;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;

[TypeTarget(typeof(MinEventActionBase))]
public class MEAModuleExpressionAsCVar : CVarReplacer
{
    private MinEventEvaluator evaluator;
    
    public override bool ParseModuleAttribute(MinEventActionBase parent, XAttribute attr)
    {
        return attr.Name.LocalName is "expression" or "expr" && MinEventEvaluator.TryParse(attr.Value, out evaluator, ParseCustomVariables);
    }

    public override bool ParseModuleElements(MinEventActionBase parent, XElement elem)
    {
        if (evaluator == null)
        {
            return false;
        }

        evaluator.ParseAdditionalAttributes(elem);

        return true;
    }

    protected override float GetCVarOverride(float _originalValue, string _cvarName, MinEventParams _params, int _targetIndex)
    {
        return !validate(_params, _targetIndex) ? _originalValue : evaluator.Evaluate(_params, targets[_targetIndex], _targetIndex, targets.Count);
    }

    private bool validate(MinEventParams _params, int _targetIndex)
    {
        return evaluator != null && evaluator.IsValid(_params, targets[_targetIndex], _targetIndex);
    }
}
