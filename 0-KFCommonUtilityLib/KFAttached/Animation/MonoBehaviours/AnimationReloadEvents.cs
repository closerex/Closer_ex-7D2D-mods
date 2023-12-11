using KFCommonUtilityLib.Scripts.Utilities;
using UnityEngine;

[AddComponentMenu("KFAttachments/Utils/Animation Reload Events")]
public class AnimationReloadEvents : MonoBehaviour
{
    private void Awake()
    {
        animator = GetComponent<Animator>();
#if NotEditor
        player = GetComponentInParent<EntityPlayerLocal>();
#endif
    }

    public void OnReloadFinish()
    {
#if NotEditor
        //animator.speed = 1f;
        animator.SetBool("Reload", false);
        if (actionData == null)
        {
            return;
        }
        if (!actionData.isReloading)
        {
#if DEBUG
            Log.Out($"ANIMATION RELOAD EVENT NOT RELOADING : {actionData.invData.item.Name}");
#endif
            return;
        }
        if (!actionData.isReloadCancelled)
        {
            EntityAlive holdingEntity = actionData.invData.holdingEntity;
            ItemValue item = ItemClass.GetItem(actionRanged.MagazineItemNames[MultiActionUtils.MultiActionGetSelectedAmmoTypeIndex(actionData)], false);
            int num = (int)EffectManager.GetValue(PassiveEffects.MagazineSize, actionData.invData.itemValue, (float)actionRanged.BulletsPerMagazine, holdingEntity, null, MultiActionUtils.GetItemTagsWithActionIndex(actionData), true, true, true, true, 1, true, false);
            actionData.reloadAmount = GetAmmoCountToReload(holdingEntity, item, num);
            if (actionData.reloadAmount > 0)
            {
                MultiActionUtils.MultiActionReload(actionData);
                if (actionData.invData.item.Properties.Values[ItemClass.PropSoundIdle] != null)
                {
                    actionData.invData.holdingEntitySoundID = -1;
                }
            }
#if DEBUG
            Log.Out($"ANIMATION RELOAD EVENT FINISHING : {actionData.invData.item.Name}");
#endif
        }
        actionData.isReloading = false;
        actionData.invData.holdingEntity.MinEventContext.ItemActionData = actionData;
        actionData.invData.holdingEntity.FireEvent(MinEventTypes.onReloadStop, true);
        actionData.invData.holdingEntity.OnReloadEnd();
        actionData.invData.holdingEntity.inventory.CallOnToolbeltChangedInternal();
        actionData.isReloadCancelled = false;
        actionData.isChangingAmmoType = false;
#if DEBUG
        Log.Out($"ANIMATION RELOAD EVENT FINISHED : {actionData.invData.item.Name}");
#endif
#endif
        }

#if NotEditor
    public bool ReloadUpdatedThisFrame => reloadUpdatedThisFrame;
    private bool reloadUpdatedThisFrame = false;
    internal void OnReloadUpdate()
    {
        reloadUpdatedThisFrame = true;
    }

    private void OnAnimatorMove()
    {
        if (actionData != null)
        {
            if (actionData.isReloading && !reloadUpdatedThisFrame)
            {
                Log.Warning("Animator not sending update msg this frame, reloading is cancelled!");
                actionData.isReloadCancelled = true;
                OnReloadFinish();
            }
        }
        else
        {
            //Log.Warning("actionData is null!");
        }
        reloadUpdatedThisFrame = false;
    }

