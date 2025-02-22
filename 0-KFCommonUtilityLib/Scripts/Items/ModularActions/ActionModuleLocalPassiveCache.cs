using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using System.Collections;
using System.Collections.Generic;

[TypeTarget(typeof(ItemAction)), ActionDataTarget(typeof(LocalPassiveCacheData))]
public class ActionModuleLocalPassiveCache
{
    //public int[] nameHashes;

    //[MethodTargetPostfix(nameof(ItemAction.ReadFrom))]
    //private void Postfix_ReadFrom(DynamicProperties _props)
    //{
    //    string str = _props.Values["CachePassives"];
    //    if (!string.IsNullOrEmpty(str))
    //    {
    //        nameHashes = Array.ConvertAll(str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), s => s.GetHashCode());
    //    }
    //}

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

    public class LocalPassiveCacheData : IEnumerable<int>
    {
        //public float[] cache;
        //public bool[] markedForCache;
        //public ActionModuleLocalPassiveCache _cacheModule;
        public ItemInventoryData invData;
        private Dictionary<int, float> dict_hash_value = new Dictionary<int, float>();
        private Dictionary<int, string> dict_hash_name = new Dictionary<int, string>();

        public LocalPassiveCacheData(ItemInventoryData _invData, int _indexOfAction, ActionModuleLocalPassiveCache _cacheModule)
        {
            //this._cacheModule = _cacheModule;
            this.invData = _invData;
            //if (_cacheModule.nameHashes != null)
            //{
            //    cache = new float[_cacheModule.nameHashes.Length];
            //    //markedForCache = new bool[_cacheModule.nameHashes.Length];
            //}
        }

        public void CachePassive(PassiveEffects target, int targetHash, string targetStr, FastTags<TagGroup.Global> tags)
        {
            if (invData.holdingEntity.isEntityRemote)
                return;
            if (!dict_hash_name.ContainsKey(targetHash))
                dict_hash_name[targetHash] = targetStr;

            dict_hash_value[targetHash] = EffectManager.GetValue(target, invData.itemValue, 0, invData.holdingEntity, null, tags);
            //markedForCache[index] = true;
        }

        public float GetCachedValue(int targetHash)
        {
            return dict_hash_value.TryGetValue(targetHash, out float res) ? res : 0;
        }

        public string GetCachedName(int targetHash)
        {
            return dict_hash_name.TryGetValue(targetHash, out string res) ? res : string.Empty;
        }

        public IEnumerator<int> GetEnumerator()
        {
            return dict_hash_value.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
