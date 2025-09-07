using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace SMXMultiActionCompatibilityPatch
{
    public class SMXMultiActionCompatibilityPatchInit : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (inited)
                return;
            inited = true;
            Log.Out(" Loading Patch: " + GetType());
            var harmony = new HarmonyLib.Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch]
    public static class SMXMACPatch
    {
        [HarmonyPatch(typeof(SMXcore.XUiC_HUDActiveItem), "HasChanged")]
        [HarmonyPostfix]
        private static void Postfix_XUiC_HUDActiveItem_HasChanged(EntityPlayer ___localPlayer, ref bool __result)
        {
            if (!__result)
            {
                __result |= ___localPlayer.inventory.holdingItem?.Actions?[0]?.IsStatChanged() ?? false;
            }
        }

        [HarmonyPatch(typeof(SMXcore.XUiC_HUDActiveItem), "SetupActiveItemEntry")]
        [HarmonyPrefix]
        private static bool Prefix_XUiC_HUDActiveItem_SetupActiveItemEntry(EntityPlayer ___localPlayer)
        {
            ___localPlayer.MinEventContext.ItemActionData = ___localPlayer.inventory.holdingItemData?.actionData?[MultiActionManager.GetActionIndexForEntity(___localPlayer)] ?? ___localPlayer.MinEventContext.ItemActionData;
            return true;
        }

        [HarmonyPatch(typeof(SMXcore.XUiC_HUDActiveItem), "SetupActiveItemEntry")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_XUiC_HUDActiveItem_SetupActiveItemEntry(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var lbd_index = generator.DeclareLocal(typeof(int));
            FieldInfo fld_action = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));
            FieldInfo fld_ammoindex = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_action))
                {
                    codes.RemoveAt(i + 1);
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(SMXcore.XUiC_HUDActiveItem), "itemValue"),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetActionIndexByMetaData)),
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_index)
                    });
                    i += 4;
                }
                else if (codes[i].LoadsField(fld_ammoindex))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetSelectedAmmoIndexByActionIndex));
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_S, lbd_index));
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(SMXcore.XUiC_HUDActiveItem), nameof(SMXcore.XUiC_HUDActiveItem.Update))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Update_XUiC_HUDStatBar(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo mtd_getfocus = AccessTools.Method(typeof(Inventory), nameof(Inventory.GetFocusedItemIdx));
            MethodInfo mtd_getholding = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItemIdx));

            foreach (var ins in instructions)
            {
                if (ins.Calls(mtd_getfocus))
                {
                    ins.operand = mtd_getholding;
                }
                yield return ins;
            }
        }
    }
}
