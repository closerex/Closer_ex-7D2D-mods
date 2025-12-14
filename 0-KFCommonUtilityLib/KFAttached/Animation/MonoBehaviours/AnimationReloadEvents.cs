#if NotEditor
using KFCommonUtilityLib;

#endif
using System.Collections;
using UnityEngine;

[AddComponentMenu("KFAttachments/Utils/Animation Reload Events")]
public class AnimationReloadEvents : MonoBehaviour, IPlayableGraphRelated
{
    private void Awake()
    {
        animator = GetComponent<Animator>();
#if NotEditor
        player = GetComponentInParent<EntityAlive>();
#endif
    }

    public void OnReloadFinish()
    {
        OnReloadAmmo();
        OnReloadEnd();
    }

    public void OnReloadAmmo()
    {
#if NotEditor
        if (actionData == null || !actionData.isReloading)
        {
#if DEBUG
            Log.Out($"ANIMATION RELOAD EVENT NOT RELOADING : {actionData?.invData.item.Name ?? "null"}");
#endif
            return;
        }
        if (!actionData.isReloadCancelled)
        {
            player.MinEventContext.ItemActionData = actionData;
            ItemValue item = ItemClass.GetItem(actionRanged.MagazineItemNames[actionData.invData.itemValue.SelectedAmmoTypeIndex], false);
            int magSize = actionRanged.GetMaxAmmoCount(actionData);
            actionData.reloadAmount = GetAmmoCountToReload(player, item, magSize);
            if (actionData.reloadAmount > 0)
            {
                actionData.invData.itemValue.Meta = Utils.FastMin(actionData.invData.itemValue.Meta + actionData.reloadAmount, magSize);
                if (actionData.invData.item.Properties.Values[ItemClass.PropSoundIdle] != null)
                {
                    actionData.invData.holdingEntitySoundID = -1;
                }
            }
#if DEBUG
            Log.Out($"ANIMATION RELOAD EVENT AMMO : {actionData.invData.item.Name}");
#endif
        }
#endif
    }

    public void OnReloadEnd()
    {
        StopAllCoroutines();
        animator.SetWrappedBool(Animator.StringToHash("Reload"), false);
        animator.SetWrappedBool(Animator.StringToHash("IsReloading"), false);
        animator.speed = 1f;
#if NotEditor
        cancelReloadCo = null;
        if (actionData == null || !actionData.isReloading)
        {
            return;
        }
        actionData.isReloading = false;
        actionData.isWeaponReloading = false;
        player.MinEventContext.ItemActionData = actionData;
        player.MinEventContext.ItemInventoryData = actionData.invData;
        player.MinEventContext.ItemValue = actionData.invData.itemValue;
        actionRanged.OnReloadSuccess(actionData);
        player.FireEvent(MinEventTypes.onReloadStop);
        player.OnReloadEnd();
        player.inventory.CallOnToolbeltChangedInternal();
        AnimationAmmoUpdateState.SetAmmoCountForEntity(player, actionData.invData.slotIdx);
        actionData.isReloadCancelled = false;
        actionData.isWeaponReloadCancelled = false;
        actionData.isChangingAmmoType = false;
        if (actionData is IModuleContainerFor<ActionModuleMultiBarrel.MultiBarrelData> dataModule)
        {
            dataModule.Instance.SetCurrentBarrel(actionData.invData.itemValue.Meta);
        }
#if DEBUG
        Log.Out($"ANIMATION RELOAD EVENT FINISHED : {actionData.invData.item.Name}");
#endif
        actionData = null;
#endif
    }

