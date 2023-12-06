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
    public string rechargeData;

    [MethodTargetPostfix(nameof(ItemActionRanged.OnModificationsChanged))]
    private void Postfix_OnModificationChanged(RechargeData __customData, ItemActionRanged __instance, ItemActionData _data)
    {
        __customData.UpdateRechargeTarget(__instance.Properties, _data.invData.itemValue);
    }

    public class RechargeData : IBackgroundInventoryUpdater
    {
        public ActionModuleRecharge module;
        private float lastUpdateTime;
        private TypedMetadataValue valueToUpdate;
        public RechargeData(ItemInventoryData _invData, int _indexOfAction, ActionModuleRecharge _rechargeModule)
        {
            module = _rechargeModule;
            lastUpdateTime = Time.time;
        }

        public void UpdateRechargeTarget(DynamicProperties _properties, ItemValue _itemValue)
        {

        }

        public void OnUpdate(ItemInventoryData invData)
        {
            throw new NotImplementedException();
        }
    }
}
