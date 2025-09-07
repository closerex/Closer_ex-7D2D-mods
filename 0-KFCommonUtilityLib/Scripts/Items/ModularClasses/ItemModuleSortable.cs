using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;

[TypeTarget(typeof(ItemClassModifier))]
public class ItemModuleSortable
{
    //public int priority = int.MaxValue;

    //[HarmonyPatch(nameof(ItemClassModifier.Init)), MethodTargetPostfix]
    //public void Postfix_Init(ItemClassModifier __instance)
    //{
    //    __instance.Properties.ParseInt("ModSortPriority", ref priority);
    //}
}

public struct ItemModuleSortableComparer : IComparer<ItemValue>
{
    private int itemId;
    public ItemModuleSortableComparer(ItemValue item)
    {
        itemId = item.ItemClass.Id;
    }

    public int Compare(ItemValue x, ItemValue y)
    {
        return GetPriority(x) - GetPriority(y);
    }

    private int GetPriority(ItemValue itemValue)
    {
        if (itemValue.ItemClass is ItemClassModifier modifierClass)
        {
            string str = null;
            if (modifierClass.GetPropertyOverride("ModSortPriority", ItemClass.GetForId(itemId).GetItemName(), ref str) && int.TryParse(str, out int priority))
            {
                return priority;
            }
        }
        return int.MaxValue;
    }
}

[HarmonyPatch]
public static class ModSortingPatches
{
    //[HarmonyPatch(typeof(XUiC_ItemPartStackGrid), nameof(XUiC_ItemPartStackGrid.HandleSlotChangedEvent))]
    //[HarmonyTranspiler]
    //public static IEnumerable<CodeInstruction> Transpiler_HandleSlotChangedEvent_XUiC_ItemPartStackGrid(IEnumerable<CodeInstruction> instructions)
    //{
    //    var codes = instructions.ToList();
    //    var prop_setstack = AccessTools.PropertySetter(typeof(XUiC_AssembleWindow), nameof(XUiC_AssembleWindow.ItemStack));
    //    var idx = codes.FindIndex(x => x.Calls(prop_setstack));
    //    if (idx > 0)
    //    {
    //        codes.InsertRange(idx, new[]
    //        {
    //            new CodeInstruction(OpCodes.Dup),
    //            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ModSortingPatches), nameof(SortMods)))
    //        });
    //    }
    //    return codes;
    //}

    //[HarmonyPatch(typeof(XUiC_ItemPartStackGrid), nameof(XUiC_ItemPartStackGrid.HandleSlotChangedEvent))]
    //[HarmonyPostfix]
    //private static void Postfix_HandleSlotChangedEvent_XUiC_ItemPartStackGrid(XUiC_ItemPartStackGrid __instance)
    //{
    //    __instance.SetParts(__instance.CurrentItem.itemValue?.Modifications);
    //}

    [HarmonyPatch(typeof(XUiC_AssembleWindowGroup), nameof(XUiC_AssembleWindowGroup.ItemStack), MethodType.Setter)]
    [HarmonyPrefix]
    private static void Postfix_set_ItemStack_XUiC_AssembleWindowGroup(ItemStack value)
    {
        SortMods(value);
    }

    private static void SortMods(ItemStack itemStack)
    {
        if (itemStack?.itemValue?.Modifications != null)
        {
            Array.Sort(itemStack.itemValue.Modifications, new ItemModuleSortableComparer(itemStack.itemValue));
        }
    }
}