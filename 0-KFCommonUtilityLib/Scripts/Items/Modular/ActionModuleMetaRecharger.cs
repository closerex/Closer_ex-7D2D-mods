using HarmonyLib;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged)), ActionDataTarget(typeof(MetaRechargerData))]
public class ActionModuleMetaRecharger
{
    public struct RechargeTags
    {
        public FastTags<TagGroup.Global> tagsOriginal;
        public FastTags<TagGroup.Global> tagsInterval;
        public FastTags<TagGroup.Global> tagsMaximum;
        public FastTags<TagGroup.Global> tagsValue;
        public FastTags<TagGroup.Global> tagsDecrease;
        public FastTags<TagGroup.Global> tagsDecreaseInterval;
    }

    public string[] rechargeDatas;
    public RechargeTags[] rechargeTags;
    private static readonly FastTags<TagGroup.Global> TagsInterval = FastTags<TagGroup.Global>.Parse("RechargeDataInterval");
    private static readonly FastTags<TagGroup.Global> TagsMaximum = FastTags<TagGroup.Global>.Parse("RechargeDataMaximum");
    private static readonly FastTags<TagGroup.Global> TagsValue = FastTags<TagGroup.Global>.Parse("RechargeDataValue");
    private static readonly FastTags<TagGroup.Global> TagsDecrease = FastTags<TagGroup.Global>.Parse("RechargeDataDecrease");
    private static readonly FastTags<TagGroup.Global> TagsDecreaseInterval = FastTags<TagGroup.Global>.Parse("RechargeDecreaseInterval");

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
                tagsMaximum = _tags | TagsMaximum,
                tagsValue = _tags | TagsValue,
                tagsDecrease = _tags | TagsDecrease,
                tagsDecreaseInterval = _tags | TagsDecreaseInterval,
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
        return true;
    }

    public class MetaRechargerData : IBackgroundInventoryUpdater
    {
        private ActionModuleMetaRecharger module;
        private float lastUpdateTime, lastDecreaseTime;
        private int indexOfAction;

        public int Index => indexOfAction;

        public MetaRechargerData(ItemInventoryData _invData, int _indexOfAction, ActionModuleMetaRecharger _rechargeModule)
        {
            module = _rechargeModule;
            indexOfAction = _indexOfAction;
            lastUpdateTime = lastDecreaseTime = Time.time;
            if (_rechargeModule.rechargeDatas == null)
                return;

            BackgroundInventoryUpdateManager.RegisterUpdater(_invData.holdingEntity, _invData.slotIdx, this);
        }

        public bool OnUpdate(ItemInventoryData invData)
        {
            ItemValue itemValue = invData.itemValue;
            EntityAlive holdingEntity = invData.holdingEntity;
            holdingEntity.MinEventContext.ItemInventoryData = invData;
            holdingEntity.MinEventContext.ItemValue = itemValue;
            holdingEntity.MinEventContext.ItemActionData = invData.actionData[indexOfAction];
            float curTime = Time.time;
            bool res = false;
            for (int i = 0; i < module.rechargeDatas.Length; i++)
            {
                string rechargeData = module.rechargeDatas[i];
                RechargeTags rechargeTag = module.rechargeTags[i];
                float updateInterval = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, itemValue, float.MaxValue, holdingEntity, null, rechargeTag.tagsInterval);
                float decreaseInterval = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, itemValue, float.MaxValue, holdingEntity, null, rechargeTag.tagsDecreaseInterval);
                float deltaTime = curTime - lastUpdateTime;
                float deltaDecreaseTime = curTime - lastDecreaseTime;
                if (deltaTime > updateInterval || deltaDecreaseTime > decreaseInterval)
                {
                    //Log.Out($"last update time {lastUpdateTime} cur time {curTime} update interval {updateInterval}");
                    float cur;
                    if (!itemValue.HasMetadata(rechargeData))
                    {
                        itemValue.SetMetadata(rechargeData, 0, TypedMetadataValue.TypeTag.Float);
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
                            cur = Mathf.Max(cur - dec, max);
                            lastDecreaseTime = curTime;
                            modified = true;
                        }
                        lastUpdateTime = curTime;
                    }
                    else
                    {
                        if (cur < max && deltaTime > updateInterval)
                        {
                            //add up and clamp to max
                            float add = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, itemValue, 0, holdingEntity, null, rechargeTag.tagsValue);
                            cur = Mathf.Min(cur + add, max);
                            lastUpdateTime = curTime;
                            modified = true;
                        }
                        //always set lastDecreaseTime if not overcharged, since we don't want overcharged data to decrease right after it's charged
                        lastDecreaseTime = curTime;
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
