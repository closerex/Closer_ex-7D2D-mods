using System.Xml.Linq;
using CodeWriter.ExpressionParser;

public class MinEventActionCVarExpression : MinEventActionTargetedBase
{
    private string cvarName;
    private CVarOperation operation;
    private bool isValid;
    private MinEventEvaluator evaluator;

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        if (cvarName != null && cvarName.StartsWith("_"))
        {
            Log.Out("CVar '{0}' is readonly", new object[] { cvarName });
            return false;
        }

        if (isValid)
        {
            return base.CanExecute(_eventType, _params);
        }
        
        Log.Out("Invalid expression!");
        return false;
    }

    public override void Execute(MinEventParams _params)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (!evaluator.IsValid(_params, target, i))
                continue;
            target.Buffs.SetCustomVar(cvarName, evaluator.Evaluate(_params, target, i, targets.Count),
                (target.isEntityRemote && !_params.Self.isEntityRemote) ||
                (!target.isEntityRemote && !_params.Self.isEntityRemote && target != _params.Self) || _params.IsLocal,
                operation);
        }
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        bool flag = base.ParseXmlAttribute(_attribute);
        if (!flag)
        {
            switch(_attribute.Name.LocalName)
            {
                case "cvar":
                    cvarName = _attribute.Value;
                    flag = true;
                    break;
                case "expression":
                    isValid = true;
                    string expr = _attribute.Value;
                    isValid = MinEventEvaluator.TryParse(expr, out evaluator);
                    if (isValid)
                    {
                        foreach (var child in _attribute.Parent.Elements())
                        {
                            evaluator.ParseAdditionalAttributes(child);
                        }
                    }
                    flag = true;
                    break;
                case "operation":
                    operation = EnumUtils.Parse<CVarOperation>(_attribute.Value, true);
                    flag = true;
                    break;
            }
        }
        return flag;
    }
}