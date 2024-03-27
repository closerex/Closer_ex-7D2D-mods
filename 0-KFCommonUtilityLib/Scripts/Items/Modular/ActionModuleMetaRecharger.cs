using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged), typeof(MetaRechargerData))]
public class ActionModuleMetaRecharger
{
    public struct RechargeTags
    {
        public FastTags tagsOriginal;
        public FastTags tagsInterval;
        public FastTags tagsMaximum;
        public FastTags tagsValue;
        public FastTags tagsDecrease;
        public FastTags tagsDecreaseInterval;
    }

    public string[] rechargeDatas;
    public RechargeTags[] rechargeTags;
    private static FastTags TagsInterval = FastTags.Parse("RechargeDataInterval");
    private static FastTags TagsMaximum = FastTags.Parse("RechargeDataMaximum");
    private static FastTags TagsValue = FastTags.Parse("RechargeDataValue");
    private static FastTags TagsDecrease = FastTags.Parse("RechargeDataDecrease");
    private static FastTags TagsDecreaseInterval = FastTags.Parse("RechargeDecreaseInterval");

    [MethodTargetPostfix(nameof(ItemActionRanged.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props, ItemAction __instance)
    {
        rechargeDatas = null;
        rechargeTags = null;
        string rechargeData = string.Empty;
        _props.Values.TryGetString("RechargeData", out rechargeData);
        _props.Values.TryGetString("RechargeTags", out string tags);
        FastTags commonTags = string.IsNullOrEmpty(tags) ? FastTags.none : FastTags.Parse(tags);
        if (string.IsNullOrEmpty(rechargeData))
        {
            Log.Error($"No recharge data found on item {__instance.item.Name} action {__instance.ActionIndex}");
            return;
        }
        rechargeDatas = rechargeData.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        rechargeTags = rechargeDatas.Select(s =>
        {
            var _tags = FastTags.Parse(s) | commonTags;
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

    [MethodTargetPrefix(nameof(ItemActionAttack.StartHolding))]
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

        public void OnUpdate(ItemInventoryData invData)
        {
            ItemValue itemValue = invData.itemValue;
            EntityAlive holdingEntity = invData.holdingEntity;
            holdingEntity.MinEventContext.ItemInventoryData = invData;
            holdingEntity.MinEventContext.ItemValue = itemValue;
            holdingEntity.MinEventContext.ItemActionData = invData.actionData[indexOfAction];
            float curTime = Time.time;
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
                    if (invData.slotIdx == holdingEntity.inventory.holdingItemIdx)
                    {
                        holdingEntity.MinEventContext.Tags = rechargeTag.tagsOriginal;
                        itemValue.FireEvent(CustomEnums.onRechargeValueUpdate, holdingEntity.MinEventContext);
                        //Log.Out($"action index is {holdingEntity.MinEventContext.ItemActionData.indexInEntityOfAction} after firing event");
                    }
                }
            }
        }
    }
}
