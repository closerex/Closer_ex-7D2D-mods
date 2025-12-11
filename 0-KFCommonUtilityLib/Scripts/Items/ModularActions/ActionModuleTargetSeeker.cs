using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemAction)), TypeDataTarget(typeof(TargetSeekerData))]
internal class ActionModuleTargetSeeker
{
    public const int MAX_BODYPART_COUNT = 8;
    public FastTags<TagGroup.Global> tagsTargetSeekRange;
    public FastTags<TagGroup.Global> tagsTargetSeekAngleHor;
    public FastTags<TagGroup.Global> tagsTargetSeekAngleVer;
    public FastTags<TagGroup.Global> tagsTargetSeekAngleOffsetHor;
    public FastTags<TagGroup.Global> tagsTargetSeekAngleOffsetVer;
    public BodyPartSortingOrder sortingOrder;
    public bool hitAllTargets;
    public readonly MinEventActionTargetedBase targetCheckEvent = new MinEventActionTargetedBase();

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    public void Postfix_ReadFrom(DynamicProperties _props, ItemAction __instance)
    {
        _props.Values.TryGetValue("TargetSeekTags", out string tags);
        FastTags<TagGroup.Global> commonTags = string.IsNullOrEmpty(tags) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(tags);
        if (__instance is ItemActionDynamic)
        {
            commonTags |= __instance.ActionIndex != 1 ? FastTags<TagGroup.Global>.Parse("primary") : FastTags<TagGroup.Global>.Parse("secondary");
        }
        tagsTargetSeekRange = FastTags<TagGroup.Global>.Parse("TargetSeekRange") | commonTags;
        tagsTargetSeekAngleHor = FastTags<TagGroup.Global>.Parse("TargetSeekAngleHor") | commonTags;
        tagsTargetSeekAngleVer = FastTags<TagGroup.Global>.Parse("TargetSeekAngleVer") | commonTags;
        tagsTargetSeekAngleOffsetHor = FastTags<TagGroup.Global>.Parse("TargetSeekAngleOffsetHor") | commonTags;
        tagsTargetSeekAngleOffsetVer = FastTags<TagGroup.Global>.Parse("TargetSeekAngleOffsetVer") | commonTags;
        sortingOrder = new()
        {
            mask = (EnumBodyPartHit)int.MaxValue
        };
        unsafe
        {
            fixed(byte* ptr = sortingOrder.orders)
            {
                var span = MemoryMarshal.CreateSpan(ref *ptr, MAX_BODYPART_COUNT);
                span.Fill(byte.MaxValue);
            }
        }

        if (_props.Contains("TargetSeekBodyParts"))
        {
            string[] parts = _props.GetString("TargetSeekBodyParts").Split(',', StringSplitOptions.RemoveEmptyEntries);
            EnumBodyPartHit finalParts = EnumBodyPartHit.None;
            byte validParts = 0;
            foreach (string part in parts)
            {
                if (EnumUtils.TryParse(part, out EnumBodyPartHit partEnum))
                {
                    finalParts |= partEnum;
                    int orderIndex = BodyPartSortingOrder.BodyPartToOrderIndex(partEnum);
                    unsafe
                    {
                        if (sortingOrder.orders[orderIndex] > validParts)
                        {
                            sortingOrder.orders[orderIndex] = validParts;
                        }
                    }
                    validParts++;
                }
            }
            if (finalParts != EnumBodyPartHit.None)
            {
                sortingOrder.mask = finalParts;
            }
        }

        hitAllTargets = false;
        _props.ParseBool("HitAllTargets", ref hitAllTargets);

        if (_props.Contains("TargetSeekTargetTags"))
        {
            string tagStr = _props.GetString("TargetSeekTargetTags");
            targetCheckEvent.targetTags = FastTags<TagGroup.Global>.Parse(tagStr);
        }
        else
        {
            targetCheckEvent.targetTags = MinEventActionTargetedBase.enemy;
        }
    }

    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPostfix]
    public static void Postfix_StartHolding(TargetSeekerData __customData)
    {
        __customData.list_entities_around.Clear();
        __customData.list_hitinfo.Clear();
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    public static void Postfix_StopHolding(TargetSeekerData __customData)
    {
        __customData.list_entities_around.Clear();
        __customData.list_hitinfo.Clear();
    }

    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.Raycast)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionDynamicMelee_Raycast(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var mtd_floortoint = AccessTools.Method(typeof(Mathf), nameof(Mathf.FloorToInt));
        var mtd_gettarget = AccessTools.Method(typeof(ItemAction), nameof(ItemAction.GetExecuteActionTarget));
        var mtd_isvalid = AccessTools.Method(typeof(ItemActionDynamic), nameof(ItemActionDynamic.isHitValid));
        var prop_instance = AccessTools.PropertyGetter(typeof(IModuleContainerFor<TargetSeekerData>), nameof(IModuleContainerFor<TargetSeekerData>.Instance));
        var fld_hitinfo = AccessTools.Field(typeof(TargetSeekerData), nameof(TargetSeekerData.list_hitinfo));
        var prop_count = AccessTools.PropertyGetter(typeof(List<EntityRayHitInfo>), nameof(List<EntityRayHitInfo>.Count));
        var prop_item = AccessTools.IndexerGetter(typeof(List<EntityRayHitInfo>), new[] { typeof(int) });

        var lbd_targets = generator.DeclareLocal(typeof(List<EntityRayHitInfo>));
        var lbd_data_module = generator.DeclareLocal(typeof(TargetSeekerData));

        List<Label> lbls_loop_start = null;
        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_floortoint))
            {
                for (var j = i - 1; j >= 0; j--)
                {
                    if (codes[j].opcode == OpCodes.Ldloc_1)
                    {
                        var lbl_valid = generator.DefineLabel();
                        var lbls_original = codes[j].ExtractLabels();
                        codes.RemoveRange(j, i - j + 2);
                        codes.InsertRange(j, new[]
                        {
                            CodeInstruction.LoadArgument(1).WithLabels(lbls_original),
                            new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<TargetSeekerData>)),
                            new CodeInstruction(OpCodes.Callvirt, prop_instance),
                            new CodeInstruction(OpCodes.Dup),
                            CodeInstruction.StoreLocal(lbd_data_module.LocalIndex),
                            CodeInstruction.Call(typeof(TargetSeekerData), nameof(TargetSeekerData.GetValidTargetsInRange)),
                            new CodeInstruction(OpCodes.Brtrue_S, lbl_valid),
                            new CodeInstruction(OpCodes.Ldc_I4_0),
                            new CodeInstruction(OpCodes.Ret),
                            new CodeInstruction(OpCodes.Ldarg_0).WithLabels(lbl_valid),
                            new CodeInstruction(OpCodes.Dup),
                            CodeInstruction.LoadLocal(lbd_data_module.LocalIndex),
                            CodeInstruction.LoadField(typeof(TargetSeekerData), nameof(TargetSeekerData.seekRange)),
                            CodeInstruction.StoreField(typeof(ItemActionDynamic), nameof(ItemActionDynamic.Range)),
                            CodeInstruction.LoadLocal(lbd_data_module.LocalIndex),
                            CodeInstruction.LoadField(typeof(TargetSeekerData), nameof(TargetSeekerData.seekRange)),
                            CodeInstruction.StoreField(typeof(ItemActionDynamic), nameof(ItemActionDynamic.BlockRange)),
                            CodeInstruction.LoadLocal(lbd_data_module.LocalIndex),
                            CodeInstruction.LoadField(typeof(TargetSeekerData), nameof(TargetSeekerData.list_hitinfo)),
                            new CodeInstruction(OpCodes.Dup),
                            CodeInstruction.StoreLocal(lbd_targets.LocalIndex),
                            new CodeInstruction(OpCodes.Callvirt, prop_count),
                        });
                        i = j + 21;
                        break;
                    }
                }
            }
            else if (codes[i].Calls(mtd_gettarget))
            {
                lbls_loop_start = codes[i - 2].ExtractLabels();
                codes.RemoveRange(i - 2, 3);
                //get target from hit cache
                codes.InsertRange(i - 2, new[]
                {
                    CodeInstruction.LoadLocal(lbd_targets.LocalIndex).WithLabels(lbls_loop_start),
                    CodeInstruction.LoadLocal(5),
                    new CodeInstruction(OpCodes.Callvirt, prop_item),
                });
                for (int j = i + 1; j < codes.Count; j++)
                {
                    if (codes[j].Branches(out var lbl_loop_start) && lbls_loop_start.Contains(lbl_loop_start.Value))
                    {
                        //convert to for loop
                        var lbls_loop_end = codes[j + 1].labels;
                        codes[j - 2].operand = 5;
                        var lbl_check = generator.DefineLabel();
                        codes[j - 2].WithLabels(lbl_check);
                        var lbl_increase = generator.DefineLabel();
                        codes.InsertRange(j - 2, new[]
                        {
                            CodeInstruction.LoadLocal(5).WithLabels(lbl_increase),
                            new CodeInstruction(OpCodes.Ldc_I4_1),
                            new CodeInstruction(OpCodes.Add),
                            CodeInstruction.StoreLocal(5),
                        });
                        codes.Insert(i - 2, new CodeInstruction(OpCodes.Br_S, lbl_check));
                        i++;
                        for (int k = i + 1; k < j - 2; k++)
                        {
                            //break to continue
                            if (codes[k].Branches(out var lbl) && lbls_loop_end.Contains(lbl.Value))
                            {
                                codes[k].operand = lbl_increase;
                            }
                            //skip forced 20 targets check
                            else if (codes[k].LoadsConstant(20))
                            {
                                for (int l = k - 1; l >= i + 2; l--)
                                {
                                    if (codes[l].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[l].operand).LocalIndex == 5)
                                    {
                                        codes[k + 2].WithLabels(codes[l].ExtractLabels());
                                        int countRemoved = k - l + 2;
                                        codes.RemoveRange(l, countRemoved);
                                        k -= countRemoved;
                                        j -= countRemoved;
                                        break;
                                    }
                                }
                            }
                            else if (codes[k].Calls(mtd_isvalid))
                            {
                                for (int l = k - 1; l >= i + 2; l--)
                                {
                                    if (codes[l].opcode == OpCodes.Ldarg_0)
                                    {
                                        int countRemoved = k - l + 1;
                                        codes[k + 1].opcode = OpCodes.Brtrue_S;
                                        codes.RemoveRange(l, countRemoved);
                                        codes.InsertRange(l, new[]
                                        {
                                            CodeInstruction.LoadLocal(8),
                                            new CodeInstruction(OpCodes.Castclass, typeof(EntityRayHitInfo)),
                                            CodeInstruction.LoadField(typeof(EntityRayHitInfo), nameof(EntityRayHitInfo.entityHit)),
                                            CodeInstruction.StoreLocal(9),
                                            CodeInstruction.LoadArgument(0),
                                            CodeInstruction.LoadLocal(9),
                                            CodeInstruction.LoadLocal(0),
                                            new CodeInstruction(OpCodes.Ldc_I4_0),
                                            CodeInstruction.Call(typeof(ItemActionDynamic), nameof(ItemActionDynamic.shouldIgnoreTarget)),
                                        });
                                        k += 9 - countRemoved;
                                        j += 9 - countRemoved;
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }

        return codes;
    }

    public unsafe struct BodyPartSortingOrder
    {
        public EnumBodyPartHit mask;
        public fixed byte orders[MAX_BODYPART_COUNT];

        public static int BodyPartToOrderIndex(EnumBodyPartHit part)
        {
            if (part.HasFlag(EnumBodyPartHit.Head))
            {
                return 0;
            }
            if (part.HasFlag(EnumBodyPartHit.Torso))
            {
                return 1;
            }
            if (part.IsArm())
            {
                return 2;
            }
            if (part.IsLeg())
            {
                return 3;
            }
            if (part.HasFlag(EnumBodyPartHit.Special))
            {
                return 4;
            }
            return -1;
        }
    }

    public class EntityRayHitInfo : WorldRayHitInfo
    {
        public EntityAlive entityHit;
    }

    public class TargetSeekerData : IComparer<EnumBodyPartHit>
    {
        private ItemInventoryData invData;
        private ItemActionData actionData;
        private ActionModuleTargetSeeker module;
        public float seekRange;
        public Vector2 angleRangeHor, angleRangeVer;
        public readonly List<Entity> list_entities_around = new List<Entity>();
        public readonly List<EntityRayHitInfo> list_hitinfo = new List<EntityRayHitInfo>();

        public TargetSeekerData(ItemInventoryData _inventoryData, ItemActionData __instance, ActionModuleTargetSeeker __customModule)
        {
            invData = _inventoryData;
            actionData = __instance;
            module = __customModule;
        }

        int IComparer<EnumBodyPartHit>.Compare(EnumBodyPartHit x, EnumBodyPartHit y)
        {
            int orderx = BodyPartSortingOrder.BodyPartToOrderIndex(x), ordery = BodyPartSortingOrder.BodyPartToOrderIndex(y);
            if (orderx == ordery || orderx < 0 && ordery < 0)
            {
                return 0;
            }
            if (orderx > 0 && ordery < 0)
            {
                return -1;
            }
            if (orderx < 0 && ordery > 0)
            {
                return 1;
            }
            unsafe
            {
                int res = module.sortingOrder.orders[orderx] - module.sortingOrder.orders[ordery];
                if (res == 0)
                {
                    return orderx - ordery;
                }
                return res;
            }
        }

        public bool GetValidTargetsInRange()
        {
            list_entities_around.Clear();
            list_hitinfo.Clear();
            if (actionData.invData?.holdingEntity is not EntityPlayerLocal player)
                return false;

            seekRange = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, actionData.invData.itemValue, 0, player, null, module.tagsTargetSeekRange);
            Vector2 seekAngles = new Vector2(EffectManager.GetValue(CustomEnums.CustomTaggedEffect, actionData.invData.itemValue, 0, player, null, module.tagsTargetSeekAngleVer),
                                             EffectManager.GetValue(CustomEnums.CustomTaggedEffect, actionData.invData.itemValue, 0, player, null, module.tagsTargetSeekAngleHor));
            Vector2 seekAngleOffset = new Vector2(EffectManager.GetValue(CustomEnums.CustomTaggedEffect, actionData.invData.itemValue, 0, player, null, module.tagsTargetSeekAngleOffsetVer),
                                                  EffectManager.GetValue(CustomEnums.CustomTaggedEffect, actionData.invData.itemValue, 0, player, null, module.tagsTargetSeekAngleOffsetHor));
            angleRangeVer = new Vector2(seekAngleOffset.x - seekAngles.x * .5f, seekAngleOffset.x + seekAngles.x * .5f);
            angleRangeHor = new Vector2(seekAngleOffset.y - seekAngles.y * .5f, seekAngleOffset.y + seekAngles.y * .5f);
            player.world.GetEntitiesAround(EntityFlags.All, player.position, seekRange, list_entities_around);
            if (list_entities_around.Count > 0)
            {
                foreach (EntityAlive entity in list_entities_around)
                {
                    if (IsEntityValidTarget(entity))
                    {
                        //log sth?
                    }
                }
                if (list_hitinfo.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsEntityValidTarget(EntityAlive entity)
        {
            var attacker = invData.holdingEntity as EntityPlayerLocal;
            var originTransform = attacker?.playerCamera.transform;
            if (attacker == null || attacker.IsDead() || entity == null || entity.IsDead() || originTransform == null || !module.targetCheckEvent.isValidTarget(attacker, entity))
            {
                return false;
            }

            var colliders = entity.GetComponentsInChildren<Collider>().Select(c => (collider: c, part: DamageSource.TagToBodyPart(c.tag)))
                                                                      .Where(c => module.sortingOrder.mask.HasFlag(c.part))
                                                                      .OrderBy(c => c.part, this).ToArray();

            if (colliders.Length > 0)
            {
                foreach (var pair in colliders)
                {
                    if (IsTargetInAngle.IsPointInPyramidAngle(pair.collider.bounds.center - originTransform.position, originTransform.forward, originTransform.right, originTransform.up, angleRangeHor, angleRangeVer))
                    {
                        Vector3 direction = pair.collider.bounds.center - originTransform.position;
                        Ray ray = new(originTransform.position + Origin.position, direction.normalized);
                        //todo: check hit mask
                        if (!Voxel.Raycast(attacker.world, ray, direction.magnitude, 65536, 66, 0))
                        {
                            EntityRayHitInfo hitInfo = new()
                            {
                                ray = ray,
                                bHitValid = true,
                                transform = pair.collider.transform,
                                hitCollider = pair.collider,
                                tag = pair.collider.tag,
                                hit = new()
                                {
                                    distanceSq = direction.sqrMagnitude,
                                    pos = pair.collider.bounds.center + Origin.position,
                                },
                                hitTriangleIdx = 0,
                                entityHit = entity
                            };
                            hitInfo.fmcHit = hitInfo.hit;
                            if (!module.hitAllTargets && list_hitinfo.Count > 0)
                            {
                                if (list_hitinfo.Count > 1)
                                {
                                    list_hitinfo.RemoveRange(1, list_hitinfo.Count - 1);
                                }
                                if (Vector3.Angle(originTransform.forward, ray.direction) - Vector3.Angle(originTransform.forward, list_hitinfo[0].ray.direction) < 0)
                                {
                                    list_hitinfo[0] = hitInfo;
                                    return true;
                                }
                                return false;
                            }
                            list_hitinfo.Add(hitInfo);
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}