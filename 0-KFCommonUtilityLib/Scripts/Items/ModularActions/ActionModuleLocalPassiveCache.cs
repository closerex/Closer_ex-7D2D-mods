using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib;
using System;
using System.Collections;
using System.Collections.Generic;

[TypeTarget(typeof(ItemAction)), TypeDataTarget(typeof(LocalPassiveCacheData))]
public class ActionModuleLocalPassiveCache
{
    public class LocalPassiveCacheData : IEnumerable<int>
    {
        public ItemInventoryData invData;
        private Dictionary<int, float> dict_hash_value = new Dictionary<int, float>();
        private Dictionary<int, string> dict_hash_name = new Dictionary<int, string>();

        public LocalPassiveCacheData(ItemInventoryData _inventoryData)
        {
            this.invData = _inventoryData;
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
