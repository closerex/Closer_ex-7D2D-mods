using KFCommonUtilityLib;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

public class MinEventActionAttachPrefabToEntitySync : MinEventActionAttachPrefabToEntity
{
    private static Dictionary<string, GameObject> dict_loaded = new Dictionary<string, GameObject>();
    //public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    //{
    //    return base.CanExecute(_eventType, _params) && (_params.IsLocal || (_params.Self && !_params.Self.isEntityRemote));
    //}

    public override void Execute(MinEventParams _params)
    {
        base.Execute(_params);
        if (ConnectionManager.Instance.IsServer)
        {
            ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRemoteAttachPrefab>().Setup(_params.Self.entityId, prefab, parent_transform_path, local_offset, local_rotation, local_scale), false, -1, -1, _params.Self.entityId);
        }
        else if (_params.IsLocal || (_params.Self && !_params.Self.isEntityRemote))
        {
            ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRemoteAttachPrefab>().Setup(_params.Self.entityId, prefab, parent_transform_path, local_offset, local_rotation, local_scale));
        }
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        bool flag = false;
        if (_attribute.Name.LocalName == "prefab")
        {
            prefab = _attribute.Value;
            if (dict_loaded.TryGetValue(_attribute.Value, out GameObject go) && go)
            {
                goToInstantiate = go;
                flag = true;
            }
            else
            {
                flag = base.ParseXmlAttribute(_attribute);
                dict_loaded[_attribute.Value] = goToInstantiate;
            }
        }
        else
        {
            flag = base.ParseXmlAttribute(_attribute);
        }
        return flag;
    }

    public static void RemoteAttachPrefab(EntityAlive entity, string prefab, string path, Vector3 local_offset, Vector3 local_rotation, Vector3 local_scale)
    {
        Transform transform = entity.RootTransform;
        if (!string.IsNullOrEmpty(path))
        {
            transform = GameUtils.FindDeepChildActive(transform, path);
        }
        if (transform == null)
        {
            return;
        }
        GameObject goToInstantiate = dict_loaded[prefab];
        string text = "tempPrefab_" + goToInstantiate.name;
        Transform transform2 = GameUtils.FindDeepChild(transform, text);
        if (transform2 == null)
        {
            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(goToInstantiate);
            if (gameObject == null)
            {
                return;
            }
            transform2 = gameObject.transform;
            gameObject.name = text;
            Utils.SetLayerRecursively(gameObject, transform.gameObject.layer, null);
            transform2.parent = transform;
            transform2.localPosition = local_offset;
            transform2.localRotation = Quaternion.Euler(local_rotation.x, local_rotation.y, local_rotation.z);
            transform2.localScale = local_scale;
        }
    }
}
