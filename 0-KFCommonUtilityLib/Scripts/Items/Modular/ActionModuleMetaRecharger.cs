using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Singletons;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged), typeof(MetaRechargerData))]
public class ActionModuleMetaRecharger
{
    public string rechargeData;

    [MethodTargetPostfix(nameof(ItemActionRanged.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        rechargeData = string.Empty;
        _props.Values.TryGetString("RechargeData", out rechargeData);
    }

    public class MetaRechargerData : IBackgroundInventoryUpdater
    {
        private ActionModuleMetaRecharger module;
        private float lastUpdateTime;
        public MetaRechargerData(ItemInventoryData _invData, int _indexOfAction, ActionModuleMetaRecharger _rechargeModule)
        {
            module = _rechargeModule;
            lastUpdateTime = Time.time;
            if (!_invData.itemValue.HasMetadata(_rechargeModule.rechargeData, TypedMetadataValue.TypeTag.Float))
            {
                _invData.itemValue.SetMetadata(_rechargeModule.rechargeData, 0, TypedMetadataValue.TypeTag.Float);
            }
            BackgroundInventoryUpdateManager.RegisterUpdater(_invData.holdingEntity, _invData.slotIdx, this);
        }

        public void OnUpdate(ItemInventoryData invData)
        {
            ItemValue itemValue = invData.itemValue;
            float curTime = Time.time;
            float updateInterval = EffectManager.GetValue(CustomEnums.RechargeDataInterval, itemValue, 0, invData.holdingEntity);
            if (curTime - lastUpdateTime > updateInterval)
            {
                lastUpdateTime = curTime;
                float cur = (float)itemValue.GetMetadata(module.rechargeData);
                float max = EffectManager.GetValue(CustomEnums.RechargeDataMaximum, itemValue, 0, invData.holdingEntity);
                if (cur > max)
                {
                    //the result updated here won't exceed max so it's set somewhere else, decrease slowly
                    float dec = EffectManager.GetValue(CustomEnums.RechargeDataDecrease, itemValue, float.MaxValue, invData.holdingEntity);
                    cur = Mathf.Max(cur - dec, max);
                }
                else if (cur < max)
                {
                    //add up and clamp to max
                    float add = EffectManager.GetValue(CustomEnums.RechargeDataValue, itemValue, 0, invData.holdingEntity);
                    cur = Mathf.Min(cur + add, max);
                }
                itemValue.SetMetadata(module.rechargeData, cur, TypedMetadataValue.TypeTag.Float);
            }
        }
    }
}
