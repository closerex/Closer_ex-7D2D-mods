using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System;

[TypeTarget(typeof(ItemAction), typeof(LocalPassiveCacheData))]
public class ActionModuleLocalPassiveCache
{
    public int[] nameHashes;

    [MethodTargetPostfix(nameof(ItemAction.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        string str = _props.Values["CachePassives"];
        if (!string.IsNullOrEmpty(str))
        {
            nameHashes = Array.ConvertAll(str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), s => s.GetHashCode());
        }
    }

    //[MethodTargetPrefix(nameof(ItemAction.StartHolding))]
    //private bool Prefix_StartHolding(ItemActionData _data, LocalPassiveCacheData __customData)
    //{
    //    if (nameHashes != null)
    //    {
    //        for (int i = 0; i < nameHashes.Length; i++)
    //        {
    //            __customData.passives[i] = EffectManager.GetValue(nameHashes[i], _data.invData.itemValue, 0, _data.invData.holdingEntity);
    //            __customData.markedForCache[i] = false;
    //        }
    //    }
    //    return true;
    //}

    //[MethodTargetPrefix(nameof(ItemAction.OnHoldingUpdate))]
    //private bool Prefix_OnHoldingUpdate(ItemActionData _actionData, LocalPassiveCacheData __customData)
    //{
    //    if (!_actionData.invData.holdingEntity.isEntityRemote && nameHashes != null)
    //    {
    //        for (int i = 0; i < nameHashes.Length; i++)
    //        {
    //            if (__customData.markedForCache[i])
    //            {
    //                __customData.cache[i] = EffectManager.GetValue(nameHashes[i], _actionData.invData.itemValue, 0, _actionData.invData.holdingEntity);
    //                __customData.markedForCache[i] = false;
    //            }
    //        }
    //    }

    //    return true;
    //}

    public class LocalPassiveCacheData
    {
        public float[] cache;
        //public bool[] markedForCache;
        public ActionModuleLocalPassiveCache _cacheModule;
        public ItemInventoryData invData;

        public LocalPassiveCacheData(ItemInventoryData _invData, int _indexOfAction, ActionModuleLocalPassiveCache _cacheModule)
        {
            this._cacheModule = _cacheModule;
            this.invData = _invData;
            if (_cacheModule.nameHashes != null)
            {
                cache = new float[_cacheModule.nameHashes.Length];
                //markedForCache = new bool[_cacheModule.nameHashes.Length];
            }
        }

        public void CachePassive(PassiveEffects target, int targetHash, FastTags tags)
        {
            if (_cacheModule.nameHashes == null || invData.holdingEntity.isEntityRemote)
                return;
            int index = Array.IndexOf(_cacheModule.nameHashes, targetHash);
            if (index < 0)
                return;

            cache[index] = EffectManager.GetValue(target, invData.itemValue, 0, invData.holdingEntity, null, tags);
            //markedForCache[index] = true;
        }

        public float GetCachedValue(int targetHash)
        {
            if (_cacheModule.nameHashes == null)
                return 0;
            int index = Array.IndexOf(_cacheModule.nameHashes, targetHash);
            if (index < 0)
                return 0;
            return cache[index];
        }
    }
}
