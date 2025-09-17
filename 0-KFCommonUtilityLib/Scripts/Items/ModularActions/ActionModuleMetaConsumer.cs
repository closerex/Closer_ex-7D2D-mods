using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;

[TypeTarget(typeof(ItemActionRanged))]
public class ActionModuleMetaConsumer
{
    public string[] consumeDatas;
    public FastTags<TagGroup.Global>[] consumeTags;
    private float[] consumeStocks;
    private float[] consumeValues;
    private static FastTags<TagGroup.Global> TagsConsumption = FastTags<TagGroup.Global>.Parse("ConsumptionValue");

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    private void Postfix_ReadFrom(DynamicProperties _props, ItemAction __instance)
    {
        string consumeData = string.Empty;
        _props.Values.TryGetValue("ConsumeData", out consumeData);
        _props.Values.TryGetValue("ConsumeTags", out string tags);
        FastTags<TagGroup.Global> commonTags = string.IsNullOrEmpty(tags) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(tags);
        if (string.IsNullOrEmpty(consumeData))
        {
            Log.Error($"No consume data found on item {__instance.item.Name} action {__instance.ActionIndex}");
            return;
        }

        consumeDatas = consumeData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        consumeTags = consumeDatas.Select(s => FastTags<TagGroup.Global>.Parse(s) | commonTags | TagsConsumption).ToArray();
        consumeStocks = new float[consumeDatas.Length];
        consumeValues = new float[consumeDatas.Length];
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemAction.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_ExecuteAction(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var fld_started = AccessTools.Field(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.burstShotStarted));
        var fld_infinite = AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.InfiniteAmmo));
        var mtd_consume = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.ConsumeAmmo));
        var lbd_module = generator.DeclareLocal(typeof(ActionModuleMetaConsumer));
        var prop_instance = AccessTools.PropertyGetter(typeof(IModuleContainerFor<ActionModuleMetaConsumer>), nameof(IModuleContainerFor<ActionModuleMetaConsumer>.Instance));
        var prop_itemvalue = AccessTools.PropertyGetter(typeof(ItemInventoryData), nameof(ItemInventoryData.itemValue));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 6)
            {
                codes.InsertRange(i - 4, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[i - 4].ExtractLabels()),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ActionModuleMetaConsumer>)),
                    new CodeInstruction(OpCodes.Callvirt, prop_instance),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_module),
                });
                i += 4;
            }
            else if (codes[i].StoresField(fld_started) && codes[i - 1].LoadsConstant(1))
            {
                var lbl = generator.DefineLabel();
                var original = codes[i - 2];
                codes.InsertRange(i - 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_module).WithLabels(original.ExtractLabels()),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.invData)),
                    new CodeInstruction(OpCodes.Callvirt, prop_itemvalue),
                    new CodeInstruction(OpCodes.Ldloc_S, 6),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(ItemActionAttack), nameof(ItemActionAttack.soundEmpty)),
                    CodeInstruction.Call(typeof(ActionModuleMetaConsumer), nameof(CheckAndCacheMetaData)),
                    new CodeInstruction(OpCodes.Brtrue_S, lbl),
                    new CodeInstruction(OpCodes.Ret)
                });
                original.WithLabels(lbl);
                i += 10;
            }
            else if (codes[i].Calls(mtd_consume))
            {
                var lbl = generator.DefineLabel();
                for (int j = i - 1; j >= 0; j--)
                {
                    if (codes[j].LoadsField(fld_infinite) && codes[j + 1].Branches(out _))
                    {
                        codes[j + 1].operand = lbl;
                        break;
                    }
                }
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_module).WithLabels(lbl),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.invData)),
                    new CodeInstruction(OpCodes.Callvirt, prop_itemvalue),
                    new CodeInstruction(OpCodes.Ldloc_S, 6),
                    CodeInstruction.Call(typeof(ActionModuleMetaConsumer), nameof(ConsumeMetaData)),
                });
                break;
            }
        }

        return codes;
    }

    public bool CheckAndCacheMetaData(ItemValue itemValue, EntityAlive holdingEntity, string soundEmpty)
    {
        for (int i = 0; i < consumeDatas.Length; i++)
        {
            string consumeData = consumeDatas[i];
            float stock = (float)itemValue.GetMetadata(consumeData);
            float consumption = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, itemValue, float.MaxValue, holdingEntity, null, consumeTags[i]);
            if (stock < consumption)
            {
                holdingEntity.PlayOneShot(soundEmpty);
                return false;
            }
            consumeStocks[i] = stock;
            consumeValues[i] = consumption;
        }
        return true;
    }

    public void ConsumeMetaData(ItemValue itemValue, EntityAlive holdingEntity)
    {
        for (int i = 0; i < consumeDatas.Length; i++)
        {
            itemValue.SetMetadata(consumeDatas[i], consumeStocks[i] - consumeValues[i], TypedMetadataValue.TypeTag.Float);
            holdingEntity.MinEventContext.Tags = consumeTags[i];
            holdingEntity.FireEvent(CustomEnums.onRechargeValueUpdate, true);
        }
    }
}