    public void OnPartialReloadEnd()
    {
#if NotEditor
        if (actionData == null)
        {
            return;
        }

        player.MinEventContext.ItemActionData = actionData;
        player.MinEventContext.ItemInventoryData = actionData.invData;
        player.MinEventContext.ItemValue = actionData.invData.itemValue;
        ItemValue ammo = ItemClass.GetItem(actionRanged.MagazineItemNames[actionData.invData.itemValue.SelectedAmmoTypeIndex], false);
        int magSize = (int)EffectManager.GetValue(PassiveEffects.MagazineSize, actionData.invData.itemValue, (float)actionRanged.BulletsPerMagazine, player);
        int partialReloadCount = (int)EffectManager.GetValue(CustomEnums.PartialReloadCount, actionData.invData.itemValue, 1, player);
        actionData.reloadAmount = GetPartialReloadCount(player, ammo, magSize, partialReloadCount);
        actionRanged.OnReloadSuccess(actionData);
        if (actionData.reloadAmount > 0)
        {
            actionData.invData.itemValue.Meta = Utils.FastMin(actionData.invData.itemValue.Meta + actionData.reloadAmount, magSize);
            player.FireEvent(CustomEnums.onPartialReloadAmmoSuccess);
            if (actionData.invData.item.Properties.Values[ItemClass.PropSoundIdle] != null)
            {
                actionData.invData.holdingEntitySoundID = -1;
            }
        }
        else
        {
            player.FireEvent(CustomEnums.onPartialReloadAmmoFail);
        }
        AnimationAmmoUpdateState.SetAmmoCountForEntity(actionData.invData.holdingEntity, actionData.invData.slotIdx);

        if (actionData.isReloadCancelled || actionData.isWeaponReloadCancelled || actionData.invData.itemValue.Meta >= magSize || player.GetItemCount(ammo) <= 0)
        {
            Log.Out("Partial reload finished");
            animator.SetWrappedBool(Animator.StringToHash("IsReloading"), false);
        }
#endif
    }
    public void OnShellEject(int actionIndex)
    {
#if NotEditor
        if (player == null)
        {
            player = GetComponentInParent<EntityAlive>();
        }
        if (!player)
        {
            return;
        }
        if (actionIndex < 0 || actionIndex > player.inventory.holdingItemData.actionData.Count)
        {
            return;
        }
        var shellEjectorData = (player.inventory.holdingItemData.actionData[actionIndex] as IModuleContainerFor<ActionModuleShellEjector.ShellEjectorData>)?.Instance;
        if (shellEjectorData != null)
        {
            shellEjectorData.SpawnShell();
        }
#endif
    }

    public void OnEffectSpawn(int actionIndex)
    {
#if NotEditor
        if (player == null)
        {
            player = GetComponentInParent<EntityAlive>();
        }
        if (!player)
        {
            return;
        }
        if (actionIndex < 0 || actionIndex > player.inventory.holdingItemData.actionData.Count)
        {
            return;
        }
        var shellEjectorData = (player.inventory.holdingItemData.actionData[actionIndex] as IModuleContainerFor<ActionModuleShellEjector.ShellEjectorData>)?.Instance;
        if (shellEjectorData != null)
        {
            shellEjectorData.SpawnEffect();
        }
#endif
    }

    public void OnShellEjectWithOverride(AnimationEvent eventData)
    {
#if NotEditor
        if (player == null)
        {
            player = GetComponentInParent<EntityAlive>();
        }
        if (!player)
        {
            return;
        }
        int actionIndex = eventData.intParameter;
        if (actionIndex < 0 || actionIndex > player.inventory.holdingItemData.actionData.Count)
        {
            return;
        }
        int barrelIndex = (int)eventData.floatParameter;
        var shellEjectorData = (player.inventory.holdingItemData.actionData[actionIndex] as IModuleContainerFor<ActionModuleShellEjector.ShellEjectorData>)?.Instance;
        var multiBarrelData = (player.inventory.holdingItemData.actionData[actionIndex] as IModuleContainerFor<ActionModuleMultiBarrel.MultiBarrelData>)?.Instance;
        if (shellEjectorData != null && multiBarrelData != null && barrelIndex >= 0 && barrelIndex < multiBarrelData.shellEffectJoints.Length)
        {
            var originalShellJoint = shellEjectorData.shellJoint;
            shellEjectorData.shellJoint = multiBarrelData.shellJoints[barrelIndex];
            shellEjectorData.SpawnShell();
            shellEjectorData.shellJoint = originalShellJoint;
        }
#endif
    }

