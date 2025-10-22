using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(MetaRechargerData))]
public class ActionModuleMetaRecharger
{
    public struct RechargeTags
    {
        public FastTags<TagGroup.Global> tagsOriginal;
        public FastTags<TagGroup.Global> tagsInterval;
        public FastTags<TagGroup.Global> tagsIntervalMultiplier;
        public FastTags<TagGroup.Global> tagsMaximum;
        public FastTags<TagGroup.Global> tagsValue;
        public FastTags<TagGroup.Global> tagsDecrease;
        public FastTags<TagGroup.Global> tagsDecreaseInterval;
        public FastTags<TagGroup.Global> tagsDecreaseIntervalMultiplier;
    }

    public string[] rechargeDatas;
    public RechargeTags[] rechargeTags;
    private static readonly FastTags<TagGroup.Global> TagsInterval = FastTags<TagGroup.Global>.Parse("RechargeDataInterval");
    private static readonly FastTags<TagGroup.Global> TagsIntervalMultiplier = FastTags<TagGroup.Global>.Parse("RechargeDataIntervalMultiplier");
    private static readonly FastTags<TagGroup.Global> TagsMaximum = FastTags<TagGroup.Global>.Parse("RechargeDataMaximum");
    private static readonly FastTags<TagGroup.Global> TagsValue = FastTags<TagGroup.Global>.Parse("RechargeDataValue");
    private static readonly FastTags<TagGroup.Global> TagsDecrease = FastTags<TagGroup.Global>.Parse("RechargeDataDecrease");
    private static readonly FastTags<TagGroup.Global> TagsDecreaseInterval = FastTags<TagGroup.Global>.Parse("RechargeDecreaseInterval");
    private static readonly FastTags<TagGroup.Global> TagsDecreaseIntervalMultiplier = FastTags<TagGroup.Global>.Parse("RechargeDataDecreaseIntervalMultiplier");

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    private void Postfix_ReadFrom(DynamicProperties _props, ItemAction __instance)
    {
        rechargeDatas = null;
        rechargeTags = null;
        string rechargeData = string.Empty;
        _props.Values.TryGetValue("RechargeData", out rechargeData);
        _props.Values.TryGetValue("RechargeTags", out string tags);
        FastTags<TagGroup.Global> commonTags = string.IsNullOrEmpty(tags) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(tags);
        if (string.IsNullOrEmpty(rechargeData))
        {
            Log.Error($"No recharge data found on item {__instance.item.Name} action {__instance.ActionIndex}");
            return;
        }
        rechargeDatas = rechargeData.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        rechargeTags = rechargeDatas.Select(s =>
        {
            var _tags = FastTags<TagGroup.Global>.Parse(s) | commonTags;
            return new RechargeTags
            {
                tagsOriginal = _tags,
                tagsInterval = _tags | TagsInterval,
                tagsIntervalMultiplier = _tags | TagsIntervalMultiplier,
                tagsMaximum = _tags | TagsMaximum,
                tagsValue = _tags | TagsValue,
                tagsDecrease = _tags | TagsDecrease,
                tagsDecreaseInterval = _tags | TagsDecreaseInterval,
                tagsDecreaseIntervalMultiplier = _tags | TagsDecreaseIntervalMultiplier,
            };
        }).ToArray();
    }

    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPrefix]
    private bool Prefix_StartHolding(ItemActionData _data, MetaRechargerData __customData)
    {
        EntityAlive holdingEntity = _data.invData.holdingEntity;
        if (holdingEntity.isEntityRemote)
            return true;
        for (int i = 0; i < rechargeDatas.Length; i++)
        {
            holdingEntity.MinEventContext.Tags = rechargeTags[i].tagsOriginal;
            holdingEntity.FireEvent(CustomEnums.onRechargeValueUpdate, true);
        }
        UpdateBasicInterval(_data, __customData);
        return true;
    }

    public void UpdateBasicInterval(ItemActionData _data, MetaRechargerData __customData)
    {
        for (int i = 0; i < rechargeDatas.Length; i++)
        {
            RechargeTags rechargeTag = rechargeTags[i];
            float value = 1, perc = 1;
            _data.invData.item.Effects.ModifyValue(_data.invData.holdingEntity, CustomEnums.CustomTaggedEffect, ref value, ref perc, _data.invData.itemValue.Quality, rechargeTag.tagsInterval);
            __customData.basicRechargeInterval[i] = Mathf.Max(0.05f, value * perc);
            value = perc = 1;
            _data.invData.item.Effects.ModifyValue(_data.invData.holdingEntity, CustomEnums.CustomTaggedEffect, ref value, ref perc, _data.invData.itemValue.Quality, rechargeTag.tagsDecreaseInterval);
            __customData.basicDecreaseInterval[i] = Mathf.Max(0.05f, value * perc);
            //Log.Out($"updating {rechargeDatas[i]} charge interval to {__customData.basicRechargeInterval[i]}, decrease interval to {__customData.basicDecreaseInterval[i]}");
        }
        __customData.basicIntervalSet = true;
    }

    public class MetaRechargerData : IBackgroundInventoryUpdater
    {
        private ActionModuleMetaRecharger module;
        public bool basicIntervalSet = false;
        public float[] basicRechargeInterval, basicDecreaseInterval, lastUpdateTime, lastDecreaseTime;
        private int indexOfAction;

        public int Index => indexOfAction;

        public MetaRechargerData(ItemInventoryData _inventoryData, int _indexInEntityOfAction, ActionModuleMetaRecharger __customModule)
        {
            module = __customModule;
            indexOfAction = _indexInEntityOfAction;
            basicRechargeInterval = new float[__customModule.rechargeDatas.Length];
            basicDecreaseInterval = new float[__customModule.rechargeDatas.Length];
            float curTime = Time.time;
            lastUpdateTime = new float[__customModule.rechargeDatas.Length];
            lastUpdateTime.Fill(curTime);
            lastDecreaseTime = new float[__customModule.rechargeDatas.Length];
            lastDecreaseTime.Fill(curTime);
            if (__customModule.rechargeDatas == null)
                return;

            BackgroundInventoryUpdateManager.RegisterUpdater(_inventoryData.holdingEntity, _inventoryData.slotIdx, this);
        }

        public bool OnUpdate(ItemInventoryData invData)
        {
            ItemValue itemValue = invData.itemValue;
            EntityAlive holdingEntity = invData.holdingEntity;
            holdingEntity.MinEventContext.ItemInventoryData = invData;
            holdingEntity.MinEventContext.ItemValue = itemValue;
            holdingEntity.MinEventContext.ItemActionData = invData.actionData[indexOfAction];
            if (!basicIntervalSet)
            {
                module.UpdateBasicInterval(invData.actionData[indexOfAction], this);
            }
            float curTime = Time.time;
            bool res = false;
            for (int i = 0; i < module.rechargeDatas.Length; i++)
            {
                string rechargeData = module.rechargeDatas[i];
                RechargeTags rechargeTag = module.rechargeTags[i];
                float updateIntervalMultiplier = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, itemValue, 0, holdingEntity, null, rechargeTag.tagsIntervalMultiplier);
                updateIntervalMultiplier = updateIntervalMultiplier >= 0 ? (1 / (1 + updateIntervalMultiplier)) : (1 - updateIntervalMultiplier);
                float updateInterval = basicRechargeInterval[i] * updateIntervalMultiplier;
                float decreaseIntervalMultiplier = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, itemValue, 0, holdingEntity, null, rechargeTag.tagsDecreaseIntervalMultiplier);
                decreaseIntervalMultiplier = decreaseIntervalMultiplier >= 0 ? (1 / (1 + decreaseIntervalMultiplier)) : (1 - decreaseIntervalMultiplier);
                float decreaseInterval = basicDecreaseInterval[i] * decreaseIntervalMultiplier;
                float deltaRechargeTime = curTime - lastUpdateTime[i];
                float deltaDecreaseTime = curTime - lastDecreaseTime[i];
                if (deltaRechargeTime > updateInterval || deltaDecreaseTime > decreaseInterval)
                {
                    //Log.Out($"data {module.rechargeDatas[i]} delta recharge time {deltaRechargeTime} delta decrease time {deltaDecreaseTime}");
                    float cur;
                    if (!itemValue.HasMetadata(rechargeData))
                    {
                        itemValue.SetMetadata(rechargeData, 0f, TypedMetadataValue.TypeTag.Float);
                        cur = 0;
                    }
                    else
                    {
                        cur = (float)itemValue.GetMetadata(rechargeData);
                    }
                    float max = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, itemValue, 0, holdingEntity, null, rechargeTag.tagsMaximum);
                    bool modified = false;
                    if (cur > max)
                    {
                        if (deltaDecreaseTime > decreaseInterval)
                        {
                            //the result updated here won't exceed max so it's set somewhere else, decrease slowly
                            float dec = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, itemValue, float.MaxValue, holdingEntity, null, rechargeTag.tagsDecrease);
                            cur = Mathf.Max(cur - dec * deltaDecreaseTime / decreaseInterval, max);
                            lastDecreaseTime[i] = curTime;
                            modified = true;
                        }
                        lastUpdateTime[i] = curTime;
                    }
                    else
                    {
                        if (deltaRechargeTime > updateInterval)
                        {
                            if (cur < max)
                            {
                                //add up and clamp to max
                                float add = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, itemValue, 0, holdingEntity, null, rechargeTag.tagsValue);
                                cur = Mathf.Min(cur + add * deltaRechargeTime / updateInterval, max);
                                modified = true;
                            }
                            lastUpdateTime[i] = curTime;
                        }
                        //always set lastDecreaseTime if not overcharged, since we don't want overcharged data to decrease right after it's charged
                        lastDecreaseTime[i] = curTime;
                    }

                    if (modified)
                    {
                        itemValue.SetMetadata(rechargeData, cur, TypedMetadataValue.TypeTag.Float);
                    }
                    if (invData.slotIdx == holdingEntity.inventory.holdingItemIdx && invData.slotIdx >= 0)
                    {
                        holdingEntity.MinEventContext.Tags = rechargeTag.tagsOriginal;
                        itemValue.FireEvent(CustomEnums.onRechargeValueUpdate, holdingEntity.MinEventContext);
                        //Log.Out($"action index is {holdingEntity.MinEventContext.ItemActionData.indexInEntityOfAction} after firing event");
                    }
                    res |= modified;
                }
            }
            return res;
        }
    }
}
