using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Singletons;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using UniLinq;
using System.Text;
using System.Threading.Tasks;
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
        rechargeDatas = rechargeData.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        rechargeTags = rechargeDatas.Select(s => FastTags.Parse(s) | __instance.item.ItemTags).ToArray();
    }

    public class MetaRechargerData : IBackgroundInventoryUpdater
    {
        private ActionModuleMetaRecharger module;
        private float lastUpdateTime;
        private ItemActionData actionData;
        public MetaRechargerData(ItemInventoryData _invData, int _indexOfAction, ActionModuleMetaRecharger _rechargeModule)
        {
            module = _rechargeModule;
            actionData = _invData.actionData[_indexOfAction];
            lastUpdateTime = Time.time;
            if (_rechargeModule.rechargeDatas == null)
                return;
            foreach (var rechargeData in _rechargeModule.rechargeDatas)
            {
                if (!_invData.itemValue.HasMetadata(rechargeData))
                {
                    _invData.itemValue.SetMetadata(rechargeData, 0, TypedMetadataValue.TypeTag.Float);
                }
            }

            BackgroundInventoryUpdateManager.RegisterUpdater(_invData.holdingEntity, _invData.slotIdx, this);
        }

        public void OnUpdate(ItemInventoryData invData)
        {
            ItemValue itemValue = invData.itemValue;
            invData.holdingEntity.MinEventContext.ItemInventoryData = invData;
            invData.holdingEntity.MinEventContext.ItemValue = itemValue;
            invData.holdingEntity.MinEventContext.ItemActionData = actionData;
            float curTime = Time.time;
            for (int i = 0; i < module.rechargeDatas.Length; i++)
            {
                string rechargeData = module.rechargeDatas[i];
                FastTags rechargeTag = module.rechargeTags[i];
                float updateInterval = EffectManager.GetValue(CustomEnums.RechargeDataInterval, itemValue, 0, invData.holdingEntity, null, rechargeTag);
                if (curTime - lastUpdateTime > updateInterval)
                {
                    lastUpdateTime = curTime;
                    float cur = (float)itemValue.GetMetadata(rechargeData);
                    float max = EffectManager.GetValue(CustomEnums.RechargeDataMaximum, itemValue, 0, invData.holdingEntity, null, rechargeTag);
                    if (cur > max)
                    {
                        //the result updated here won't exceed max so it's set somewhere else, decrease slowly
                        float dec = EffectManager.GetValue(CustomEnums.RechargeDataDecrease, itemValue, float.MaxValue, invData.holdingEntity, null, rechargeTag);
                        cur = Mathf.Max(cur - dec, max);
                    }
                    else if (cur < max)
                    {
                        //add up and clamp to max
                        float add = EffectManager.GetValue(CustomEnums.RechargeDataValue, itemValue, 0, invData.holdingEntity, null, rechargeTag);
                        cur = Mathf.Min(cur + add, max);
                    }
                    itemValue.SetMetadata(rechargeData, cur, TypedMetadataValue.TypeTag.Float);
                }
            }
        }
    }
}
