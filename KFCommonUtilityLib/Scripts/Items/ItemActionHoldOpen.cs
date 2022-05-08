using UnityEngine;

class ItemActionHoldOpen : ItemActionRanged
{
    private const string emptyAnimatorBool = "empty";

    public void setAnimatorBool(EntityAlive holdingEntity, string parameter, bool flag)
    {
        Transform trans = (holdingEntity.emodel.avatarController as AvatarMultiBodyController)?.HeldItemTransform;
        if (trans && trans.TryGetComponent<Animator>(out Animator animator))
        {
            animator.SetBool(parameter, flag);
            Log.Out("trying to set param: " + parameter + " flag: " + flag + " result: " + getAnimatorBool(holdingEntity, parameter) + " transform: " + animator.transform.name);
        }
    }

    public bool getAnimatorBool(EntityAlive holdingEntity, string parameter)
    {
        Transform trans = (holdingEntity.emodel.avatarController as AvatarMultiBodyController)?.HeldItemTransform;
        if (trans && trans.TryGetComponent<Animator>(out Animator animator))
            return animator.GetBool(parameter);
        else
            return false;
    }

    protected override int getUserData(ItemActionData _actionData)
    {
        return _actionData.invData.itemValue.Meta;
    }

    public override void ItemActionEffects(GameManager _gameManager, ItemActionData _actionData, int _firingState, Vector3 _startPos, Vector3 _direction, int _userData = 0)
    {
        if(_firingState != (int)ItemActionFiringState.Off && _userData <= 0)
            setAnimatorBool(_actionData.invData.holdingEntity, emptyAnimatorBool, true);

        base.ItemActionEffects(_gameManager, _actionData, _firingState, _startPos, _direction, _userData);
    }

    public override void ReloadGun(ItemActionData _actionData)
    {
        setAnimatorBool(_actionData.invData.holdingEntity, emptyAnimatorBool, false);
        base.ReloadGun(_actionData);
    }
    /*
    public override void OnHoldingUpdate(ItemActionData _actionData)
    {
        base.OnHoldingUpdate(_actionData);

        if (GameManager.IsDedicatedServer)
            return;

        EntityAlive holdingEntity = _actionData.invData.holdingEntity;
        bool isReloading = (_actionData as ItemActionDataRanged).isReloading;
        if (holdingEntity.isEntityRemote && !isReloading)
            return;
        int meta = _actionData.invData.itemValue.Meta;
        if (!isReloading && meta <= 0 && !getAnimatorBool(holdingEntity, emptyAnimatorBool))
        {
            Log.Out("trying to update param: " + emptyAnimatorBool + " flag: " + true);
            setAnimatorBool(holdingEntity, emptyAnimatorBool, true);
        }
        else if ((isReloading || meta > 0) && getAnimatorBool(holdingEntity, emptyAnimatorBool))
        {
            Log.Out("trying to update param: " + emptyAnimatorBool + " flag: " + false);
            setAnimatorBool(holdingEntity, emptyAnimatorBool, false);
        }
    }
    */
}

