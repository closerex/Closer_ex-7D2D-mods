using UnityEngine;

public class NetPackageParticleWeaponFire : NetPackageParticleWeaponUpdate
{
    public override int GetLength()
    {
        return base.GetLength() + 5;
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        EntityVehicle entity = _world.GetEntity(entityId) as EntityVehicle;
        var manager = entity?.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
        if (manager != null)
        {
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageParticleWeaponFire>().Setup(entityId, horEuler, verEuler, seat, slot, count, seed));
            }
            manager.NetSyncUpdate(seat, slot, horEuler, verEuler);
            manager.DoParticleFireClient(seat, slot, count, seed);
        }
    }

    public override void read(PooledBinaryReader _reader)
    {
        base.read(_reader);
        count = _reader.ReadByte();
        seed = _reader.ReadUInt32();
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write((byte)count);
        _writer.Write(seed);
    }

    public NetPackageParticleWeaponFire Setup(int entityId, float horRot, float verRot, int seat, int slot, int count, uint seed)
    {
        base.Setup(entityId, horRot, verRot, seat, slot);
        this.count = count;
        this.seed = seed;
        return this;
    }

    protected int count;
    protected uint seed;
}

