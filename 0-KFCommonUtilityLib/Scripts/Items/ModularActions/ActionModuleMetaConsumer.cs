using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;

[TypeTarget(typeof(ItemAction)), TypeDataTarget(typeof(MetaConsumerData))]
public class ActionModuleMetaConsumer
{
    public string[] consumeDatas;
    public FastTags<TagGroup.Global>[] consumeTags;
    private static FastTags<TagGroup.Global> TagsConsumption = FastTags<TagGroup.Global>.Parse("ConsumptionValue");

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    private void Postfix_ReadFrom(DynamicProperties _props, ItemAction __instance)
    {
        string consumeData = string.Empty;
        _props.Values.TryGetValue("ConsumeData", out consumeData);
        if (string.IsNullOrEmpty(consumeData))
        {
            Log.Error($"No consume data found on item {__instance.item.Name} action {__instance.ActionIndex}");
            return;
        }

        _props.Values.TryGetValue("ConsumeTags", out string tags);
        FastTags<TagGroup.Global> commonTags = string.IsNullOrEmpty(tags) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(tags);
        if (__instance is ItemActionDynamic)
        {
            commonTags |= __instance.ActionIndex != 1 ? FastTags<TagGroup.Global>.Parse("primary") : FastTags<TagGroup.Global>.Parse("secondary");
        }

        consumeDatas = consumeData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        consumeTags = consumeDatas.Select(s => FastTags<TagGroup.Global>.Parse(s) | commonTags | TagsConsumption).ToArray();
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    private void Postfix_OnModificationsChanged(MetaConsumerData __customData)
    {
        __customData.consumeStocks = new float[consumeDatas.Length];
        __customData.consumeValues = new float[consumeDatas.Length];
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemAction.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_ExecuteAction(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var fld_started = AccessTools.Field(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.burstShotStarted));
        var fld_infinite = AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.InfiniteAmmo));
        var mtd_consume = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.ConsumeAmmo));
        var lbd_module = generator.DeclareLocal(typeof(ActionModuleMetaConsumer));
        var lbd_data_module = generator.DeclareLocal(typeof(ActionModuleMetaConsumer.MetaConsumerData));
        var prop_instance = AccessTools.PropertyGetter(typeof(IModuleContainerFor<ActionModuleMetaConsumer>), nameof(IModuleContainerFor<ActionModuleMetaConsumer>.Instance));
        var prop_data_instance = AccessTools.PropertyGetter(typeof(IModuleContainerFor<ActionModuleMetaConsumer.MetaConsumerData>), nameof(IModuleContainerFor<ActionModuleMetaConsumer.MetaConsumerData>.Instance));
        var prop_itemvalue = AccessTools.PropertyGetter(typeof(ItemInventoryData), nameof(ItemInventoryData.itemValue));

        int localIndexEntity;
        if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 4))
        {
            localIndexEntity = 6;
        }
        else
        {
            localIndexEntity = 7;
        }

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == localIndexEntity)
            {
                codes.InsertRange(i - 4, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[i - 4].ExtractLabels()),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ActionModuleMetaConsumer>)),
                    new CodeInstruction(OpCodes.Callvirt, prop_instance),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_module),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ActionModuleMetaConsumer.MetaConsumerData>)),
                    new CodeInstruction(OpCodes.Callvirt, prop_data_instance),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_data_module),
                });
                i += 8;
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
                    new CodeInstruction(OpCodes.Ldloc_S, localIndexEntity),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(ItemActionAttack), nameof(ItemActionAttack.soundEmpty)),
                    CodeInstruction.Call(typeof(ActionModuleMetaConsumer), nameof(CheckAndCacheMetaData)),
                    new CodeInstruction(OpCodes.Brtrue_S, lbl),
                    new CodeInstruction(OpCodes.Ret)
                });
                original.WithLabels(lbl);
                i += 11;
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
                    new CodeInstruction(OpCodes.Ldloc_S, localIndexEntity),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                    CodeInstruction.Call(typeof(ActionModuleMetaConsumer), nameof(ConsumeMetaData)),
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemAction.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionDynamicMelee_ExecuteAction(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var fld_attacking = AccessTools.Field(typeof(ItemActionDynamicMelee.ItemActionDynamicMeleeData), nameof(ItemActionDynamicMelee.ItemActionDynamicMeleeData.Attacking));
        var fld_released = AccessTools.Field(typeof(ItemActionDynamicMelee.ItemActionDynamicMeleeData), nameof(ItemActionDynamicMelee.ItemActionDynamicMeleeData.HasReleased));
        var prop_instance = AccessTools.PropertyGetter(typeof(IModuleContainerFor<ActionModuleMetaConsumer>), nameof(IModuleContainerFor<ActionModuleMetaConsumer>.Instance));
        var prop_data_instance = AccessTools.PropertyGetter(typeof(IModuleContainerFor<ActionModuleMetaConsumer.MetaConsumerData>), nameof(IModuleContainerFor<ActionModuleMetaConsumer.MetaConsumerData>.Instance));
        var prop_itemvalue = AccessTools.PropertyGetter(typeof(ItemInventoryData), nameof(ItemInventoryData.itemValue));
        var mtd_canstart = AccessTools.Method(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.canStartAttack));

        var lbd_module = generator.DeclareLocal(typeof(ActionModuleMetaConsumer));
        var lbd_data_module = generator.DeclareLocal(typeof(ActionModuleMetaConsumer.MetaConsumerData));

        for (var i = 1; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_0)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ActionModuleMetaConsumer>)),
                    new CodeInstruction(OpCodes.Callvirt, prop_instance),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_module),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ActionModuleMetaConsumer.MetaConsumerData>)),
                    new CodeInstruction(OpCodes.Callvirt, prop_data_instance),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_data_module),
                });
                i += 8;
            }
            else if (codes[i].StoresField(fld_attacking) && codes[i - 1].LoadsConstant(1))
            {
                codes.InsertRange(i - 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_module.LocalIndex).WithLabels(codes[i - 2].ExtractLabels()),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.invData)),
                    new CodeInstruction(OpCodes.Callvirt, prop_itemvalue),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module.LocalIndex),
                    CodeInstruction.Call(typeof(ActionModuleMetaConsumer), nameof(ConsumeMetaData)),
                });
                i += 7;
            }
            else if (codes[i].Calls(mtd_canstart))
            {
                for (int j = i + 1; j < codes.Count; j++)
                {
                    if (codes[j].StoresField(fld_released))
                    {
                        var lbl = generator.DefineLabel();
                        codes[j - 2].WithLabels(lbl);
                        codes.InsertRange(i - 2, new[]
                        {
                            new CodeInstruction(OpCodes.Ldloc_S, lbd_module.LocalIndex).WithLabels(codes[i - 2].ExtractLabels()),
                            new CodeInstruction(OpCodes.Ldloc_0),
                            CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.invData)),
                            new CodeInstruction(OpCodes.Callvirt, prop_itemvalue),
                            new CodeInstruction(OpCodes.Ldloc_0),
                            CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.invData)),
                            CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.holdingEntity)),
                            new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module.LocalIndex),
                            new CodeInstruction(OpCodes.Ldnull),
                            CodeInstruction.Call(typeof(ActionModuleMetaConsumer), nameof(CheckAndCacheMetaData)),
                            new CodeInstruction(OpCodes.Brfalse_S, lbl)
                        });
                        i += 11;
                        break;
                    }
                }
            }
        }

        return codes;
    }

    public bool CheckAndCacheMetaData(ItemValue itemValue, EntityAlive holdingEntity, MetaConsumerData customData, string soundEmpty)
    {
        for (int i = 0; i < consumeDatas.Length; i++)
        {
            string consumeData = consumeDatas[i];
            float stock = (float)itemValue.GetMetadata(consumeData);
            float consumption = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, itemValue, float.MaxValue, holdingEntity, null, consumeTags[i]);
            if (stock < consumption)
            {
                if (!string.IsNullOrEmpty(soundEmpty))
                {
                    holdingEntity.PlayOneShot(soundEmpty);
                }
                return false;
            }
            customData.consumeStocks[i] = stock;
            customData.consumeValues[i] = consumption;
        }
        return true;
    }

    public void ConsumeMetaData(ItemValue itemValue, EntityAlive holdingEntity, MetaConsumerData customData)
    {
        for (int i = 0; i < consumeDatas.Length; i++)
        {
            itemValue.SetMetadata(consumeDatas[i], customData.consumeStocks[i] - customData.consumeValues[i], TypedMetadataValue.TypeTag.Float);
            holdingEntity.MinEventContext.Tags = consumeTags[i];
            holdingEntity.FireEvent(CustomEnums.onRechargeValueUpdate, true);
        }
    }

    public class MetaConsumerData
    {
        public float[] consumeStocks;
        public float[] consumeValues;
    }
}