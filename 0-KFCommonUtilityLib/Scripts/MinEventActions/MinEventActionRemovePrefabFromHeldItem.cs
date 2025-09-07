using KFCommonUtilityLib;
using UnityEngine;

public class MinEventActionRemovePrefabFromHeldItem : MinEventActionRemovePrefabFromEntity
{
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        return base.CanExecute(_eventType, _params) && _params.Transform;
    }

    public override void Execute(MinEventParams _params)
    {
        if (!_params.Self)
        {
            return;
        }

        Transform parent = AnimationRiggingManager.GetAddPartTransformOverride(_params.Transform, parent_transform_path, false);
        if (parent)
        {
            Transform child = null;
            string prefabName = "tempPrefab_" + base.prefabName;
            if (_params.Transform.TryGetComponent<AnimationTargetsAbs>(out var targets))
            {
                GameObject prefab = targets.RemovePrefab(prefabName);
                if (prefab)
                {
                    child = prefab.transform;
                }
            }
            if (!child)
            {
                child = parent.Find(prefabName);
            }
            if (child)
            {
                child.parent = null;
                GameObject.Destroy(child.gameObject);
            }
        }
    }
}