    public void OnReloadStart(int actionIndex)
    {
        if (player == null)
        {
            return;
        }
        actionData = player.inventory.holdingItemData.actionData[actionIndex] as ItemActionRanged.ItemActionDataRanged;
        actionRanged = (ItemActionRanged)player.inventory.holdingItem.Actions[actionIndex];
        if (actionData == null)
        {
            return;
        }
        if (actionData.isReloading)
        {
            return;
        }
        if (actionData.invData.item.Properties.Values[ItemClass.PropSoundIdle] != null && actionData.invData.holdingEntitySoundID >= 0)
        {
            Audio.Manager.Stop(actionData.invData.holdingEntity.entityId, actionData.invData.item.Properties.Values[ItemClass.PropSoundIdle]);
        }
        actionData.wasAiming = actionData.invData.holdingEntity.AimingGun;
        if (actionData.invData.holdingEntity.AimingGun && actionData.invData.item.Actions[1] is ItemActionZoom)
        {
            actionData.invData.holdingEntity.inventory.Execute(1, false, null);
            actionData.invData.holdingEntity.inventory.Execute(1, true, null);
        }
        if (animator.GetCurrentAnimatorClipInfo(0).Length != 0 && animator.GetCurrentAnimatorClipInfo(0)[0].clip.events.Length == 0)
        {
            if (actionRanged.SoundReload != null)
            {
                player.PlayOneShot(actionRanged.SoundReload.Value, false);
            }
        }
        else if (animator.GetNextAnimatorClipInfo(0).Length != 0 && animator.GetNextAnimatorClipInfo(0)[0].clip.events.Length == 0 && actionRanged.SoundReload != null)
        {
            player.PlayOneShot(actionRanged.SoundReload.Value, false);
        }
        int magSize = (int)EffectManager.GetValue(PassiveEffects.MagazineSize, actionData.invData.itemValue, (float)actionRanged.BulletsPerMagazine, actionData.invData.holdingEntity, null, MultiActionUtils.GetItemTagsWithActionIndex(actionData), true, true, true, true, 1, true, false);
        ItemActionLauncher itemActionLauncher = actionRanged as ItemActionLauncher;
        if (itemActionLauncher != null && MultiActionUtils.MultiActionGetMeta(actionData) < magSize)
        {
            ItemValue itemValue = actionData.invData.itemValue;
            ItemValue item = ItemClass.GetItem(actionRanged.MagazineItemNames[MultiActionUtils.MultiActionGetSelectedAmmoTypeIndex(actionData)], false);
            ItemActionLauncher.ItemActionDataLauncher itemActionDataLauncher = actionData as ItemActionLauncher.ItemActionDataLauncher;
            if (itemActionDataLauncher.isChangingAmmoType)
            {
                itemActionLauncher.DeleteProjectiles(actionData);
                itemActionDataLauncher.isChangingAmmoType = false;
            }
            int projectileCount = 1;
            if (!actionData.invData.holdingEntity.isEntityRemote)
            {
                projectileCount = (itemActionLauncher.HasInfiniteAmmo(actionData) ? magSize : GetAmmoCount(actionData.invData.holdingEntity, item, magSize));
                projectileCount *= getProjectileCount(itemActionDataLauncher);
            }
            for (int i = itemActionDataLauncher.projectileInstance.Count; i < projectileCount; i++)
            {
                itemActionDataLauncher.projectileInstance.Add(itemActionLauncher.instantiateProjectile(actionData, new Vector3(0f, 0f, 0f)));
            }
        }
        actionData.isReloading = true;
        actionData.invData.holdingEntity.MinEventContext.ItemActionData = actionData;
        actionData.invData.holdingEntity.FireEvent(MinEventTypes.onReloadStart, true);
#if DEBUG
        Log.Out($"ANIMATION EVENT RELOAD START : {actionData.invData.item.Name}");
#endif
    }

    private int GetAmmoCountToReload(EntityAlive ea, ItemValue ammo, int modifiedMagazineSize)
    {
        int meta = MultiActionUtils.MultiActionGetMeta(actionData);
        if (actionRanged.HasInfiniteAmmo(actionData))
        {
            if (actionRanged.AmmoIsPerMagazine)
            {
                return modifiedMagazineSize;
            }
            return modifiedMagazineSize - meta;
        }
        else if (ea.bag.GetItemCount(ammo, -1, -1, true) > 0)
        {
            if (actionRanged.AmmoIsPerMagazine)
            {
                return modifiedMagazineSize * ea.bag.DecItem(ammo, 1, false, null);
            }
            return ea.bag.DecItem(ammo, modifiedMagazineSize - meta, false, null);
        }
        else
        {
            if (ea.inventory.GetItemCount(ammo, false, -1, -1, true) <= 0)
            {
                return 0;
            }
            if (actionRanged.AmmoIsPerMagazine)
            {
                return modifiedMagazineSize * ea.inventory.DecItem(ammo, 1, false, null);
            }
            return actionData.invData.holdingEntity.inventory.DecItem(ammo, modifiedMagazineSize - meta, false, null);
        }
    }

    private int GetAmmoCount(EntityAlive ea, ItemValue ammo, int modifiedMagazineSize)
    {
        return Mathf.Min(ea.bag.GetItemCount(ammo, -1, -1, true) + ea.inventory.GetItemCount(ammo, false, -1, -1, true), modifiedMagazineSize);
    }

    private int getProjectileCount(ItemActionData _data)
    {
        int rps = 1;
        ItemInventoryData invD = _data != null ? _data.invData : null;
        if (invD != null)
        {
            ItemClass item = invD.itemValue != null ? invD.itemValue.ItemClass : null;
            rps = (int)EffectManager.GetValue(PassiveEffects.RoundRayCount, invD.itemValue, rps, invD.holdingEntity, null, item != null ? MultiActionUtils.GetItemTagsWithActionIndex(_data) : default(FastTags));
        }
        return rps > 0 ? rps : 1;
    }

    private EntityPlayerLocal player;
    private ItemActionRanged.ItemActionDataRanged actionData;
    private ItemActionRanged actionRanged;
#endif
    private Animator animator;
    }
