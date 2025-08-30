using KFCommonUtilityLib;
using System.Collections.Generic;
using UnityEngine;

public class AnimationParticleSpawner : MonoBehaviour
{
    private Dictionary<string, Transform> dict_path;

    private void Awake()
    {
        dict_path = new Dictionary<string, Transform>();
    }

    public void SpawnParticle(AnimationEvent param)
    {
        if (!dict_path.TryGetValue(param.stringParameter, out var parent))
        {
            parent = transform.FindInAllChildren(param.stringParameter);
            if (!parent)
            {
                return;
            }
            dict_path[param.stringParameter] = parent;
        }

        if (param.objectReferenceParameter is GameObject prefab)
        {
            var particle = Instantiate(prefab, parent, false);
            if (particle)
            {
                particle.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
#if NotEditor
                particle.transform.AddMissingComponent<TemporaryMuzzleFlash>().life = param.floatParameter > 0f ? param.floatParameter : 5f;
#endif
            }
        }
    }
}
