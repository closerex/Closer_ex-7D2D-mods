using UnityEngine;

namespace KFCommonUtilityLib
{
    public class NetPackageRemoteAttachPrefab : NetPackage
    {
        private int entityID;
        private string prefab;
        private string path;
        private Vector3 localPosition;
        private Vector3 localRotation;
        private Vector3 localScale;

        public NetPackageRemoteAttachPrefab Setup(int entityID, string prefab, string path, Vector3 localPosition, Vector3 localRotation, Vector3 localScale)
        {
            this.entityID = entityID;
            this.prefab = prefab;
            this.path = path;
            this.localPosition = localPosition;
            this.localRotation = localRotation;
            this.localScale = localScale;
            return this;
        }

        public override int GetLength()
        {
            return 200;
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null || _callbacks == null)
                return;
            var entity = _world.GetEntity(entityID) as EntityAlive;
            if (!entity || !entity.isEntityRemote)
                return;
            if (ConnectionManager.Instance.IsServer)
                ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRemoteAttachPrefab>().Setup(entityID, prefab, path, localPosition, localRotation, localScale), false, -1, -1, entityID);
            MinEventActionAttachPrefabToEntitySync.RemoteAttachPrefab(entity, prefab, path, localPosition, localRotation, localScale);
        }

        public override void read(PooledBinaryReader _reader)
        {
            entityID = _reader.ReadInt32();
            prefab = _reader.ReadString();
            path = _reader.ReadString();
            localPosition = StreamUtils.ReadVector3(_reader);
            localRotation = StreamUtils.ReadVector3(_reader);
            localScale = StreamUtils.ReadVector3(_reader);
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            _writer.Write(entityID);
            _writer.Write(prefab);
            _writer.Write(path);
            StreamUtils.Write(_writer, localPosition);
            StreamUtils.Write(_writer, localRotation);
            StreamUtils.Write(_writer, localScale);
        }
    }
}