    public void OnEffectSpawnWithOverride(AnimationEvent eventData)
    {
#if NotEditor
        if (player == null)
        {
            player = GetComponentInParent<EntityAlive>();
        }
        if (!player)
        {
            return;
        }
        int actionIndex = eventData.intParameter;
        if (actionIndex < 0 || actionIndex > player.inventory.holdingItemData.actionData.Count)
        {
            return;
        }
        int barrelIndex = (int)eventData.floatParameter;
        var shellEjectorData = (player.inventory.holdingItemData.actionData[actionIndex] as IModuleContainerFor<ActionModuleShellEjector.ShellEjectorData>)?.Instance;
        var multiBarrelData = (player.inventory.holdingItemData.actionData[actionIndex] as IModuleContainerFor<ActionModuleMultiBarrel.MultiBarrelData>)?.Instance;
        if (shellEjectorData != null && multiBarrelData != null && barrelIndex >= 0 && barrelIndex < multiBarrelData.shellEffectJoints.Length)
        {
            var originalShellJoint = shellEjectorData.shellEffectJoint;
            shellEjectorData.shellEffectJoint = multiBarrelData.shellEffectJoints[barrelIndex];
            shellEjectorData.SpawnEffect();
            shellEjectorData.shellEffectJoint = originalShellJoint;
        }
#endif
    }

#if NotEditor
    //public bool ReloadUpdatedThisFrame => reloadUpdatedThisFrame;
    //private bool reloadUpdatedThisFrame = false;
    //internal void OnReloadUpdate()
    //{
    //    reloadUpdatedThisFrame = true;
    //}

    //private void OnAnimatorMove()
    //{
    //    if (actionData != null)
    //    {
    //        //if (actionData.isReloading && !reloadUpdatedThisFrame)
    //        //{
    //        //    Log.Warning("Animator not sending update msg this frame, reloading is cancelled!");
    //        //    actionData.isReloadCancelled = true;
    //        //    OnReloadFinish();
    //        //}
    //    }
    //    else
    //    {
    //        //Log.Warning("actionData is null!");
    //    }
    //    reloadUpdatedThisFrame = false;
    //}

    public void OnReloadStart(int actionIndex)
    {
        if (player == null)
        {
            player = GetComponentInParent<EntityAlive>();
        }
        actionData = player.inventory.holdingItemData.actionData[actionIndex] as ItemActionRanged.ItemActionDataRanged;
        actionRanged = (ItemActionRanged)player.inventory.holdingItem.Actions[actionIndex];
        if (actionData == null || actionData.isReloading)
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

        ItemValue itemValue = actionData.invData.itemValue;
        actionData.invData.holdingEntity.MinEventContext.ItemActionData = actionData;
        int magSize = (int)EffectManager.GetValue(PassiveEffects.MagazineSize, itemValue, actionRanged.BulletsPerMagazine, actionData.invData.holdingEntity);
        ItemActionLauncher itemActionLauncher = actionRanged as ItemActionLauncher;
        if (itemActionLauncher != null && itemValue.Meta < magSize)
        {
            ItemValue ammoValue = ItemClass.GetItem(actionRanged.MagazineItemNames[itemValue.SelectedAmmoTypeIndex], false);
            if (ConsoleCmdReloadLog.LogInfo)
                Log.Out($"loading ammo {ammoValue.ItemClass.Name}");
            ItemActionLauncher.ItemActionDataLauncher itemActionDataLauncher = actionData as ItemActionLauncher.ItemActionDataLauncher;
            if (itemActionDataLauncher.isChangingAmmoType)
            {
                if (ConsoleCmdReloadLog.LogInfo)
                    Log.Out($"is changing ammo type {itemActionDataLauncher.isChangingAmmoType}");
                itemActionLauncher.DeleteProjectiles(actionData);
                itemActionDataLauncher.isChangingAmmoType = false;
            }
            int projectileCount = 1;
            if (!actionData.invData.holdingEntity.isEntityRemote)
            {
                projectileCount = (itemActionLauncher.HasInfiniteAmmo(actionData) ? magSize : GetAmmoCount(actionData.invData.holdingEntity, ammoValue, magSize));
                projectileCount *= getProjectileCount(itemActionDataLauncher);
            }
            int times = 1;
            IModuleContainerFor<ActionModuleMultiBarrel.MultiBarrelData> dataModule = actionData as IModuleContainerFor<ActionModuleMultiBarrel.MultiBarrelData>;
            if (dataModule != null && dataModule.Instance.oneRoundMultishot)
            {
                times = dataModule.Instance.roundsPerShot;
            }
            for (int j = itemActionDataLauncher.projectileTs.Count; j < projectileCount; j++)
            {
                for (int i = 0; i < times; i++)
                {
                    if (dataModule != null)
                    {
                        itemActionDataLauncher.projectileJointT = dataModule.Instance.projectileJoints[i] ?? itemActionDataLauncher.projectileJointT;
                    }
                    itemActionDataLauncher.projectileTs.Add(itemActionLauncher.instantiateProjectile(actionData, Vector3.zero));
                }
            }
        }
        actionData.isReloading = true;
        actionData.isWeaponReloading = true;
        actionData.invData.holdingEntity.FireEvent(MinEventTypes.onReloadStart, true);
#if DEBUG
        Log.Out($"ANIMATION EVENT RELOAD START : {actionData.invData.item.Name}");
#endif
    }

