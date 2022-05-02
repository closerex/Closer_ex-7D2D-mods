using UnityEngine;

public class NetPackageMyTurretSyncUpdate : NetPackageMyTurretSyncDestroy
{
    public override int GetLength()
    {
        return 26;
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        if (TurretAiController.TryGetValue(explId, entityId, out var controller) && controller is TurretAiController _controller)
            _controller.NetSyncUpdate(position, rotation);
    }

    public override void read(PooledBinaryReader _reader)
    {
        position = StreamUtilsCompressed.ReadHalfVector3(_reader) - Origin.position;
        rotation = StreamUtilsCompressed.ReadHalfQuaternion(_reader);
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        StreamUtilsCompressed.Write(_writer, position);
        StreamUtilsCompressed.Write(_writer, rotation);
    }

    public NetPackageMyTurretSyncUpdate Setup(uint explId, int entityId, Vector3 position, Quaternion rotation)
    {
        base.Setup(explId, entityId);
        this.position = position;
        this.rotation = rotation;
        return this;
    }

    protected Vector3 position;
    protected Quaternion rotation;
}

