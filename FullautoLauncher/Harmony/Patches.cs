using FullautoLauncher.Scripts.ProjectileManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using UnityEngine;

[HarmonyPatch]
class ItemActionLauncherProjectilePatch
{
    public static FieldInfo fldinfo_meta = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Meta));
    public static MethodInfo mtdinfo_gbc = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.GetBurstCount), new Type[] { typeof(ItemActionData) });
    public static MethodInfo mtdinfo_gac = AccessTools.Method(typeof(AnimatorRangedReloadState), "GetAmmoCount", new Type[] { typeof(EntityAlive), typeof(ItemValue), typeof(Int32) });
    public static MethodInfo mtdinfo_sta = AccessTools.Method(typeof(GameObject), nameof(GameObject.SetActive), new Type[] { typeof(bool) });
    public static int getProjectileCount(ItemActionData _data)
    {
        int rps = 1;
        ItemInventoryData invD = _data != null ? _data.invData : null;
        if (invD != null)
        {
            ItemClass item = invD.itemValue != null ? invD.itemValue.ItemClass : null;
            rps = (int)EffectManager.GetValue(PassiveEffects.RoundRayCount, invD.itemValue, rps, invD.holdingEntity, null, item != null ? item.ItemTags | _data.ActionTags : default);
        }
        return rps > 0 ? rps : 1;
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.StartHolding))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_StartHolding_ItemActionLauncher(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new List<CodeInstruction>(instructions);

        LocalBuilder lbd_rps = generator.DeclareLocal(typeof(int));

        var list_insert = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldloc_S, lbd_rps),
            new CodeInstruction(OpCodes.Mul)
        };

        for (int i = 0; i < codes.Count; i++)
        {
            if ( codes[i].LoadsField(fldinfo_meta))
            {
                codes.InsertRange(i + 1, list_insert);
                i += list_insert.Count;
            }
        }

        codes.InsertRange(0, new CodeInstruction[]
        {
            new CodeInstruction(OpCodes.Ldarg_1),
            CodeInstruction.Call(typeof(ItemActionLauncherProjectilePatch), nameof(getProjectileCount), new Type[] { typeof(ItemActionData) }),
            new CodeInstruction(OpCodes.Stloc_S, lbd_rps)
        });

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.ItemActionEffects))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionEffects_ItemActionLauncher(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for(int i = 0, totali = codes.Count; i < totali; i++)
        {
            if (codes[i].Calls(mtdinfo_gbc))
            {
                codes.InsertRange(i + 1, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_2),
                    CodeInstruction.Call(typeof(ItemActionLauncherProjectilePatch), nameof(getProjectileCount), new Type[] { typeof(ItemActionData) })
                });
                codes.RemoveRange(i - 2, 3);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.ConsumeAmmo))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ConsumeAmmo_ItemActionLauncher(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>()
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldarg_1),
            CodeInstruction.Call(typeof(ItemActionRanged), nameof(ItemActionRanged.ConsumeAmmo), new Type[]{ typeof(ItemActionData) }),
            new CodeInstruction(OpCodes.Ret)
        };

        return codes;
    }

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.ExecuteAction))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemClass(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Isinst && codes[i].OperandIs(typeof(ItemActionLauncher)))
            {
                codes.RemoveRange(i - 2, 4);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.OnStateEnter))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_OnStateEnter_AnimatorRangedReloadState(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new List<CodeInstruction>(instructions);

        LocalBuilder lbd_rps = generator.DeclareLocal(typeof(int));

        for (int i = 0, totali = codes.Count; i < totali; i++)
        {
            if (codes[i].Calls(mtdinfo_gac))
            {
                codes.InsertRange(i + 1, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, 6),
                    CodeInstruction.Call(typeof(ItemActionLauncherProjectilePatch), nameof(getProjectileCount), new Type[] { typeof(ItemActionData) }),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_rps),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_rps),
                    new CodeInstruction(OpCodes.Mul)
                });
                totali += 5;
                break;
            }
            //else if (codes[i].opcode == OpCodes.Ldc_R4 && codes[i].OperandIs(0.005f))
            //{
            //    codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_R4, 0f));
            //    codes.RemoveRange(i - 2, 4);
            //    break;
            //}
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.instantiateProjectile))]
    [HarmonyPrefix]
    private static bool Prefix_instantiateProjectile_ItemActionLauncher(ref Vector3 _positionOffset)
    {
        _positionOffset = Vector3.zero;
        return true;
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.instantiateProjectile))]
    [HarmonyPostfix]
    private static void Postfix_instantiateProjectile_ItemActionLauncher(Transform __result, ItemActionLauncher __instance)
    {
        if (__instance.Properties.Contains("VisibleInMag") && !__instance.Properties.GetBool("VisibleInMag"))
        {
            __result.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(GameManager), "FixedUpdate")]
    [HarmonyPostfix]
    private static void Postfix_FixedUpdate_GameManager()
    {
        CustomProjectileManager.FixedUpdate();
    }

    //custom projectile manager workarounds
    private static void ParseProjectileType(XElement _node)
    {
        string itemName = _node.GetAttribute("name");
        if (string.IsNullOrEmpty(itemName))
        {
            return;
        }
        ItemClass item = ItemClass.GetItemClass(itemName);
        for (int i = 0; i < item.Actions.Length; i++)
        {
            if (item.Actions[i] is ItemActionProjectile proj && proj.Properties.Contains("CustomProjectileType"))
            {
                CustomProjectileManager.InitClass(item, proj.Properties.GetString("CustomProjectileType"));
            }
        }
    }

    [HarmonyPatch(typeof(ItemClassesFromXml), nameof(ItemClassesFromXml.parseItem))]
    [HarmonyPostfix]
    private static void Postfix_parseItem_ItemClassesFromXml(XElement _node)
    {
        ParseProjectileType(_node);
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveAndCleanupWorld))]
    [HarmonyPostfix]
    private static void Postfix_SaveAndCleanupWorld_GameManager()
    {
        CustomProjectileManager.Cleanup();
    }
}