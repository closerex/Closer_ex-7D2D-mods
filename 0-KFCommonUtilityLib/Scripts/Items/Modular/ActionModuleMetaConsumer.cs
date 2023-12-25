using Audio;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using SteelSeries.GameSense;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ItemActionAltMode;

[TypeTarget(typeof(ItemActionRanged))]
public class ActionModuleMetaConsumer
{
    public string consumeData;

    [MethodTargetPostfix(nameof(ItemActionRanged.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        consumeData = string.Empty;
        _props.Values.TryGetString("ConsumeData", out consumeData);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.ExecuteAction))]
    private bool Prefix_ExecuteAction(ItemActionData _actionData, bool _bReleased, ItemActionRanged __instance, string ___soundEmpty)
    {
        ItemActionRanged.ItemActionDataRanged _data = _actionData as ItemActionRanged.ItemActionDataRanged;
        EntityAlive holdingEntity = _actionData.invData.holdingEntity;
        ItemValue itemValue = _actionData.invData.itemValue;
        if (!_bReleased)
        {
            int burstCount = __instance.GetBurstCount(_actionData);
            if (holdingEntity.inventory.holdingItemItemValue.PercentUsesLeft <= 0f ||(_data.curBurstCount >= burstCount && burstCount != -1) || (!__instance.InfiniteAmmo && itemValue.Meta <= 0))
            {
                return true;
            }

            float stock = (float)itemValue.GetMetadata(consumeData);
            float consumption = EffectManager.GetValue(CustomEnums.ConsumptionValue, itemValue, float.MaxValue, _actionData.invData.holdingEntity);
            if (stock < consumption)
            {
                holdingEntity.PlayOneShot(___soundEmpty);
                return false;
            }

            itemValue.SetMetadata(consumeData, stock - consumption, TypedMetadataValue.TypeTag.Float);
        }
        return true;
    }
}