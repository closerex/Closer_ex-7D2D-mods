using KFCommonUtilityLib.Scripts.Attributes;
using System;
using UniLinq;

[TypeTarget(typeof(ItemActionRanged))]
public class ActionModuleMetaConsumer
{
    public string[] consumeDatas;
    public FastTags[] consumeTags;
    private float[] consumeStocks;
    private float[] consumeValues;
    private static FastTags TagsConsumption = FastTags.Parse("ConsumptionValue");

    [MethodTargetPostfix(nameof(ItemActionRanged.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props, ItemAction __instance)
    {
        string consumeData = string.Empty;
        _props.Values.TryGetString("ConsumeData", out consumeData);
        _props.Values.TryGetString("ConsumeTags", out string tags);
        FastTags commonTags = string.IsNullOrEmpty(tags) ? FastTags.none : FastTags.Parse(tags);
        if (string.IsNullOrEmpty(consumeData))
        {
            Log.Error($"No consume data found on item {__instance.item.Name} action {__instance.ActionIndex}");
            return;
        }

        consumeDatas = consumeData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        consumeTags = consumeDatas.Select(s => FastTags.Parse(s) | commonTags | TagsConsumption).ToArray();
        consumeStocks = new float[consumeDatas.Length];
        consumeValues = new float[consumeDatas.Length];
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
            if (holdingEntity.inventory.holdingItemItemValue.PercentUsesLeft <= 0f || (_data.curBurstCount >= burstCount && burstCount != -1) || (!__instance.InfiniteAmmo && itemValue.Meta <= 0))
            {
                return true;
            }

            for (int i = 0; i < consumeDatas.Length; i++)
            {
                string consumeData = consumeDatas[i];
                float stock = (float)itemValue.GetMetadata(consumeData);
                float consumption = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, itemValue, float.MaxValue, _actionData.invData.holdingEntity, null, consumeTags[i]);
                if (stock < consumption)
                {
                    if (!_data.bPressed)
                    {
                        holdingEntity.PlayOneShot(___soundEmpty);
                        _data.bPressed = true;
                    }
                    return false;
                }
                consumeStocks[i] = stock;
                consumeValues[i] = consumption;
            }

            for (int i = 0; i < consumeDatas.Length; i++)
            {
                itemValue.SetMetadata(consumeDatas[i], consumeStocks[i] - consumeValues[i], TypedMetadataValue.TypeTag.Float);
                holdingEntity.MinEventContext.Tags = consumeTags[i];
                holdingEntity.FireEvent(CustomEnums.onRechargeValueUpdate, true);
            }
        }
        return true;
    }
}