    private Coroutine cancelReloadCo = null;

    public void DelayForceCancelReload(float delay)
    {
        if (cancelReloadCo == null)
            cancelReloadCo = StartCoroutine(ForceCancelReloadCo(delay));
    }

    private void OnDisable()
    {
        if (animator)
        {
            OnReloadEnd();
        }
    }

    private IEnumerator ForceCancelReloadCo(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (actionData != null && (actionData.isReloading || actionData.isWeaponReloading) && (actionData.isReloadCancelled || actionData.isWeaponReloadCancelled))
            OnReloadEnd();
        cancelReloadCo = null;
    }

    public int GetAmmoCountToReload(EntityAlive ea, ItemValue ammo, int modifiedMagazineSize)
    {
        int meta = actionData.invData.itemValue.Meta;
        int target = modifiedMagazineSize - meta;
        if (actionRanged.HasInfiniteAmmo(actionData))
        {
            if (actionRanged.AmmoIsPerMagazine)
            {
                return modifiedMagazineSize;
            }
            return target;
        }

        int res = 0;
        if (ea.bag.GetItemCount(ammo, -1, -1, true) > 0)
        {
            if (actionRanged.AmmoIsPerMagazine)
            {
                return modifiedMagazineSize * ea.bag.DecItem(ammo, 1, false, null);
            }
            res = ea.bag.DecItem(ammo, target, false, null);
            if (res == target)
            {
                return res;
            }
        }

        if (actionRanged.AmmoIsPerMagazine)
        {
            return modifiedMagazineSize * ea.inventory.DecItem(ammo, 1, false, null);
        }

        if (ea.inventory.GetItemCount(ammo, false, -1, -1, true) <= 0)
        {
            return res;
        }
        return res + actionData.invData.holdingEntity.inventory.DecItem(ammo, target - res, false, null);
    }

    public int GetPartialReloadCount(EntityAlive ea, ItemValue ammo, int modifiedMagazineSize, int partialReloadCount)
    {
        int meta = actionData.invData.itemValue.Meta;
        int target = Mathf.Min(partialReloadCount, modifiedMagazineSize - meta);
        if (actionRanged.HasInfiniteAmmo(actionData))
        {
            return target;
        }

        int res = 0;
        if (ea.bag.GetItemCount(ammo) > 0)
        {
            res = ea.bag.DecItem(ammo, target);
            if (res == target)
            {
                return res;
            }
        }

        if (ea.inventory.GetItemCount(ammo) <= 0)
        {
            return res;
        }
        return res + actionData.invData.holdingEntity.inventory.DecItem(ammo, target - res);
    }

    public int GetAmmoCount(EntityAlive ea, ItemValue ammo, int modifiedMagazineSize)
    {
        return Mathf.Min(ea.bag.GetItemCount(ammo, -1, -1, true) + ea.inventory.GetItemCount(ammo, false, -1, -1, true) + actionData.invData.itemValue.Meta, modifiedMagazineSize);
    }

    public int getProjectileCount(ItemActionData _data)
    {
        int rps = 1;
        ItemInventoryData invD = _data != null ? _data.invData : null;
        if (invD != null)
        {
            ItemClass item = invD.itemValue != null ? invD.itemValue.ItemClass : null;
            rps = (int)EffectManager.GetValue(PassiveEffects.RoundRayCount, invD.itemValue, rps, invD.holdingEntity);
        }
        return rps > 0 ? rps : 1;
    }

    public EntityAlive player;
    public ItemActionRanged.ItemActionDataRanged actionData;
    public ItemActionRanged actionRanged;
#endif
    private Animator animator;

    public MonoBehaviour Init(Transform playerAnimatorTrans, bool isLocalPlayer)
    {
        var copy = playerAnimatorTrans.AddMissingComponent<AnimationReloadEvents>();
        if (copy)
        {
            copy.enabled = true;
        }
        return copy;
    }

    public void Disable(Transform playerAnimatorTrans)
    {
        enabled = false;
    }
}
