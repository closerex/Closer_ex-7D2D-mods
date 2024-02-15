using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System;

[TypeTarget(typeof(ItemAction), typeof(LocalPassiveCacheData))]
public class ActionModuleLocalPassiveCache
{
    public PassiveEffects[] passives;

    [MethodTargetPostfix(nameof(ItemAction.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        string str = _props.Values["CachePassives"];
        if (!string.IsNullOrEmpty(str))
        {
            passives = Array.ConvertAll(str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), s => CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>(s));
        }
    }

    [MethodTargetPrefix(nameof(ItemAction.StartHolding))]
    private bool Prefix_StartHolding(ItemActionData _data, LocalPassiveCacheData __customData)
    {
        if (passives != null)
        {
            for (int i = 0; i < passives.Length; i++)
            {
                __customData.passives[i] = EffectManager.GetValue(passives[i], _data.invData.itemValue, 0, _data.invData.holdingEntity);
                __customData.markedForCache[i] = false;
            }
        }
        return true;
    }

    [MethodTargetPrefix(nameof(ItemAction.OnHoldingUpdate))]
    private bool Prefix_OnHoldingUpdate(ItemActionData _actionData, LocalPassiveCacheData __customData)
    {
        if (!_actionData.invData.holdingEntity.isEntityRemote && passives != null)
        {
            for (int i = 0; i < passives.Length; i++)
            {
                if (__customData.markedForCache[i])
                {
                    __customData.passives[i] = EffectManager.GetValue(passives[i], _actionData.invData.itemValue, 0, _actionData.invData.holdingEntity);
                    __customData.markedForCache[i] = false;
                }
            }
        }

        return true;
    }

    public class LocalPassiveCacheData
    {
        public float[] passives;
        public bool[] markedForCache;
        public ActionModuleLocalPassiveCache _cacheModule;

        public LocalPassiveCacheData(ItemInventoryData _invData, int _indexOfAction, ActionModuleLocalPassiveCache _cacheModule)
        {
            this._cacheModule = _cacheModule;
            if (_cacheModule.passives != null)
            {
                passives = new float[_cacheModule.passives.Length];
                markedForCache = new bool[_cacheModule.passives.Length];
            }
        }

        public void MarkForCache(PassiveEffects target)
        {
            if (_cacheModule.passives == null)
                return;
            int index = Array.IndexOf(_cacheModule.passives, target);
            if (index < 0)
                return;
            markedForCache[index] = true;
        }

        public float GetCachedValue(PassiveEffects target)
        {
            if (_cacheModule.passives == null)
                return 0;
            int index = Array.IndexOf(_cacheModule.passives, target);
            if (index < 0)
                return 0;
            return passives[index];
        }
    }
}
