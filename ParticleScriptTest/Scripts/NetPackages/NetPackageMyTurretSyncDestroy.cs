using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class NetPackageMyTurretSyncDestroy : NetPackage
{
    public override int GetLength()
    {
        return 9;
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        if (TurretAiController.TryGetValue(explId, entityId, out var controller) && controller is TurretAiController _controller)
            _controller.NetSyncDestroy();
    }

    public override void read(PooledBinaryReader _reader)
    {
        explId = _reader.ReadUInt32();
        entityId = _reader.ReadInt32();
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write(explId);
        _writer.Write(entityId);
    }

    public NetPackageMyTurretSyncDestroy Setup(uint explId, int entityId)
    {
        this.explId = explId;
        this.entityId = entityId;
        return this;
    }

    protected uint explId;
    protected int entityId;
}

