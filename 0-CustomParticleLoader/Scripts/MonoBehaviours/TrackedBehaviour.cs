using System.Collections.Generic;
using UnityEngine;

public class TrackedBehaviour<T> : TrackedBehaviourBase where T : TrackedBehaviour<T>
{
    protected static Dictionary<uint, Dictionary<object, T>> hash_instances = new Dictionary<uint, Dictionary<object, T>>();

    static TrackedBehaviour()
    {
        CustomExplosionManager.CleanUp += hash_instances.Clear;
    }

    protected override void Awake()
    {
        track = true;
        base.Awake();
    }
    protected override void addRef()
    {
        if (!hash_instances.ContainsKey(explId))
            hash_instances.Add(explId, new Dictionary<object, T>());
        hash_instances[explId].Add(key, (T)this);
    }

    protected override void removeRef()
    {
        var dict = hash_instances[explId];
        if (dict != null)
        {
            dict.Remove(key);
            if (dict.Count <= 0)
                hash_instances.Remove(explId);
        }
    }

    public static bool TryGetValue(uint id, object key, out T controller)
    {
        controller = null;
        if (hash_instances.TryGetValue(id, out var dict))
            return dict.TryGetValue(key, out controller);
        return false;
    }
}