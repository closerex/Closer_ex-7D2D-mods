using System.Collections;
using UnityEngine;

public class ItemActionHoldOpen : ItemActionRanged
{
    private const string emptyAnimatorBool = "empty";
    //private EntityAlive lastHoldingEntity = null;
    //private HashSet<EntityAlive> hashset_dirty = new HashSet<EntityAlive>();
    //private bool reloadReset = false;

    public Animator getAnimator(EntityAlive holdingEntity)
    {
        Animator animator = null;
        //should not use ?. here because when you use something from bag ui entry, the holding item is destroyed but still referenced in the avatar controller
        //and ?. will try to access that reference instead of return null and throw NRE, while != in unity is override to return null in such case
        if (holdingEntity.emodel.avatarController is AvatarMultiBodyController multiBody && multiBody.HeldItemAnimator != null)
            animator = multiBody.HeldItemAnimator;
        else if (holdingEntity.emodel.avatarController is LegacyAvatarController legacy && legacy.HeldItemTransform != null)
            animator = legacy.HeldItemTransform.GetComponent<Animator>();
        return animator;
    }

    public void setAnimatorBool(EntityAlive holdingEntity, string parameter, bool flag)
    {
        holdingEntity.emodel.avatarController.UpdateBool(parameter, flag, false);
        //Animator animator = getAnimator(holdingEntity);
        //if (animator)
        //{
        //    animator.SetBool(parameter, flag);
        //    //Log.Out("trying to set param: " + parameter + " flag: " + flag + " result: " + getAnimatorBool(holdingEntity, parameter) + " transform: " + animator.transform.name);
        //}
    }

    public void setAnimatorFloat(EntityAlive holdingEntity, string parameter, float value)
    {
        holdingEntity.emodel.avatarController.UpdateFloat(parameter, value, false);
        //Animator animator = getAnimator(holdingEntity);
        //if (animator)
        //{
        //    animator.SetFloat(parameter, value);
        //    //Log.Out("trying to set param: " + parameter + " flag: " + flag + " result: " + getAnimatorBool(holdingEntity, parameter) + " transform: " + animator.transform.name);
        //}
    }

    public override int getUserData(ItemActionData _actionData)
    {
        return _actionData.invData.itemValue.Meta <= 0 ? 1 : 0;
    }

    public override void ItemActionEffects(GameManager _gameManager, ItemActionData _actionData, int _firingState, Vector3 _startPos, Vector3 _direction, int _userData = 0)
    {
        base.ItemActionEffects(_gameManager, _actionData, _firingState, _startPos, _direction, _userData);

        if (_firingState != (int)ItemActionFiringState.Off && (_userData & 1) > 0)
            setAnimatorBool(_actionData.invData.holdingEntity, emptyAnimatorBool, true);
    }

    public override void ReloadGun(ItemActionData _actionData)
    {
        base.ReloadGun(_actionData);
        //delay 2 frames before reloading, since the animation is likely to be triggered the next frame this is called
        ThreadManager.StartCoroutine(DelaySetEmpty(_actionData, false, 2));
        //reloadReset = true;
    }

    public override void StartHolding(ItemActionData _data)
    {
        //reloadReset = false;
        //if(_data.invData.itemValue.Meta <= 0)
        //    hashset_dirty.Add(_data.invData.holdingEntity);
        //lastHoldingEntity = _data.invData.holdingEntity;
        //lastHoldingEntity.inventory.OnToolbeltItemsChangedInternal += OnStartHolding;
        //delay 1 frame before equipping weapon
        if (_data.invData.itemValue.Meta <= 0)
            ThreadManager.StartCoroutine(DelaySetEmpty(_data, true, 1));
        base.StartHolding(_data);
    }

    //protected virtual void OnStartHolding()
    //{
    //    if(lastHoldingEntity.inventory.holdingItemItemValue.Meta <= 0)
    //        setAnimatorBool(lastHoldingEntity, emptyAnimatorBool, true);
    //    //hashset_dirty.Add(lastHoldingEntity);
    //    //Log.Out("Entity " + lastHoldingEntity.entityId + " start holding " + lastHoldingEntity.inventory.holdingItemItemValue.ItemClass.Name + " meta: " + lastHoldingEntity.inventory.holdingItemItemValue.Meta);
    //    lastHoldingEntity.inventory.OnToolbeltItemsChangedInternal -= OnStartHolding;
    //    lastHoldingEntity = null;
    //}

