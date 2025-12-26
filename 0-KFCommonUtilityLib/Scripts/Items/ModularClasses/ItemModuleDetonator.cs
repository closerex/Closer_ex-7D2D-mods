using Audio;
using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[TypeTarget(typeof(ItemClassExtendedFunction)), TypeDataTarget(typeof(DetonatorData))]
public class ItemModuleDetonator
{
    public string[] detonateItems;
    public int[] detonateItemIds;
    public string activationSound;
    public string noTargetSound;
    public float detonateRange;
    public int detonateCount;
    public float updateItemCacheInterval;
    public string defaultNavObjName;
    public string defaultNavObjSprite;
    public string defaultNavObjText;
    public Color activeNavObjColor;
    //public Color inactiveNavObjColor;

    [HarmonyPatch(nameof(ItemClass.Init)), MethodTargetPostfix]
    public void Postfix_Init(ItemClass __instance)
    {
        var prop = __instance.Properties;
        if (prop.Contains("DetonateItems"))
        {
            detonateItems = prop.GetString("DetonateItems").Split(',', StringSplitOptions.RemoveEmptyEntries);
        }
        detonateRange = 50;
        prop.ParseFloat("DetonateRange", ref detonateRange);
        detonateCount = 0;
        prop.ParseInt("DetonateCount", ref detonateCount);
        updateItemCacheInterval = 0;
        prop.ParseFloat("UpdateItemCacheInterval", ref updateItemCacheInterval);
        if (prop.Contains("ActivationSound"))
        {
            activationSound = prop.GetString("ActivationSound");
        }
        if (prop.Contains("NoTargetSound"))
        {
            noTargetSound = prop.GetString("NoTargetSound");
        }
        defaultNavObjName = "";
        prop.ParseString("DefaultNavObjName", ref defaultNavObjName);
        defaultNavObjSprite = "";
        prop.ParseString("DefaultNavObjSprite", ref defaultNavObjSprite);
        defaultNavObjText = "";
        prop.ParseString("DefaultNavObjText", ref defaultNavObjText);
        activeNavObjColor = Color.green;
        prop.ParseColor("ActiveNavObjColor", ref activeNavObjColor);
        //inactiveNavObjColor = Color.red;
        //prop.ParseColor("InactiveNavObjColor", ref inactiveNavObjColor);
    }

