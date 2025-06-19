using System;
using System.Collections.Generic;
using System.Xml.Linq;
using CodeWriter.ExpressionParser;
using UnityEngine;

public class MinEventActionCVarExpression : MinEventActionTargetedBase
{
    private enum VariableType
    {
        None,
        CVar,
        RandomInt,
        RandomFloat,
        TierList
    }

    private class VariableInfo
    {
        public VariableType varType;
        public string cvarName;
        public float[] valueList;
        public float randomMin;
        public float randomMax;
    }
    public string cvarName;
    public CVarOperation operation;
    private bool isValid = false;
    private ExpressionContext<float> context;
    private Expression<float> compiledExpr; 
    private VariableInfo[] variableInfos;
    private MinEventParams minEventContext;
    private EntityAlive target;

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        if (cvarName != null && cvarName.StartsWith("_"))
        {
            Log.Out("CVar '{0}' is readonly", new object[] { cvarName });
            return false;
        }
        if (!isValid)
        {
            Log.Out("Invalid expression!");
            return false;
        }
        return base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        if (_params.Self.isEntityRemote && !_params.IsLocal)
        {
            return;
        }
        minEventContext = _params;
        if (compiledExpr == null)
        {
            return;
        }
        for (int i = 0; i < targets.Count; i++)
        {
            target = targets[i];
            float cvar = target.Buffs.GetCustomVar(cvarName);
            float value = compiledExpr.Invoke();
            switch (operation)
            {
                case CVarOperation.set:
                case CVarOperation.setvalue:
                    cvar = value;
                    break;
                case CVarOperation.add:
                    cvar += value;
                    break;
                case CVarOperation.subtract:
                    cvar -= value;
                    break;
                case CVarOperation.multiply:
                    cvar *= value;
                    break;
                case CVarOperation.divide:
                    cvar /= ((value == 0f) ? 0.0001f : value);
                    break;
                case CVarOperation.percentadd:
                    cvar += cvar * value;
                    break;
                case CVarOperation.percentsubtract:
                    cvar -= cvar * value;
                    break;
            }
            target.Buffs.SetCustomVar(cvarName, cvar);
        }
        minEventContext = null;
        target = null;
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
                    context = new ExpressionContext<float>();
                    List<VariableInfo> variableInfos = new List<VariableInfo>();
                    Dictionary<string, int> varStrs = new Dictionary<string, int>();
                    while (true)
                    {
                        int nextVarStart = expr.IndexOf('[');
                        if (nextVarStart < 0)
                        {
                            break;
                        }
                        int nextVarEnd = expr.IndexOf(']', nextVarStart);
                        if (nextVarEnd < 0)
                        {
                            isValid = false;
                            break;
                        }
                        string varStr = expr.Substring(nextVarStart + 1, nextVarEnd - nextVarStart - 1);
                        VariableInfo variableInfo = null;
                        if (varStr.StartsWith("@"))
                        {
                            if (!varStrs.ContainsKey(varStr))
                            {
                                variableInfo = new VariableInfo();
                                variableInfo.varType = VariableType.CVar;
                                variableInfo.cvarName = varStr.Substring(1);
                                varStrs.Add(varStr, variableInfos.Count);
                            }
                        }
                        else if (varStr.StartsWith("randomInt", StringComparison.OrdinalIgnoreCase))
                        {
                            variableInfo = new VariableInfo();
                            variableInfo.varType = VariableType.RandomInt;
                            Vector2 vector = StringParsers.ParseVector2(varStr.Substring(varStr.IndexOf('(') + 1, varStr.IndexOf(')') - (varStr.IndexOf('(') + 1)));
                            variableInfo.randomMin = (int)vector.x;
                            variableInfo.randomMax = (int)vector.y;
                        }
                        else if (varStr.StartsWith("randomFloat", StringComparison.OrdinalIgnoreCase))
                        {
                            variableInfo = new VariableInfo();
                            variableInfo.varType = VariableType.RandomFloat;
                            Vector2 vector = StringParsers.ParseVector2(varStr.Substring(varStr.IndexOf('(') + 1, varStr.IndexOf(')') - (varStr.IndexOf('(') + 1)));
                            variableInfo.randomMin = vector.x;
                            variableInfo.randomMax = vector.y;
                        }
                        else if (varStr.Contains(','))
                        {
                            if (!varStrs.ContainsKey(varStr))
                            {
                                variableInfo = new VariableInfo();
                                variableInfo.varType = VariableType.TierList;
                                string[] array = varStr.Split(',', StringSplitOptions.None);
                                variableInfo.valueList = new float[array.Length];
                                for (int i = 0; i < array.Length; i++)
                                {
                                    variableInfo.valueList[i] = float.Parse(array[i]);
                                }
                                varStrs.Add(varStr, variableInfos.Count);
                            }
                        }
                        else if (float.TryParse(varStr, out _))
                        {
                            
                            expr = expr.Remove(nextVarEnd).Remove(nextVarStart);
                        }
                        else
                        {
                            isValid = false;
                            break;
                        }
                        int curIndex = varStrs.TryGetValue(varStr, out var index) ? index : variableInfos.Count;
                        string varName = "x" + curIndex;
                        expr = expr.Remove(nextVarStart, nextVarEnd - nextVarStart + 1).Insert(nextVarStart, varName);
                        //Log.Out($"cur index {curIndex} var name {varStr} is new var {curIndex == variableInfos.Count}");
                        if (curIndex == variableInfos.Count)
                        {
                            context.RegisterVariable(varName, () => { return EvaluateVar(curIndex); });
                        }
                        if (variableInfo != null)
                        {
                            variableInfos.Add(variableInfo);
                        }
                    }
                    if (!isValid)
                    {
                        Log.Out("Invalid expression: {0}", new object[] { expr });
                        return false;
                    }
                    Log.Out($"Compiling expr {expr}...");
                    compiledExpr = FloatExpressionParser.Instance.Compile(expr, context, true);
                    this.variableInfos = variableInfos.ToArray();
                    flag = true;
                    break;
                case "operation":
                    this.operation = EnumUtils.Parse<CVarOperation>(_attribute.Value, true);
                    flag = true;
                    break;
            }
        }
        return flag;
    }

    private float EvaluateVar(int index)
    {
        var variableInfo = variableInfos[index];
        switch (variableInfo.varType)
        {
            case VariableType.CVar:
                return target.Buffs.GetCustomVar(variableInfo.cvarName);
            case VariableType.RandomInt:
                return Mathf.Clamp(minEventContext.Self.rand.RandomRange((int)variableInfo.randomMin, (int)variableInfo.randomMax + 1), variableInfo.randomMin, variableInfo.randomMax);
            case VariableType.RandomFloat:
                return Mathf.Clamp(minEventContext.Self.rand.RandomRange(variableInfo.randomMin, variableInfo.randomMax + 1), variableInfo.randomMin, variableInfo.randomMax);
            case VariableType.TierList:
                if (minEventContext.ParentType == MinEffectController.SourceParentType.ItemClass || minEventContext.ParentType == MinEffectController.SourceParentType.ItemModifierClass)
                {
                    if (!minEventContext.ItemValue.IsEmpty())
                    {
                        int tier = (int)(minEventContext.ItemValue.Quality - 1);
                        if (tier >= 0)
                        {
                            return variableInfo.valueList[tier];
                        }
                    }
                }
                else if (minEventContext.ParentType == MinEffectController.SourceParentType.ProgressionClass && minEventContext.ProgressionValue != null)
                {
                    int level = minEventContext.ProgressionValue.CalculatedLevel(minEventContext.Self);
                    if (level >= 0)
                    {
                        return variableInfo.valueList[level];
                    }
                }
                return 0f;
            default:
                return 0f;
        }
    }
}