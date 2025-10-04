namespace KFCommonUtilityLib
{
    public class NetPackageEntityActionIndex : NetPackage
    {
        private int entityID;
        private int mode;
        public NetPackageEntityActionIndex Setup(int entityID, int mode)
        {
            this.entityID = entityID;
            this.mode = mode;
            return this;
        }

        public override int GetLength()
        {
            return 5;
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (MultiActionManager.SetModeForEntity(entityID, mode) && ConnectionManager.Instance.IsServer)
                ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageEntityActionIndex>().Setup(entityID, mode), false, -1, entityID);
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            _writer.Write(entityID);
            _writer.Write((byte)mode);
        }

        public override void read(PooledBinaryReader _reader)
        {
            entityID = _reader.ReadInt32();
            mode = _reader.ReadByte();
        }
    }
}
