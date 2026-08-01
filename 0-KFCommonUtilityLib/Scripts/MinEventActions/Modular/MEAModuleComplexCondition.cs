using System;
using System.Collections.Generic;
using System.Xml.Linq;
using CodeWriter.ExpressionParser;
using HarmonyLib;
using JetBrains.Annotations;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;

[TypeTarget(typeof(MinEventActionBase))]
public class MEAModuleComplexCondition : ICustomXElementParser
{
    private enum VariableSide
    {
        Both,
        Left,
        Right
    }
    
    protected class Evaluator
    {
        private enum EvaluationType
        {
            PlainValue,
            Variable,
            Expression
        }
        private readonly EvaluationType type;
        private readonly float value;
        private readonly VariableInfo variableInfo;
        private readonly MinEventEvaluator evaluator;

        public Evaluator(float value)
        {
            this.value = value;
            type = EvaluationType.PlainValue;
        }
        
        public Evaluator(VariableInfo variableInfo)
        {
            this.variableInfo = variableInfo;
            type = EvaluationType.Variable;
        }

        public Evaluator(MinEventEvaluator evaluator)
        {
            this.evaluator = evaluator;
            type = EvaluationType.Expression;
        }

        public bool IsValid(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            return type switch
            {
                EvaluationType.PlainValue => true,
                EvaluationType.Variable => !variableInfo.ValidationRequired || variableInfo.IsValid(_params, _target, _targetIndex),
                EvaluationType.Expression => evaluator.IsValid(_params, _target, _targetIndex),
                _ => false
            };
        }

        public float Evaluate(MinEventParams _params, EntityAlive _target, int _targetIndex, int _targetCount)
        {
            return type switch
            {
                EvaluationType.PlainValue => value,
                EvaluationType.Variable => variableInfo.Evaluate(_params, _target, _targetIndex, _targetCount),
                EvaluationType.Expression => evaluator.Evaluate(_params, _target, _targetIndex, _targetCount),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public bool Parse(XElement elem)
        {
            switch (type)
            {
                case EvaluationType.PlainValue:
                    return false;
                case EvaluationType.Variable:
                    foreach (var attr in elem.Attributes())
                    {
                        _ = variableInfo.ParseAdditionalAttributes(attr);
                    }
                    return true;
                case EvaluationType.Expression:
                    evaluator.ParseAdditionalAttributes(elem);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    
    protected readonly List<EntityAlive> targets = new();
    protected readonly List<int> removeIndicesCache = new();
    protected Evaluator leval, reval;
    protected RequirementBase.OperationTypes compareType;
    
    [HarmonyPatch(nameof(MinEventActionBase.CanExecute)), MethodTargetPostfix]
    public void Postfix_CanExecute(MinEventParams _params, ref bool __result)
    {
        targets.Clear();
        if (__result)
        {
            targets.Add(_params.Self);
            checkTargets(_params, targets);
            __result = targets.Count > 0;
        }
    }

    [HarmonyPatch(typeof(MinEventActionTargetedBase), nameof(MinEventActionBase.CanExecute)), MethodTargetPostfix]
    public void Postfix_MinEventActionTargetedBase_CanExecute(MinEventParams _params, MinEventActionTargetedBase __instance, ref bool __result)
    {
        targets.Clear();
        if (__result)
        {
            checkTargets(_params, __instance.targets);
            __result = __instance.targets.Count > 0;
            if (__result)
            {
                targets.AddRange(__instance.targets);
            }
        }
    }

    private void checkTargets(MinEventParams _params, List<EntityAlive> _targets)
    {
        removeIndicesCache.Clear();
        //we need to loop forward for evalOnce to work properly
        for (int i = 0; i < _targets.Count; i++)
        {
            var target = _targets[i];
            if (leval.IsValid(_params, target, i) && reval.IsValid(_params, target, i))
            {
                var lval = leval.Evaluate(_params, target, i, _targets.Count);
                var rval = reval.Evaluate(_params, target, i, _targets.Count);
                // Log.Out($"comparing {lval} -> {rval} op: {compareType} self: {_params.Self.GetDebugName()} target: {target.GetDebugName()}");
                if (RequirementBase.compareValues(lval, compareType, rval))
                {
                    continue;
                }
            }
            removeIndicesCache.Add(i);
        }

        for (int i = removeIndicesCache.Count - 1; i >= 0; i--)
        {
            _targets.RemoveAt(removeIndicesCache[i]);
        }
    }

    public bool ParseModuleAttribute(MinEventActionBase parent, XAttribute attr)
    {
        switch (attr.Name.LocalName)
        {
            case "op":
            case "operation":
                compareType = EnumUtils.Parse<RequirementBase.OperationTypes>(attr.Value, true);
                return true;
            case "l_val":
            case "r_val":
            case "l_value":
            case "r_value":
            {
                ref var eval = ref checkEvaluator(attr.Name.LocalName);
                eval = new Evaluator(float.Parse(attr.Value));
                return true;
            }
            case "l_var":
            case "r_var":
            case "l_variable":
            case "r_variable":
            {
                ref var eval = ref checkEvaluator(attr.Name.LocalName);
                if (!VariableInfo.TryParse(attr.Value, out var variableInfo, null, null, ParseCustomVariables))
                    throw new ArgumentException($"{attr.Value} is not a valid variable!");
                eval = new Evaluator(variableInfo);
                return true;
            }
            case "l_expr":
            case "r_expr":
            case "l_expression":
            case "r_expression":
            {
                ref var eval = ref checkEvaluator(attr.Name.LocalName);
                if (!MinEventEvaluator.TryParse(attr.Value, out var evaluator, ParseCustomVariables))
                    throw new ArgumentException($"{attr.Value} is not a valid evaluator!");
                eval = new Evaluator(evaluator);
                return true;
            }
        }
        return false;
    }

    private ref Evaluator checkEvaluator(string name)
    {
        if (name.StartsWith("l_"))
            return ref leval;
        if (name.StartsWith("r_"))
            return ref reval;
        throw new ArgumentException($"{name} does not specify the side of the evaluator!");
    }

    public bool ParseModuleElements(MinEventActionBase parent, XElement elem)
    {
        if (leval == null)
            throw new ArgumentNullException(nameof(leval));
        if (reval == null)
            throw new ArgumentNullException(nameof(reval));
        
        var side = VariableSide.Both;
        if (elem.HasAttribute("side"))
        {
            EnumUtils.TryParse(elem.GetAttribute("side"), out side, true);
        }

        switch (side)
        {
            case VariableSide.Both:
                leval.Parse(elem);
                reval.Parse(elem);
                return true;
            case VariableSide.Left:
                leval.Parse(elem);
                return true;
            case VariableSide.Right:
                reval.Parse(elem);
                return true;
        }

        return false;
    }

    protected virtual VariableInfo ParseCustomVariables(string varStr) => null;
}