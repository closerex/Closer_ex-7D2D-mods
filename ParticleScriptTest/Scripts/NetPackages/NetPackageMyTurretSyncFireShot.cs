using UnityEngine;

public class NetPackageMyTurretSyncFireShot : NetPackageMyTurretSyncUpdate
{
    public override int GetLength()
    {
        return base.GetLength() + 34;
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        if (TurretAiController.TryGetValue(explId, entityId, out var controller))
            controller.NetSyncFireShot(position, horRot, verRot, ammoleft, rotation);
    }

    public override void read(PooledBinaryReader _reader)
    {
        base.read(_reader);
        horRot = _reader.ReadSingle();
        verRot = _reader.ReadSingle();
        ammoleft = (int)_reader.ReadUInt16();
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write(horRot);
        _writer.Write(verRot);
        _writer.Write((ushort)ammoleft);
    }

    public NetPackageMyTurretSyncFireShot Setup(uint explId, int entityId, Vector3 position, Quaternion rotation, float horRot, float verRot, int ammoleft)
    {
        base.Setup(explId, entityId, position, rotation);
        this.horRot = horRot;
        this.verRot = verRot;
        this.ammoleft = ammoleft;
        return this;
    }

    private float horRot;
    private float verRot;
    private int ammoleft;
}

