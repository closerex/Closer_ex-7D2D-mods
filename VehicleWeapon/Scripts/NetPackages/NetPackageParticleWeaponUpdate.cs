using UnityEngine;

public class NetPackageParticleWeaponUpdate : NetPackage
{
    public NetPackageParticleWeaponUpdate Setup(int entityId, float horRot, float verRot, int seat, int slot)
    {
        this.entityId = entityId;
        this.horEuler = horRot;
        this.verEuler = verRot;
        this.seat = seat;
        this.slot = slot;
        return this;
    }

    public override int GetLength()
    {
        return 40;
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        EntityVehicle entity = _world.GetEntity(entityId) as EntityVehicle;
        if(entity)
        {
            var manager = entity.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
            var player = entity.GetAttached(seat);
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && player)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageParticleWeaponUpdate>().Setup(entityId, horEuler, verEuler, seat, slot), false, -1, player.entityId);
            manager.NetSyncUpdate(seat, slot, horEuler, verEuler);
        }
    }

    public override void read(PooledBinaryReader _reader)
    {
        entityId = _reader.ReadInt32();
        horEuler = _reader.ReadSingle();
        verEuler = _reader.ReadSingle();
        seat = _reader.ReadByte();
        slot = _reader.ReadByte();
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write(entityId);
        _writer.Write(horEuler);
        _writer.Write(verEuler);
        _writer.Write((byte)seat);
        _writer.Write((byte)slot);
    }

    protected int entityId;
    protected float horEuler;
    protected float verEuler;
    protected int seat;
    protected int slot;
}

