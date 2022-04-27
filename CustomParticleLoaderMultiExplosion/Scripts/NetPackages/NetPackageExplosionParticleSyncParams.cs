/*
using System.Collections.Generic;

public class NetPackageExplosionParticleSyncParams : NetPackage
{
    public NetPackageExplosionParticleSyncParams Setup(uint explId, int name, List<ParticleSyncController.ExplosionParticleSyncParams> list)
    {
        this.explId = explId;
        this.name = name;
        if (list != null)
            list_sync = list;
        else
            list_sync = new List<ParticleSyncController.ExplosionParticleSyncParams>();

        return this;
    }
    public override int GetLength()
    {
        return 14 + list_sync.Count * 28;
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        if(ParticleSyncController.TryGetValue(explId, name, out var controller) && controller is ParticleSyncController _controller)
            _controller.EmitClient(list_sync);
    }

    public override void read(PooledBinaryReader _reader)
    {
        explId = _reader.ReadUInt32();
        name = _reader.ReadInt32();
        int count = (int)_reader.ReadUInt16();
        list_sync = new List<ParticleSyncController.ExplosionParticleSyncParams>(count);
        for (int i = 0; i < count; ++i)
            list_sync.Add(ParticleSyncController.ExplosionParticleSyncParams.Create(_reader));
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write(explId);
        _writer.Write(name);
        _writer.Write((ushort)list_sync.Count);
        foreach (var param in list_sync)
            param.write(_writer);
    }
    public override NetPackageDirection PackageDirection
    {
        get
        {
            return NetPackageDirection.ToClient;
        }
    }

    private uint explId;
    private int name;
    private List<ParticleSyncController.ExplosionParticleSyncParams> list_sync;
}
*/
