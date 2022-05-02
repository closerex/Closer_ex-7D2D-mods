using UnityEngine;

public class NetPackageMyTurretSyncTarget : NetPackageMyTurretSyncUpdate
{
    public override int GetLength()
    {
        return base.GetLength() + 36;
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        if (TurretAiController.TryGetValue(explId, entityId, out var controller) && controller is TurretAiController _controller)
            _controller.NetSyncTarget(position, horRot, verRot, target, rotation);
    }

    public override void read(PooledBinaryReader _reader)
    {
        base.read(_reader);
        horRot = _reader.ReadSingle();
        verRot = _reader.ReadSingle();
        target = _reader.ReadInt32();
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write(horRot);
        _writer.Write(verRot);
        _writer.Write(target);
    }

    public NetPackageMyTurretSyncTarget Setup(uint explId, int entityId, Vector3 position, Quaternion rotation, float horRot, float verRot, int target)
    {
        base.Setup(explId, entityId, position, rotation);
        this.horRot = horRot;
        this.verRot = verRot;
        this.target = target;
        return this;
    }

    private float horRot;
    private float verRot;
    private int target;
}