    public override void ConsumeAmmo(ItemActionData _actionData)
    {
        base.ConsumeAmmo(_actionData);
        if (_actionData.invData.itemValue.Meta == 0)
            _actionData.invData.holdingEntity.FireEvent(CustomEnums.onSelfMagzineDeplete, true);
    }

    public override void SwapAmmoType(EntityAlive _entity, int _ammoItemId = -1)
    {
        setAnimatorBool(_entity, emptyAnimatorBool, true);
        base.SwapAmmoType(_entity, _ammoItemId);
        /*
        ItemActionDataRanged _action = _entity.inventory.holdingItemData.actionData[0] as ItemActionDataRanged;
        Log.Out("is reloading: " + _action.isReloading + " item: " + _action.invData.itemValue.ItemClass.Name + " meta: " + _action.invData.itemValue.Meta + " holding item: " + _entity.inventory.holdingItemItemValue.ItemClass.Name + " holding meta: " + _entity.inventory.holdingItemItemValue.Meta);
        if (_action != null && !_action.isReloading && _action.invData.itemValue.Meta <= 0)
        */
    }

    private IEnumerator DelaySetEmpty(ItemActionData _actionData, bool empty, int delay)
    {
        for (int i = 0; i < delay; i++)
        {
            yield return null;
        }
        if (_actionData.invData.holdingEntity.inventory.holdingItemIdx == _actionData.invData.slotIdx)
        {
            setAnimatorBool(_actionData.invData.holdingEntity, emptyAnimatorBool, empty);
        }
        yield break;
    }

    //public override void OnHoldingUpdate(ItemActionData _actionData)
    //{
    //    base.OnHoldingUpdate(_actionData);

    //    if (GameManager.IsDedicatedServer)
    //        return;

    //    if (reloadReset)
    //    {
    //        setAnimatorBool(GameManager.Instance.World.GetPrimaryPlayer(), emptyAnimatorBool, false);
    //        reloadReset = false;
    //    }

    //    if (hashset_dirty.Count <= 0)
    //        return;

    //    foreach (EntityAlive holdingEntity in hashset_dirty)
    //        setAnimatorBool(holdingEntity, emptyAnimatorBool, true);
    //    hashset_dirty.Clear();
    //    /*
    //    EntityAlive holdingEntity = _actionData.invData.holdingEntity;
    //    if (hash_dirty.Count <= 0 || !hash_dirty.ContainsKey(holdingEntity))
    //        return;

    //    if (hash_dirty[holdingEntity])
    //        hash_dirty[holdingEntity] = false;
    //    else
    //    {
    //        hash_dirty.Remove(holdingEntity);
    //        setAnimatorBool(holdingEntity, emptyAnimatorBool, true);
    //    }
    //    */
    //    /*
    //    EntityAlive holdingEntity = _actionData.invData.holdingEntity;
    //    bool isReloading = (_actionData as ItemActionDataRanged).isReloading;
    //    if (holdingEntity.isEntityRemote && !isReloading)
    //        return;
    //    int meta = _actionData.invData.itemValue.Meta;
    //    if (!isReloading && meta <= 0 && !getAnimatorBool(holdingEntity, emptyAnimatorBool))
    //    {
    //        Log.Out("trying to update param: " + emptyAnimatorBool + " flag: " + true);
    //        setAnimatorBool(holdingEntity, emptyAnimatorBool, true);
    //    }
    //    else if ((isReloading || meta > 0) && getAnimatorBool(holdingEntity, emptyAnimatorBool))
    //    {
    //        Log.Out("trying to update param: " + emptyAnimatorBool + " flag: " + false);
    //        setAnimatorBool(holdingEntity, emptyAnimatorBool, false);
    //    }
    //    */
    //}

}

