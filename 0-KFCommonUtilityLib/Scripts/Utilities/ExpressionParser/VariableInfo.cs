using System;
using System.Collections.Generic;
using System.Xml.Linq;
using JetBrains.Annotations;
using KFCommonUtilityLib;
using UnityEngine;

namespace CodeWriter.ExpressionParser
{
    using LevelPair = KeyValuePair<float, float>;
    
    public abstract class VariableInfo
    {
        public string VarName { get; private set; }
        public int VarIndex { get; set; }
        public string ExprLiteral { [UsedImplicitly] get; private set; }
        public float LastEvalValue { get; private set; }

        public bool ValidationRequired { get; private set; }
        public abstract bool IsUnique { get; }

        protected float DefaultValue => defaultValueInfo?.LastEvalValue ?? defaultValue;

        protected bool useSelfInsteadOfTarget = true;
        private bool evalOnceForAllTargets;
        private bool wasValid;
        private float defaultValue;
        private VariableInfo defaultValueInfo;

        public float Evaluate(MinEventParams _params, EntityAlive _target, int _targetIndex, int _targetCount)
        {
            if (!evalOnceForAllTargets || _targetIndex == 0)
            {
                LastEvalValue = DoEvaluate(_params, _target, _targetIndex, _targetCount);
            }
            return LastEvalValue;
        }

        protected abstract float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex, int _targetCount);

        public bool IsValid(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            if (!evalOnceForAllTargets || _targetIndex == 0)
            {
                wasValid = IsValidImpl(_params, _target, _targetIndex);
            }
            return wasValid;
        }
        
        //while IsValid is not always called (only when ValidationRequired is true),
        //it should be guaranteed that if both it and Evaluate are called then all your cached data is valid
        //in other words, if cached data is invalid when Evaluate is called, it should calculate those data again
        //and there's always only 1 cache calculation in total
        protected virtual bool IsValidImpl(MinEventParams _params, EntityAlive _target, int _targetIndex) => true;

        public virtual bool ParseAdditionalAttributes(XAttribute attr)
        {
            switch (attr.Name.LocalName)
            {
                case "validate":
                    ValidationRequired = bool.Parse(attr.Value);
                    return true;
                case "use_self_target":
                    useSelfInsteadOfTarget = bool.Parse(attr.Value);
                    return true;
            }
            return false;
        }

