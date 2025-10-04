using KFCommonUtilityLib.Scripts.Utilities;

namespace KFCommonUtilityLib
{
    class NetPackageFixedReload : NetPackage
    {
        private int entityId;
        private byte actionIndex;

        public NetPackageFixedReload Setup(int entityId, int actionIndex)
        {
            this.entityId = entityId;
            this.actionIndex = (byte)actionIndex;
            return this;
        }

        public override int GetLength()
        {
            return 5;
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null)
                return;

            if (!_world.IsRemote())
                MultiActionUtils.FixedItemReloadServer(entityId, actionIndex);
            else
                MultiActionUtils.FixedItemReloadClient(entityId, actionIndex);
        }

        public override void read(PooledBinaryReader _reader)
        {
            entityId = _reader.ReadInt32();
            actionIndex = _reader.ReadByte();
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            _writer.Write(entityId);
            _writer.Write(actionIndex);
        }
    }
}