using KFCommonUtilityLib.Scripts.StaticManagers;
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
            Transform child = parent.Find("tempPrefab_" + prefabName);
            if (child)
            {
                child.parent = null;
                if (child.TryGetComponent<AttachmentReferenceAppended>(out var reference))
                {
                    reference.Remove();
                }
                GameObject.Destroy(child.gameObject);
            }
        }
    }
}