        protected abstract bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children);

        public static bool TryParse(string varStr, out VariableInfo variableInfo, Dictionary<string, VariableInfo> children, string alias = null, Func<string, VariableInfo> variableParser = null)
        {
            varStr = varStr.Replace(" ", "");
            alias = alias?.Replace(" ", "");
            string parsedStr = varStr;
            if (varStr.StartsWith("[") && varStr.EndsWith("]"))
                parsedStr = parsedStr[1..^1];
            bool evalOnce = parsedStr.StartsWith('~');
            if (evalOnce)
                parsedStr = parsedStr[1..];
            float defaultValue = 0f;
            VariableInfo defaultValueInfo = null;
            int pipeIndex = parsedStr.LastIndexOf('|');
            if (pipeIndex > 0)
            {
                var def = parsedStr[(pipeIndex + 1)..];
                if (float.TryParse(def, out defaultValue) || children.TryGetValue(def, out defaultValueInfo))
                {
                    parsedStr = parsedStr[..pipeIndex];
                }
            }

            variableInfo = variableParser?.Invoke(parsedStr);
            if (variableInfo == null)
            {
                if (parsedStr.StartsWith("@"))
                {
                    variableInfo = new CvarInfo();
                }
                else if (parsedStr.StartsWith("#"))
                {
                    variableInfo = new MetadataInfo();
                }
                else if (parsedStr.StartsWith('$'))
                {
                    variableInfo = new LocalCacheInfo();
                }
                else if (parsedStr.StartsWith('%') && parsedStr.Contains('='))
                {
                    variableInfo = new ProgressionLevelInfo();
                }
                else if (parsedStr.StartsWith("tier(", StringComparison.OrdinalIgnoreCase) && parsedStr.Contains('='))
                {
                    variableInfo = new ItemTierInfo();
                }
                else if (parsedStr.StartsWith('^'))
                {
                    variableInfo = new PassiveInfo();
                }
                else if (parsedStr.StartsWith("randomInt(", StringComparison.OrdinalIgnoreCase))
                {
                    variableInfo = new RandomIntInfo();
                }
                else if (parsedStr.StartsWith("randomFloat(", StringComparison.OrdinalIgnoreCase))
                {
                    variableInfo = new RandomFloatInfo();
                }
                else if (parsedStr.StartsWith("stat(", StringComparison.OrdinalIgnoreCase) && parsedStr.Contains(','))
                {
                    variableInfo = new StatInfo();
                }
                else if (parsedStr.StartsWith("expr=", StringComparison.OrdinalIgnoreCase))
                {
                    variableInfo = new ExpressionInfo();
                }
                else if (parsedStr.EqualsCaseInsensitive("targetIndex"))
                {
                    variableInfo = new TargetIndexInfo();
                }
                else if (parsedStr.EqualsCaseInsensitive("targetCount"))
                {
                    variableInfo = new TargetCountInfo();
                }
                else if (parsedStr.EqualsCaseInsensitive("distance"))
                {
                    variableInfo = new DistanceInfo();
                }
                else if (parsedStr.EqualsCaseInsensitive("hpDamage"))
                {
                    variableInfo = new HPDamageInfo();
                }
                else if (parsedStr.EqualsCaseInsensitive("armorDamage"))
                {
                    variableInfo = new ArmorDamageInfo();
                }
            }

            if (variableInfo == null || !variableInfo.TryParse(parsedStr, children))
                return false;
            
            variableInfo.VarName = string.IsNullOrEmpty(alias) ? varStr : alias;
            variableInfo.ExprLiteral = varStr;
            variableInfo.evalOnceForAllTargets = evalOnce;
            variableInfo.defaultValue = defaultValue;
            variableInfo.defaultValueInfo = defaultValueInfo;
            return true;
        }
        
        protected EntityAlive getEntityTarget(MinEventParams _params, EntityAlive _target)
        {
            return useSelfInsteadOfTarget ? _params.Self : _target;
        }
    }

    public class CvarInfo : VariableInfo
    {
        private string cvarName;
        
        public override bool IsUnique => true;
        
        private EntityBuffs targetCache;
        
        public CvarInfo()
        {
            useSelfInsteadOfTarget = false;
        }

        protected override bool IsValidImpl(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            targetCache = getTarget(_params, _target);
            return targetCache != null && targetCache.HasCustomVar(cvarName);
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            cvarName = parsedStr[1..];
            return true;
        }

        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex,
            int _targetCount)
        {
            var value = (targetCache ?? getTarget(_params, _target))?.GetCustomVar(cvarName) ?? DefaultValue;
            //Log.Out($"Evaluate CVar {variableInfo.cvarName} = {value}");
            targetCache = null;
            return value;
        }

        private EntityBuffs getTarget(MinEventParams _params, EntityAlive _target)
        {
            var entity = getEntityTarget(_params, _target);
            return entity ? entity.Buffs : null;
        }
    }

    public class MetadataInfo : VariableInfo
    {
        private string metadataName;
        private bool useHoldingItem;
        
        public override bool IsUnique => true;

        private TypedMetadataValue metaCache;

        protected override bool IsValidImpl(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            metaCache = getTarget(_params, _target);
            return metaCache != null && metaCache.GetTypeTag() == TypedMetadataValue.TypeTag.Float;
        }

        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex,
            int _targetCount)
        {
            var value = (metaCache ?? getTarget(_params, _target))?.GetValue() is float res ? res : DefaultValue;
            metaCache = null;
            return value;
        }

        private TypedMetadataValue getTarget(MinEventParams _params, EntityAlive _target)
        {
            if (useHoldingItem)
            {
                var entity = getEntityTarget(_params, _target);
                var holdingItemValue = entity ? entity.inventory?.holdingItemItemValue : null;
                if (holdingItemValue?.Metadata == null || !holdingItemValue.Metadata.TryGetValue(metadataName, out var metaData))
                {
                    return null;
                }

                return metaData;
            }

            {
                if (_params.ItemValue?.Metadata == null || !_params.ItemValue.Metadata.TryGetValue(metadataName, out var metaData))
                {
                    return null;
                }
                return metaData;
            }
        }

        public override bool ParseAdditionalAttributes(XAttribute attr)
        {
            if (base.ParseAdditionalAttributes(attr))
                return true;
            
            switch (attr.Name.LocalName)
            {
                case "use_holding_item":
                    useHoldingItem = bool.Parse(attr.Value);
                    return true;
            }

            return false;
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            metadataName = parsedStr[1..];
            return true;
        }
    }

    public class LocalCacheInfo : VariableInfo
    {
        private int targetHash;
        private int actionIndex = -1;
        private bool useHoldingItem;
        
        public override bool IsUnique => true;

        private ActionModuleLocalPassiveCache.LocalPassiveCacheData customDataCache;

        protected override bool IsValidImpl(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            customDataCache = getTarget(_params, _target);
            return customDataCache != null && customDataCache.HasCachedValue(targetHash);
        }

        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex,
            int _targetCount)
        {
            var value = (customDataCache ?? getTarget(_params, _target))?.GetCachedValue(targetHash) ?? DefaultValue;
            customDataCache = null;
            return value;
        }

        private ActionModuleLocalPassiveCache.LocalPassiveCacheData getTarget(MinEventParams _params,
            EntityAlive _target)
        {
            ItemActionData actionData;
            
            if (useHoldingItem)
            {
                var target = getEntityTarget(_params, _target);
                var invData = target ? target.inventory?.holdingItemData : null;
                if (invData?.actionData == null)
                    return null;
                int realActionIndex = actionIndex >= 0 ? actionIndex : MultiActionManager.GetActionIndexForEntity(target);
                if (realActionIndex < 0 || realActionIndex >= invData.actionData.Count)
                    return null;
                
                actionData = invData.actionData[realActionIndex];
            }
            else
            {
                if (actionIndex >= 0)
                {
                    if (_params.ItemInventoryData?.actionData == null || _params.ItemInventoryData.actionData.Count <= actionIndex)
                        return null;

                    actionData = _params.ItemInventoryData.actionData[actionIndex];
                }
                else
                {
                    actionData = _params.ItemActionData;
                }
            }

            return actionData.LoadModule<ActionModuleLocalPassiveCache.LocalPassiveCacheData>();
        }

        public override bool ParseAdditionalAttributes(XAttribute attr)
        {
            if (base.ParseAdditionalAttributes(attr))
                return true;
            
            switch (attr.Name.LocalName)
            {
                case "use_holding_item":
                    useHoldingItem = bool.Parse(attr.Value);
                    return true;
            }

            return false;
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            parsedStr = parsedStr[1..];
            if (parsedStr.Contains('('))
            {
                var index = parsedStr.Substring(parsedStr.IndexOf('(') + 1,
                    parsedStr.IndexOf(')') - (parsedStr.IndexOf('(') + 1));
                if (!int.TryParse(index, out actionIndex))
                    return false;
                parsedStr = parsedStr[..parsedStr.IndexOf('(')];
            }
            targetHash = parsedStr.GetHashCode();
            return true;
        }
    }

    public class RandomIntInfo : VariableInfo
    {
        private Vector2Int rangeInt;
        private readonly VariableInfo[] varValueList = new VariableInfo[2];

        public override bool IsUnique => false;
        
        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex,
            int _targetCount)
        {
            var entity = getEntityTarget(_params, _target);
            var randomInt = new IntRange(varValueList[0] != null ? (int)varValueList[0].LastEvalValue : rangeInt.x,
                                         varValueList[1] != null ? (int)varValueList[1].LastEvalValue : rangeInt.y);
            if (randomInt.max < randomInt.min)
                return DefaultValue;
            return entity ? randomInt.Random(entity.rand ?? GameRandomManager.Instance.GetTempGameRandom(entity.entityId)) : DefaultValue;
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            var range = parsedStr.Substring(parsedStr.IndexOf('(') + 1,
                parsedStr.IndexOf(')') - (parsedStr.IndexOf('(') + 1));
            var split = range.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (split.Length != 2)
                return false;
            if (!int.TryParse(split[0], out var min) && (children == null || !children.TryGetValue(split[0], out varValueList[0])))
                return false;
            if (!int.TryParse(split[1], out var max) && (children == null || !children.TryGetValue(split[1], out varValueList[1])))
                return false;
            rangeInt = new Vector2Int(min, max);
            return true;
        }
    }

    public class RandomFloatInfo : VariableInfo
    {
        private Vector2 rangeFloat;
        private readonly VariableInfo[] varValueList = new VariableInfo[2];

        public override bool IsUnique => false;

        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex,
            int _targetCount)
        {
            var entity = getEntityTarget(_params, _target);
            var randomFloat = new FloatRange(varValueList[0]?.LastEvalValue ?? rangeFloat.x,
                                             varValueList[1]?.LastEvalValue ?? rangeFloat.y);
            if (randomFloat.max < randomFloat.min)
                return DefaultValue;
            return entity ? randomFloat.Random(entity.rand ?? GameRandomManager.Instance.GetTempGameRandom(entity.entityId)) : DefaultValue;
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            var range = parsedStr.Substring(parsedStr.IndexOf('(') + 1,
                parsedStr.IndexOf(')') - (parsedStr.IndexOf('(') + 1));
            var split = range.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (split.Length != 2)
                return false;
            if (!float.TryParse(split[0], out var min) && (children == null || !children.TryGetValue(split[0], out varValueList[0])))
                return false;
            if (!float.TryParse(split[1], out var max) && (children == null || !children.TryGetValue(split[1], out varValueList[1])))
                return false;
            rangeFloat = new Vector2(min, max);
            return true;
        }
    }

    public abstract class LevelInfoAbs : VariableInfo
    {
        private LevelPair[] valuePairs;
        private VariableInfo[] varValueList;
        public override bool IsUnique => true;
        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            return parseLevelPair(parsedStr, children);
        }

        protected float getValueByLevel(float level)
        {
            if (level < valuePairs[0].Key)
                return valuePairs[0].Value;

            if (level >= valuePairs[^1].Key)
                return valuePairs[^1].Value;

            for (int i = 1; i < valuePairs.Length; i++)
            {
                if (level >= valuePairs[i - 1].Key && level < valuePairs[i].Key)
                {
                    return Mathf.Lerp(getValueAtIndex(i - 1), getValueAtIndex(i),
                                    Mathf.InverseLerp(valuePairs[i - 1].Key, valuePairs[i].Key, level));
                }
            }

            return DefaultValue;
        }

        private float getValueAtIndex(int index)
        {
            return varValueList[index]?.LastEvalValue ?? valuePairs[index].Value;
        }

        private bool parseLevelPair(string varStr, Dictionary<string, VariableInfo> children)
        {
            bool isValid = false;
            var str = varStr.Substring(varStr.IndexOf('(') + 1,
                varStr.IndexOf(')') - (varStr.IndexOf('(') + 1));
            var split = str.Split('=');
            if (split.Length == 2)
            {
                var levelList = split[0].Split(',');
                var valueList = split[1].Split(',');
                if (levelList.Length == valueList.Length && levelList.Length > 1)
                {
                    isValid = true;
                    valuePairs = new LevelPair[levelList.Length];
                    varValueList = new VariableInfo[levelList.Length];
                    for (int i = 0; i < levelList.Length; i++)
                    {
                        if (!float.TryParse(levelList[i], out var level))
                        {
                            isValid = false;
                            break;
                        }

                        if (!float.TryParse(valueList[i], out var value) && 
                            (children == null || !children.TryGetValue(valueList[i], out varValueList[i])))
                        {
                            isValid = false;
                            break;
                        }
                        
                        valuePairs[i] = new LevelPair(level, value);
                    }
                }
            }

            return isValid;
        }
    }

    public class ProgressionLevelInfo : LevelInfoAbs
    {
        private string perkName;
        
        private ProgressionValue pvCache;

        protected override bool IsValidImpl(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            pvCache = getTarget(_params, _target);
            return pvCache != null;
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            if (!base.TryParse(parsedStr, children))
                return false;

            perkName = parsedStr[1..parsedStr.IndexOf('(')];
            return true;
        }

        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex,
            int _targetCount)
        {
            var pv = pvCache ?? getTarget(_params, _target);
            pvCache = null;
            return pv == null ? DefaultValue : getValueByLevel(pv.Level);
        }

        private ProgressionValue getTarget(MinEventParams _params, EntityAlive _target)
        {
            EntityAlive target = getEntityTarget(_params, _target);
            return target ? target.Progression?.GetProgressionValue(perkName) : null;
        }
    }
    
    public class ItemTierInfo : LevelInfoAbs
    {
        private bool useHoldingItem;

        private ItemValue ivCache;

        protected override bool IsValidImpl(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            ivCache = getItemValue(_params, _target);
            return ivCache?.IsEmpty() == false && ivCache.HasQuality;
        }

        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex,
            int _targetCount)
        {
            var iv = ivCache ?? getItemValue(_params, _target);
            ivCache = null;
            return iv?.IsEmpty() == false && iv.HasQuality ? getValueByLevel(iv.Quality) : DefaultValue;
        }

        private ItemValue getItemValue(MinEventParams _params, EntityAlive _target)
        {
            if (!useHoldingItem)
                return _params.ItemValue;
            var entity = getEntityTarget(_params, _target);
            var holdingItemValue = entity ? entity.inventory?.holdingItemItemValue : null;
            return holdingItemValue;
        }

        public override bool ParseAdditionalAttributes(XAttribute attr)
        {
            if (base.ParseAdditionalAttributes(attr))
                return true;
            
            switch (attr.Name.LocalName)
            {
                case "use_holding_item":
                    useHoldingItem = bool.Parse(attr.Value);
                    return true;
            }

            return false;
        }
    }

    public class StatInfo : VariableInfo
    {
        private enum ValueType
        {
            Current,
            CurrentPerc,
            Max,
            MaxModifier,
            ModifiedMax,
            ModifiedMaxPerc
        }
        
        private Stat.StatTypes statType;
        private ValueType valueType;

        public override bool IsUnique => true;
        
        private Stat statCache;

        protected override bool IsValidImpl(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            statCache = getTarget(_params, _target);
            return statCache != null;
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            var statInfo = parsedStr.Substring(parsedStr.IndexOf('(') + 1,
                parsedStr.IndexOf(')') - (parsedStr.IndexOf('(') + 1));
            var stat = statInfo.Split(',');
            return stat.Length == 2 && EnumUtils.TryParse(stat[0], out statType, true) && EnumUtils.TryParse(stat[1], out valueType, true);
        }

        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex,
            int _targetCount)
        {
            var stat = statCache ?? getTarget(_params, _target);
            statCache = null;
            if (stat == null)
                return DefaultValue;

            return valueType switch
            {
                ValueType.Current => stat.Value,
                ValueType.CurrentPerc => stat.ValuePercent,
                ValueType.Max => stat.Max,
                ValueType.MaxModifier => stat.MaxModifier,
                ValueType.ModifiedMax => stat.ModifiedMax,
                ValueType.ModifiedMaxPerc => stat.ModifiedMaxPercent,
                _ => DefaultValue
            };
        }

        private Stat getTarget(MinEventParams _params, EntityAlive _target)
        {
            var target = getEntityTarget(_params, _target);
            if (!target || target.Stats == null)
                return null;
            return statType switch
            {
                Stat.StatTypes.Health => target.Stats.Health,
                Stat.StatTypes.Stamina => target.Stats.Stamina,
                Stat.StatTypes.Food => target.Stats.Food,
                Stat.StatTypes.Water => target.Stats.Water,
                _ => null
            };
        }
    }
    
    public class PassiveInfo : VariableInfo
    {
        private PassiveEffects passive;
        private FastTags<TagGroup.Global> tags;
        private bool useHoldingItem = true;
        private bool useEquipment = true;
        private bool useProgression = true;
        private bool useBuffs = true;
        private bool useChallenges = true;
        private bool useMods = true;
        private bool useDurability;
        private bool appendTags;

        public override bool IsUnique => true;

        private EntityAlive targetCache;

        protected override bool IsValidImpl(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            targetCache = getEntityTarget(_params, _target);
            return targetCache;
        }

        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex,
            int _targetCount)
        {
            var target = targetCache ? getEntityTarget(_params, _target) : null;
            targetCache = null;
            if (!target)
                return DefaultValue;
            
            var iv = _params.ItemValue;
            if (useHoldingItem)
            {
                iv = target.inventory?.holdingItemItemValue;
            }

            FastTags<TagGroup.Global> tagsFinal;
            if (tags.IsEmpty)
            {
                tagsFinal = iv?.ItemClass?.ItemTags ?? tags;
            }
            else if (appendTags)
            {
                tagsFinal = iv?.ItemClass?.ItemTags | tags ?? tags;
            }
            else
            {
                tagsFinal = tags;
            }
            return EffectManager.GetValue(passive, iv, DefaultValue, target, null,
                tagsFinal, useEquipment, false,
                useProgression, useBuffs, useChallenges, 1, useMods, useDurability);
        }

        public override bool ParseAdditionalAttributes(XAttribute attr)
        {
            if (base.ParseAdditionalAttributes(attr))
                return true;

            switch (attr.Name.LocalName)
            {
                case "use_holding_item":
                    useHoldingItem = bool.Parse(attr.Value);
                    return true;
                case "use_equipment":
                    useEquipment = bool.Parse(attr.Value);
                    return true;
                case "use_progression":
                    useProgression = bool.Parse(attr.Value);
                    return true;
                case "use_buffs":
                    useBuffs = bool.Parse(attr.Value);
                    return true;
                case "use_challenges":
                    useChallenges = bool.Parse(attr.Value);
                    return true;
                case "use_mods":
                    useMods = bool.Parse(attr.Value);
                    return true;
                case "use_durability":
                    useDurability = bool.Parse(attr.Value);
                    return true;
                case "tags":
                    string tagStr = attr.Value;
                    appendTags = tagStr.StartsWith("+");
                    if (appendTags)
                    {
                        tagStr = tagStr[1..];
                    }
                    tags = FastTags<TagGroup.Global>.Parse(tagStr);
                    return true;
            }
            return false;
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            tags = FastTags<TagGroup.Global>.none;
            var passiveStr = parsedStr[1..];
            if (passiveStr.Contains('(') && passiveStr.Contains(')'))
            {
                var tagStr = passiveStr.Substring(passiveStr.IndexOf('(') + 1,
                    passiveStr.IndexOf(')') - (passiveStr.IndexOf('(') + 1));
                if (!string.IsNullOrEmpty(tagStr))
                {
                    appendTags = tagStr.StartsWith("+");
                    if (appendTags)
                    {
                        tagStr = tagStr[1..];
                    }
                    tags = FastTags<TagGroup.Global>.Parse(tagStr);
                }
                passiveStr = passiveStr[..passiveStr.IndexOf('(')];
            }

            try
            {
                passive = CustomEffectEnumManager.GetEnumOrThrow<PassiveEffects>(passiveStr, true);
                return true;
            }
            catch (Exception)
            {
                Log.Error($"Failed to parse passive: {passiveStr}");
                return false;
            }
        }
    }
    
    public class ExpressionInfo : VariableInfo
    {
        public override bool IsUnique => true;
        
        private Expression<float> expression;

        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex,
            int _targetCount)
        {
            return expression.Invoke();
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            parsedStr = parsedStr[5..];
            var context = new ExpressionContext<float>();
            while (true)
            {
                int startIndex = parsedStr.IndexOf('[');
                if (startIndex == -1)
                    break;
                int endIndex = parsedStr.IndexOf(']', startIndex);
                if (endIndex == -1)
                    return false;
                var name = parsedStr.Substring(startIndex, endIndex - startIndex + 1);
                if (children == null || !children.TryGetValue(name, out var child))
                    return false;
                var varName = $"x{child.VarIndex}";
                parsedStr = parsedStr.Remove(startIndex, endIndex - startIndex + 1)
                    .Insert(startIndex, varName);
                context.RegisterVariable(varName, () => child.LastEvalValue);
            }

            expression = FloatExpressionParser.Instance.Compile(parsedStr, context, true);
            return true;
        }
    }

    public class TargetIndexInfo : VariableInfo
    {
        public override bool IsUnique => true;
        
        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex, int _targetCount)
        {
            return _targetIndex;
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            return true;
        }
    }

    public class TargetCountInfo : VariableInfo
    {
        public override bool IsUnique => true;

        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex, int _targetCount)
        {
            return _targetCount;
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            return true;
        }
    }

    public class DistanceInfo : VariableInfo
    {
        public override bool IsUnique => true;
        
        protected override bool IsValidImpl(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            return _params.Self != null;
        }
        
        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex, int _targetCount)
        {
            return _params.Self != null ? Vector3.Distance(_params.Self.position, _target.position) : DefaultValue;
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            return true;
        }
    }

    public class HPDamageInfo : VariableInfo
    {
        public override bool IsUnique => true;

        protected override bool IsValidImpl(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            return _params.DamageResponse.Source != null;
        }

        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex, int _targetCount)
        {
            return _params.DamageResponse.Source != null ? _params.DamageResponse.Strength : DefaultValue;
        }

        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            return true;
        }
    }
    
    public class ArmorDamageInfo : VariableInfo
    {
        public override bool IsUnique => true;
        
        protected override bool IsValidImpl(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            return _params.DamageResponse.Source != null;
        }
        
        protected override float DoEvaluate(MinEventParams _params, EntityAlive _target, int _targetIndex, int _targetCount)
        {
            return _params.DamageResponse.Source != null ? _params.DamageResponse.ArmorDamage : DefaultValue;
        }
        
        protected override bool TryParse(string parsedStr, Dictionary<string, VariableInfo> children)
        {
            return true;
        }
    }
}