using Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Scripting;
using UnityEngine;
using FullautoLauncher.Scripts.ProjectileManager;
using static ItemActionLauncher;
using static ItemActionRanged;

[Preserve]
public class ItemActionBetterLauncher : ItemActionRanged
{
    private IProjectileItemGroup group;
    public override ItemActionData CreateModifierData(ItemInventoryData _invData, int _indexInEntityOfAction)
    {
        return new ItemActionBetterLauncher.ItemActionDataBetterLauncher(_invData, _indexInEntityOfAction);
    }

    public override void ReadFrom(DynamicProperties _props)
    {
        base.ReadFrom(_props);
    }

    public override void StartHolding(ItemActionData _actionData)
    {
        base.StartHolding(_actionData);
        ItemActionBetterLauncher.ItemActionDataBetterLauncher ItemActionDataBetterLauncher = (ItemActionBetterLauncher.ItemActionDataBetterLauncher)_actionData;
        ItemValue holdingItemItemValue = _actionData.invData.holdingEntity.inventory.holdingItemItemValue;
        ItemClass forId = ItemClass.GetForId(ItemClass.GetItem(MagazineItemNames[(int)holdingItemItemValue.SelectedAmmoTypeIndex], false).type);
        group = CustomProjectileManager.Get(forId.Name);
        if (_actionData.invData.itemValue.Meta != 0 && GetMaxAmmoCount(ItemActionDataBetterLauncher) != 0)
        {
            group.Pool(_actionData.invData.itemValue.Meta * (int)EffectManager.GetValue(PassiveEffects.RoundRayCount, ItemActionDataBetterLauncher.invData.itemValue, 1f, ItemActionDataBetterLauncher.invData.holdingEntity, null, default(FastTags), true, true, true, true, 1, true, false));
        }
        ItemActionDataBetterLauncher.info = new ProjectileParams.ItemInfo()
        {
            actionData = ItemActionDataBetterLauncher,
            itemProjectile = forId,
            itemActionProjectile = (ItemActionProjectile)((forId.Actions[0] is ItemActionProjectile) ? forId.Actions[0] : forId.Actions[1]),
            itemValueLauncher = holdingItemItemValue,
            itemValueProjectile = new ItemValue(forId.Id)
        };
    }

    public override void StopHolding(ItemActionData _data)
    {
        base.StopHolding(_data);
        ItemActionDataBetterLauncher itemActionDataLauncher = (ItemActionDataBetterLauncher)_data;
        itemActionDataLauncher.info = null;
    }

    public override void SwapAmmoType(EntityAlive _entity, int _ammoItemId = -1)
    {
        base.SwapAmmoType(_entity, _ammoItemId);
        ItemActionDataBetterLauncher ItemActionDataBetterLauncher = (ItemActionDataBetterLauncher)_entity.inventory.holdingItemData.actionData[0];
        ItemValue itemValue = ItemActionDataBetterLauncher.invData.itemValue;
        ItemClass forId = ItemClass.GetForId(ItemClass.GetItem(MagazineItemNames[(int)itemValue.SelectedAmmoTypeIndex], false).type);
        group = CustomProjectileManager.Get(forId.Name);
        ItemActionDataBetterLauncher.info = new ProjectileParams.ItemInfo()
        {
            actionData = ItemActionDataBetterLauncher,
            itemProjectile = forId,
            itemActionProjectile = (ItemActionProjectile)((forId.Actions[0] is ItemActionProjectile) ? forId.Actions[0] : forId.Actions[1]),
            itemValueLauncher = itemValue,
            itemValueProjectile = new ItemValue(forId.Id)
        };
    }

    protected override Vector3 fireShot(int _shotIdx, ItemActionRanged.ItemActionDataRanged _actionData, ref bool hitEntity)
    {
        hitEntity = true;
        return Vector3.zero;
    }

    public override void ItemActionEffects(GameManager _gameManager, ItemActionData _actionData, int _firingState, Vector3 _startPos, Vector3 _direction, int _userData = 0)
    {
        base.ItemActionEffects(_gameManager, _actionData, _firingState, _startPos, _direction, _userData);
        if (_firingState == 0)
        {
            return;
        }
        EntityAlive entity = _actionData.invData.holdingEntity;
        if (entity.isEntityRemote && GameManager.IsDedicatedServer)
        {
            return;
        }
        ItemActionBetterLauncher.ItemActionDataBetterLauncher ItemActionDataBetterLauncher = (ItemActionBetterLauncher.ItemActionDataBetterLauncher)_actionData;
        ItemValue holdingItemItemValue = _actionData.invData.holdingEntity.inventory.holdingItemItemValue;
        ItemClass forId = ItemClass.GetForId(ItemClass.GetItem(MagazineItemNames[(int)holdingItemItemValue.SelectedAmmoTypeIndex], false).type);
        int projCount = (int)EffectManager.GetValue(PassiveEffects.RoundRayCount, ItemActionDataBetterLauncher.invData.itemValue, 1f, ItemActionDataBetterLauncher.invData.holdingEntity, null, default(FastTags), true, true, true, true, 1, true, false); ;
        if (projCount <= 0)
        {
            return;
        }
        if (ItemActionDataBetterLauncher.info == null)
        {
            Log.Error("null info!");
            return;
        }
        Vector3 realStartPosition = ItemActionDataBetterLauncher.projectileJoint.position + Origin.position;
        for (int i = 0; i < projCount; i++)
        {
            var par = group.Fire(entity.entityId, ItemActionDataBetterLauncher.info, _startPos, realStartPosition, getDirectionOffset(ItemActionDataBetterLauncher, _direction, i), entity, hitmaskOverride);
        }
    }

    protected override void getImageActionEffectsStartPosAndDirection(ItemActionData _actionData, out Vector3 _startPos, out Vector3 _direction)
    {
        Ray lookRay = _actionData.invData.holdingEntity.GetLookRay();
        _startPos = lookRay.origin;
        _direction = lookRay.direction;//getDirectionOffset(ItemActionDataBetterLauncher, lookRay.direction, 0);
    }

    public class ItemActionDataBetterLauncher : ItemActionRanged.ItemActionDataRanged
    {
        public ItemActionDataBetterLauncher(ItemInventoryData _invData, int _indexInEntityOfAction)
            : base(_invData, _indexInEntityOfAction)
        {
            this.projectileJoint = ((_invData.model != null) ? _invData.model.FindInChilds("ProjectileJoint", false) : null);
        }

        public Transform projectileJoint;
        public ProjectileParams.ItemInfo info;
    }

}
