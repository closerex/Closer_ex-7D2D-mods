using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

public class RandomRollPassive : TargetedCompareRequirementBase
{
    private enum SeedType
    {
        Item,
        Player,
        Random
    }

    private Vector2 minMax;

    private SeedType seedType;

    private int seedAdditive;

    private GameRandom rand;

    private bool usePassive = false;
    private PassiveEffects passive;
    FastTags tags = FastTags.none;
    bool useSelf = true;
    bool useHoldingItem = false;

    public override bool IsValid(MinEventParams _params)
    {
        if (!ParamsValid(_params))
        {
            return false;
        }

        if (usePassive)
        {
            value = EffectManager.GetValue(passive, useHoldingItem ? ((useSelf ? _params.Self.inventory?.holdingItemItemValue : target.inventory?.holdingItemItemValue) ?? _params.ItemValue) : _params.ItemValue, 0, useSelf ? _params.Self : target, null, tags, true, useHoldingItem);
        }
        else if (useCVar)
        {
            value = target.Buffs.GetCustomVar(refCvarName);
        }

        if (seedType == SeedType.Item)
        {
            rand = GameRandomManager.Instance.CreateGameRandom(_params.Seed);
        }
        else if (seedType == SeedType.Player)
        {
            rand = GameRandomManager.Instance.CreateGameRandom(_params.Self.entityId);
        }
        else
        {
            rand = GameRandomManager.Instance.CreateGameRandom(Environment.TickCount);
        }

        float randomFloat = rand.RandomFloat;
        GameRandomManager.Instance.FreeGameRandom(rand);
        if (invert)
        {
            return !RequirementBase.compareValues(Mathf.Lerp(minMax.x, minMax.y, randomFloat), operation, value);
        }

        return RequirementBase.compareValues(Mathf.Lerp(minMax.x, minMax.y, randomFloat), operation, value);
    }

    public override void GetInfoStrings(ref List<string> list)
    {
        list.Add($"roll[{minMax.x.ToCultureInvariantString()}-{minMax.y.ToCultureInvariantString()}] {operation.ToStringCached()} {value.ToCultureInvariantString()}");
    }

    public override bool ParseXAttribute(XAttribute _attribute)
    {
        bool flag = base.ParseXAttribute(_attribute);
        if (!flag)
        {
            switch (_attribute.Name.LocalName)
            {
                case "min_max":
                    minMax = StringParsers.ParseVector2(_attribute.Value);
                    return true;
                case "seed_type":
                    seedType = EnumUtils.Parse<SeedType>(_attribute.Value, _ignoreCase: true);
                    return true;
                case "seed_additive":
                    seedAdditive = StringParsers.ParseSInt32(_attribute.Value);
                    return true;
                case "passive":
                    usePassive = true;
                    passive = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>(_attribute.Value);
                    return true;
                case "tags":
                    tags = FastTags.Parse(_attribute.Value);
                    return true;
                case "use_holding_item":
                    useHoldingItem = StringParsers.ParseBool(_attribute.Value);
                    return true;
                case "use_self":
                    useSelf = StringParsers.ParseBool(_attribute.Value);
                    return true;
            }
        }

        return flag;
    }
}
