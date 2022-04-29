using System.Collections.Generic;
using UnityEngine;

public class ReverseTrackedBehaviour<T> : TrackedBehaviourBase
{
    protected static Dictionary<object, Dictionary<uint, MonoBehaviour>> hash_instances = new Dictionary<object, Dictionary<uint, MonoBehaviour>>();

    protected override void Awake()
    {
        track = true;
        base.Awake();
    }
    protected override void addRef()
    {
        if (!hash_instances.ContainsKey(key))
            hash_instances.Add(key, new Dictionary<uint, MonoBehaviour>());
        hash_instances[key].Add(explId, this);
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

    public static bool TryGetValue(uint id, object key, out MonoBehaviour controller)
    {
        controller = null;
        if (hash_instances.TryGetValue(key, out var dict))
            return dict.TryGetValue(id, out controller);
        return false;
    }
}

