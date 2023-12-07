using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Singletons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged), typeof(RechargeData))]
public class ActionModuleRecharge
{

    [MethodTargetPostfix(nameof(ItemActionRanged.OnModificationsChanged))]
    private void Postfix_OnModificationChanged(RechargeData __customData, ItemActionRanged __instance, ItemActionData _data)
    {
        __customData.UpdateRechargeTarget(__instance.Properties, _data.invData.itemValue);
    }

    public class RechargeData : IBackgroundInventoryUpdater
    {
        public ActionModuleRecharge module;
        private float lastUpdateTime;
        private string rechargeData;
        private FastTags tags;
        public RechargeData(ItemInventoryData _invData, int _indexOfAction, ActionModuleRecharge _rechargeModule)
        {
            module = _rechargeModule;
            lastUpdateTime = Time.time;
            tags = _invData.item.ItemTags | ActionIndexToTag(_indexOfAction);
            BackgroundInventoryUpdateManager.RegisterUpdater(_invData.holdingEntity, _invData.slotIdx, this);
        }

        public void UpdateRechargeTarget(DynamicProperties _properties, ItemValue _itemValue)
        {
            rechargeData = string.Empty;
            _properties.Values.TryGetString("RechargeData", out rechargeData);
            rechargeData = _itemValue.GetPropertyOverride("RechargeData", rechargeData);
            if (!_itemValue.HasMetadata(rechargeData, TypedMetadataValue.TypeTag.Float))
            {
                _itemValue.SetMetadata(rechargeData, 0, TypedMetadataValue.TypeTag.Float);
            }
        }

        public void OnUpdate(ItemInventoryData invData)
        {
            ItemValue itemValue = invData.itemValue;
            float curTime = Time.time;
            float updateInterval = EffectManager.GetValue(CustomEnums.RechargeDataInterval, itemValue, 0, invData.holdingEntity, null, tags);
            if (curTime - lastUpdateTime > updateInterval)
            {
                lastUpdateTime = curTime;
                float cur = (float)itemValue.GetMetadata(rechargeData);
                float max = EffectManager.GetValue(CustomEnums.RechargeDataMaximum, itemValue, 0, invData.holdingEntity, null, tags);
                if (cur > max)
                {
                    //the result updated here won't exceed max so it's set somewhere else, decrease slowly
                    float dec = EffectManager.GetValue(CustomEnums.RechargeDataDecrease, itemValue, float.MaxValue, invData.holdingEntity, null, tags);
                    cur = Mathf.Max(cur - dec, max);
                }
                else if (cur < max)
                {
                    //add up and clamp to max
                    float add = EffectManager.GetValue(CustomEnums.RechargeDataValue, itemValue, 0, invData.holdingEntity, null, tags);
                    cur = Mathf.Min(cur + add, max);
                }
                itemValue.SetMetadata(rechargeData, cur, TypedMetadataValue.TypeTag.Float);
            }
        }

        public static FastTags ActionIndexToTag(int index)
        {
            switch (index)
            {
                case 0:
                    return FastTags.Parse("primary");
                case 1:
                    return FastTags.Parse("secondary");
                case 2:
                    return FastTags.Parse("tertiary");
                case 3:
                    return FastTags.Parse("quaternary");
                case 4:
                    return FastTags.Parse("quinary");
                default:
                    throw new IndexOutOfRangeException("ItemAction count is limited to 5!");
            }
        }
    }
}
