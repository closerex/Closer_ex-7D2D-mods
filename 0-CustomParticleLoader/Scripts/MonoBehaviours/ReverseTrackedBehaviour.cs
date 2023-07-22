using System.Collections.Generic;
using UnityEngine;

public class ReverseTrackedBehaviour<T> : TrackedBehaviourBase where T : ReverseTrackedBehaviour<T>
{
    protected static Dictionary<object, Dictionary<uint, T>> hash_instances = new Dictionary<object, Dictionary<uint, T>>();

    static ReverseTrackedBehaviour()
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
        if (!hash_instances.ContainsKey(key))
            hash_instances.Add(key, new Dictionary<uint, T>());
        hash_instances[key].Add(explId, (T)this);
    }
    protected override void removeRef()
    {
        var dict = hash_instances[key];
        if (dict != null)
        {
            dict.Remove(explId);
            if (dict.Count <= 0)
                hash_instances.Remove(key);
        }
    }

    public static bool TryGetValue(uint id, object key, out T controller)
    {
        controller = null;
        if (hash_instances.TryGetValue(key, out var dict))
            return dict.TryGetValue(id, out controller);
        return false;
    }
}

