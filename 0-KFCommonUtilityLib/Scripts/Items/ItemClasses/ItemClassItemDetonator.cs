using KFCommonUtilityLib;
using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

public class ItemClassItemDetonator : ItemClass, ILateInitItem
{
    public string[] detonateItems;
    public int[] detonateItemIds;
    public string activationSound;
    public float detonateRange;
    public int detonateCount;
    public float updateItemCacheInterval;

    public override void Init()
    {
        base.Init();
        if (Properties.Contains("DetonateItems"))
        {
            detonateItems = Properties.GetString("DetonateItems").Split(',', StringSplitOptions.RemoveEmptyEntries);
        }
        detonateRange = 50;
        Properties.ParseFloat("DetonateRange", ref detonateRange);
        detonateCount = 0;
        Properties.ParseInt("DetonateCount", ref detonateCount);
        updateItemCacheInterval = 0;
        Properties.ParseFloat("UpdateItemCacheInterval", ref updateItemCacheInterval);
        if (Properties.Contains("ActivationSound"))
        {
            activationSound = Properties.GetString("ActivationSound");
        }
    }

    public override void OnHoldingItemActivated(ItemInventoryData _data)
    {
        if (!HasDetonateItem() || _data is not DetonatorInvData invData)
        {
            return;
        }

        _data.holdingEntity.RightArmAnimationUse = true;

        if (ConnectionManager.Instance.IsServer)
        {
            invData.CacheDetonateItemInRange();
            int detonatedCount = 0;
            foreach (EntityItem entityItem in invData.itemCache)
            {
                if (detonateCount <= 0 || detonatedCount < detonateCount)
                {
                    GameManager.Instance.ExplosionServer(0, entityItem.GetPosition(), World.worldToBlockPos(entityItem.GetPosition()), Quaternion.identity, ((ItemClassTimeBomb)entityItem.itemClass).explosion, _data.holdingEntity.entityId, 0, false, entityItem.itemStack.itemValue.Clone());
                    entityItem.SetDead();
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
            if (detonatedCount > 0)
            {
                invData.itemCache.RemoveRange(0, detonatedCount);
                invData.UpdateCachedItemCount();
            }
        }

        if (!_data.holdingEntity.isEntityRemote)
        {
            GameManager.Instance.SimpleRPC(_data.holdingEntity.entityId, SimpleRPCType.OnActivateItem, false, false);
        }
    }

    public void LateInitItem()
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

    public override void StartHolding(ItemInventoryData _data, Transform _modelTransform)
    {
        base.StartHolding(_data, _modelTransform);
        if (!_data.holdingEntity.isEntityRemote && updateItemCacheInterval > 0 && HasDetonateItem() && _data is DetonatorInvData invData)
        {
            invData.CacheDetonateItemInRange();
            invData.UpdateCachedItemCount();
        }
    }

    public override ItemInventoryData createItemInventoryData(ItemStack _itemStack, IGameManager _gameManager, EntityAlive _holdingEntity, int _slotIdxInInventory)
    {
        return new DetonatorInvData(this, _itemStack, _gameManager, _holdingEntity, _slotIdxInInventory);
    }

    public override void OnHoldingUpdate(ItemInventoryData _data)
    {
        base.OnHoldingUpdate(_data);
        if (!_data.holdingEntity.isEntityRemote && updateItemCacheInterval > 0 && HasDetonateItem() && _data is DetonatorInvData invData && Time.time - updateItemCacheInterval >= invData.lastCacheUpdateTime)
        {
            invData.CacheDetonateItemInRange();
            invData.UpdateCachedItemCount();
        }
    }

    public class DetonatorInvData : ItemInventoryData
    {
        public readonly List<Entity> entityCache = new();
        public readonly List<EntityItem> itemCache = new();
        public readonly static GameManager.EntityItemLifetimeComparer comparer = new();
        public float lastCacheUpdateTime = 0;
        public DetonatorInvData(ItemClass _item, ItemStack _itemStack, IGameManager _gameManager, EntityAlive _holdingEntity, int _slotIdx) : base(_item, _itemStack, _gameManager, _holdingEntity, _slotIdx)
        {

        }

        public void CacheDetonateItemInRange()
        {
            lastCacheUpdateTime = Time.time;
            entityCache.Clear();
            itemCache.Clear();
            if (item is not ItemClassItemDetonator detonatorClass)
            {
                return;
            }
            GameManager.Instance.World.GetEntitiesAround(EntityFlags.All, holdingEntity.GetPosition(), detonatorClass.detonateRange, entityCache);
            for (int i = entityCache.Count - 1; i >= 0; i++)
            {
                if (entityCache[i] is EntityItem entityItem && !entityItem.IsDead() && entityItem.OwnerId == holdingEntity.entityId && entityItem.itemClass is ItemClassTimeBomb && detonatorClass.detonateItemIds.Contains(entityItem.itemClass.Id))
                {
                    itemCache.Add(entityItem);
                }
            }
            itemCache.Sort(comparer);
        }

        public void UpdateCachedItemCount()
        {

        }
    }
}
