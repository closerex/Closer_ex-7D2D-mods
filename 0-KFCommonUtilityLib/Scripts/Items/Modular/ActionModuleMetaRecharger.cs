using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged), typeof(MetaRechargerData))]
public class ActionModuleMetaRecharger
{
    public string[] rechargeDatas;
    public FastTags[] rechargeTags;

    [MethodTargetPostfix(nameof(ItemActionRanged.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props, ItemAction __instance)
    {
        rechargeDatas = null;
        rechargeTags = null;
        string rechargeData = string.Empty;
        _props.Values.TryGetString("RechargeData", out rechargeData);
        if (string.IsNullOrEmpty(rechargeData))
        {
            Log.Error($"No recharge data found on item {__instance.item.Name} action {__instance.ActionIndex}");
            return;
        }
        rechargeDatas = rechargeData.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        rechargeTags = rechargeDatas.Select(s => FastTags.Parse(s) | __instance.item.ItemTags).ToArray();
    }

    [MethodTargetPrefix(nameof(ItemActionAttack.StartHolding))]
    private bool Prefix_StartHolding(ItemActionData _data, MetaRechargerData __customData)
    {
        EntityAlive holdingEntity = _data.invData.holdingEntity;
        if (holdingEntity.isEntityRemote)
            return true;
        for (int i = 0; i < rechargeDatas.Length; i++)
        {
            holdingEntity.MinEventContext.Tags = rechargeTags[i];
            holdingEntity.FireEvent(CustomEnums.onRechargeValueUpdate, true);
        }
        return true;
    }

    public class MetaRechargerData : IBackgroundInventoryUpdater
    {
        private ActionModuleMetaRecharger module;
        private float lastUpdateTime;
        private int indexOfAction;

        public int Index => indexOfAction;

        public MetaRechargerData(ItemInventoryData _invData, int _indexOfAction, ActionModuleMetaRecharger _rechargeModule)
        {
            module = _rechargeModule;
            indexOfAction = _indexOfAction;
            lastUpdateTime = Time.time;
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
                FastTags rechargeTag = module.rechargeTags[i];
                float updateInterval = EffectManager.GetValue(CustomEnums.RechargeDataInterval, itemValue, float.MaxValue, holdingEntity, null, rechargeTag);
                if (curTime - lastUpdateTime > updateInterval)
                {
                    //Log.Out($"last update time {lastUpdateTime} cur time {curTime} update interval {updateInterval}");
                    lastUpdateTime = curTime;
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
                    float max = EffectManager.GetValue(CustomEnums.RechargeDataMaximum, itemValue, 0, holdingEntity, null, rechargeTag);
                    if (cur > max)
                    {
                        //the result updated here won't exceed max so it's set somewhere else, decrease slowly
                        float dec = EffectManager.GetValue(CustomEnums.RechargeDataDecrease, itemValue, float.MaxValue, holdingEntity, null, rechargeTag);
                        cur = Mathf.Max(cur - dec, max);
                    }
                    else if (cur < max)
                    {
                        //add up and clamp to max
                        float add = EffectManager.GetValue(CustomEnums.RechargeDataValue, itemValue, 0, holdingEntity, null, rechargeTag);
                        cur = Mathf.Min(cur + add, max);
                    }
                    itemValue.SetMetadata(rechargeData, cur, TypedMetadataValue.TypeTag.Float);
                    if (invData.slotIdx == holdingEntity.inventory.holdingItemIdx)
                    {
                        holdingEntity.MinEventContext.Tags = rechargeTag;
                        itemValue.FireEvent(CustomEnums.onRechargeValueUpdate, holdingEntity.MinEventContext);
                        //Log.Out($"action index is {holdingEntity.MinEventContext.ItemActionData.indexInEntityOfAction} after firing event");
                    }
                }
            }
        }
    }
}
