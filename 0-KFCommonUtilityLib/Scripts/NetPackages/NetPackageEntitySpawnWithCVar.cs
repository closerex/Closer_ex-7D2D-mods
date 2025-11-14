namespace KFCommonUtilityLib
{
    public class NetPackageEntitySpawnWithCVar : NetPackageEntitySpawn
    {
        byte[] cvarData;
        public NetPackageEntitySpawnWithCVar Setup(EntityCreationData _es, EntityAlive _ea)
        {
            Setup(_es);
            using (var bw = MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                using (var ms = MemoryPools.poolMemoryStream.AllocSync(true))
                {
                    bw.SetBaseStream(ms);
                    if (_ea && _ea.Buffs != null)
                    {
                        var buff = _ea.Buffs;
                        bw.Write(buff.CVars.Count);
                        foreach (var cvar in buff.CVars)
                        {
                            bw.Write(cvar.Key);
                            bw.Write(cvar.Value);
                        }
                    }
                    else
                        bw.Write(0);
                    cvarData = ms.ToArray();
                }
                return this;
            }
        }

        public override int GetLength()
        {
            return base.GetLength() + 200;
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            base.ProcessPackage(_world, _callbacks);
            if (_world == null || _callbacks == null || es.id == -1)
                return;
            EntityAlive ea = _world.GetEntity(es.id) as EntityAlive;
            if (!ea)
                return;
            using (var ms = MemoryPools.poolMemoryStream.AllocSync(true))
            {
                ms.Write(cvarData, 0, cvarData.Length);
                ms.Position = 0;
                using (var br = MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    br.SetBaseStream(ms);
                    var count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                        ea.Buffs.SetCustomVar(br.ReadString(), br.ReadSingle(), false);
                }
            }
            ea.FireEvent(CustomEnums.onSelfFirstCVarSync);
        }

        public override void read(PooledBinaryReader _reader)
        {
            base.read(_reader);
            cvarData = new byte[_reader.ReadInt32()];
            _reader.Read(cvarData, 0, cvarData.Length);
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            _writer.Write(cvarData.Length);
            _writer.Write(cvarData);
        }
    }
}