    [HarmonyPatch(nameof(ItemClass.OnHoldingItemActivated)), MethodTargetPrefix]
    public bool Prefix_OnHoldingItemActivated(ItemInventoryData _data, DetonatorData __customData)
    {
        if (!HasDetonateItem())
        {
            return false;
        }

        _data.holdingEntity.RightArmAnimationUse = true;
        if (!string.IsNullOrEmpty(activationSound))
        {
            Manager.Play(_data.holdingEntity, activationSound);
        }

        if (ConnectionManager.Instance.IsServer)
        {
            __customData.CacheDetonateItemInRange();
            int detonatedCount = 0;
            foreach (EntityItem entityItem in __customData.itemCache)
            {
                if (detonateCount <= 0 || detonatedCount < detonateCount)
                {
                    // items must be alive to be cached, if it's dead then it's damaged by prev explosion, skip it and let death explosion do the job
                    if (entityItem.IsAlive())
                    {
                        entityItem.SetDead();
                        GameManager.Instance.ExplosionServer(0, entityItem.GetPosition(), World.worldToBlockPos(entityItem.GetPosition()), Quaternion.identity, ((ItemClassTimeBomb)entityItem.itemClass).explosion, _data.holdingEntity.entityId, .1f * detonatedCount, false, entityItem.itemStack.itemValue.Clone());
                    }
                    detonatedCount++;
                    if (detonateCount > 0 && detonatedCount >= detonateCount)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            if (detonatedCount > 0 && !_data.holdingEntity.isEntityRemote)
            {
                // detonated count might not match actual deceased count: could be triggered by prev explosion
                //Log.Out($"[Detonator] {detonatedCount} item(s) detonated by entity {_data.holdingEntity.entityId}");
                __customData.CacheDetonateItemInRange();
                //Log.Out($"[Detonator] {__customData.itemCache.Count} item(s) remain in range after detonation");
            }
            if (detonateCount == 0 && !string.IsNullOrEmpty(noTargetSound))
            {
                Manager.BroadcastPlay(_data.holdingEntity, noTargetSound);
            }
        }

        if (!_data.holdingEntity.isEntityRemote)
        {
            GameManager.Instance.SimpleRPC(_data.holdingEntity.entityId, SimpleRPCType.OnActivateItem, false, false);
        }
        return false;
    }

    [HarmonyPatch(nameof(ItemClassExtendedFunction.LateInitItem)), MethodTargetPostfix]
    public void Postfix_LateInitItem()
    {
        if (detonateItems == null || detonateItems.Length == 0)
        {
            return;
        }

        List<int> itemIds = new List<int>();
        foreach (string name in detonateItems)
        {
            var item = ItemClass.GetItemClass(name);
            if (item != null)
            {
                itemIds.Add(item.Id);
            }
        }
        detonateItemIds = itemIds.ToArray();
        detonateItems = Array.ConvertAll(detonateItemIds, (id) => ItemClass.GetForId(id).GetItemName());
    }

    public bool HasDetonateItem()
    {
        return detonateItemIds != null && detonateItems.Length > 0;
    }

    public bool CanUpdateItemCache(ItemInventoryData _data)
    {
        return !_data.holdingEntity.isEntityRemote && updateItemCacheInterval > 0 && HasDetonateItem();
    }

    [HarmonyPatch(nameof(ItemClass.StartHolding)), MethodTargetPostfix]
    public void Postfix_StartHolding(ItemInventoryData _data, DetonatorData __customData)
    {
        if (CanUpdateItemCache(_data))
        {
            __customData.CacheDetonateItemInRange();
            __customData.activeCountHandler = AnimationRiggingManager.GetActiveRigTargetsFromPlayer(_data.holdingEntity)?.ItemCurrentOrDefault?.GetComponentInChildren<IActiveCountHandler>();
        }
    }

    [HarmonyPatch(nameof(ItemClass.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(ItemInventoryData _data, DetonatorData __customData)
    {
        if (CanUpdateItemCache(_data))
        {
            __customData.CacheDetonateItemInRange(false);
        }
    }

    [HarmonyPatch(nameof(ItemClass.OnHoldingUpdate)), MethodTargetPostfix]
    public void Postfix_OnHoldingUpdate(ItemInventoryData _data, DetonatorData __customData)
    {
        if (CanUpdateItemCache(_data) && Time.time - __customData.lastCacheUpdateTime >= updateItemCacheInterval)
        {
            __customData.CacheDetonateItemInRange();
        }
    }

    [HarmonyPatch(nameof(ItemClassExtendedFunction.OnToggleItemActivation)), MethodTargetPostfix]
    public void Postfix_OnToggleItemActivation(ItemInventoryData _data, DetonatorData __customData)
    {
        if (CanUpdateItemCache(_data))
        {
            __customData.CacheDetonateItemInRange();
        }
    }

    public class DetonatorData
    {
        public readonly List<EntityItem> itemCache = new();
        private readonly List<Entity> entityCache = new();
        public readonly static EntityItemLifeComparer comparer = new();
        public float lastCacheUpdateTime = 0;
        public ItemInventoryData invData;
        public ItemModuleDetonator module;
        public IActiveCountHandler activeCountHandler;

        public DetonatorData(ItemInventoryData __instance, ItemModuleDetonator __customModule)
        {
            invData = __instance;
            module = __customModule;
        }

        public void CacheDetonateItemInRange(bool? forceNavObjState = null)
        {
            lastCacheUpdateTime = Time.time;
            // disable prev cached item nav obj
            UpdateNavObjectState(false);
            itemCache.Clear();
            entityCache.Clear();
            //var entityCache = GameManager.Instance.World.GetEntitiesInBounds(null, new Bounds(invData.holdingEntity.boundingBox.center, new Vector3(module.detonateRange, module.detonateRange, module.detonateRange)), true);
            GameManager.Instance.World.GetEntityItemsAroundInWorld(EntityFlags.All, invData.holdingEntity.GetPosition(), module.detonateRange, entityCache);
            for (int i = 0; i < entityCache.Count; i++)
            {
                if (entityCache[i] is EntityItem entityItem && entityItem.OwnerId == invData.holdingEntity.entityId && entityItem.IsAlive() && entityItem.itemClass is ItemClassTimeBomb && module.detonateItemIds.Contains(entityItem.itemClass.Id))
                {
                    itemCache.Add(entityItem);
                }
            }
            itemCache.Sort(comparer);
            // set cur cached item nav obj state
            UpdateNavObjectState(forceNavObjState.HasValue ? forceNavObjState.Value : invData.itemValue.Activated > 0);
            if (activeCountHandler as MonoBehaviour)
            {
                activeCountHandler.SetActiveCount(itemCache.Count);
            }
        }

        public void UpdateNavObjectState(bool on)
        {
            for (int i = 0; i < itemCache.Count; i++)
            {
                EntityItem eItem = itemCache[i];
                if (eItem && eItem.IsAlive())
                {
                    var navObj = eItem.NavObject;
                    // if navobj exists, it's created by item property; otherwise create default one if configured
                    if (navObj == null && !string.IsNullOrEmpty(module.defaultNavObjName))
                    {
                        eItem.AddNavObject(module.defaultNavObjName, module.defaultNavObjSprite, module.defaultNavObjText);
                        //AddNavObject does not set Entity.NavObject, get it from the manager instead
                        navObj = NavObjectManager.Instance.GetNavObjectByEntityID(eItem.entityId);
                        if (navObj != null)
                        {
                            navObj.OwnerEntity = GameManager.Instance.World.GetEntity(eItem.OwnerId);
                        }
                    }
                    if (navObj != null)
                    {
                        //navObj.IsActive = on;
                        if (on && (module.detonateCount <= 0 || i < module.detonateCount))
                        {
                            navObj.UseOverrideColor = true;
                            navObj.OverrideColor = module.activeNavObjColor;
                        }
                        else
                        {
                            navObj.UseOverrideColor = false;
                            //navObj.OverrideColor = module.inactiveNavObjColor;
                        }
                        //Log.Out($"[Detonator] Set nav obj for item {i} entity {eItem.entityId} to {(on ? "active" : "inactive")} with color {navObj.OverrideColor}");
                    }
                }
            }
        }
    }

    // sort age by descending order
    public class EntityItemLifeComparer : IComparer<EntityItem>
    {
        public int Compare(EntityItem x, EntityItem y)
        {
            return CeilToIntWithSign(GetAge(y) - GetAge(x));
        }

        private float GetAge(EntityItem entityItem)
        {
            var item = entityItem.itemClass;
            if (item != null && item.Actions != null && item.Actions[0] is IModuleContainerFor<ActionModuleDynamicDropLifetime> module)
            {
                return module.Instance.lifetime - entityItem.lifetime;
            }
            return 60f - entityItem.lifetime;
        }

        private static int CeilToIntWithSign(float value)
        {
            return value > 0 ? Mathf.CeilToInt(value) : Mathf.FloorToInt(value);
        }
    }
}