using UnityEngine;

public class NetPackageHornWeaponFire : NetPackageHornWeaponUpdate
{
    public override int GetLength()
    {
        return base.GetLength();
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        EntityVehicle entity = _world.GetEntity(entityId) as EntityVehicle;
        var horn = entity?.GetVehicle().FindPart("hornWeapon") as VPHornWeapon;
        if (horn != null)
        {
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageHornWeaponFire>().Setup(entityId, horEuler, verEuler, count, seed));
            }
            horn.NetSyncUpdate(horEuler, verEuler);
            horn.DoHornClient(count, seed);
        }
    }

    public override void read(PooledBinaryReader _reader)
    {
        base.read(_reader);
        count = _reader.ReadChar();
        seed = _reader.ReadUInt32();
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write((char)count);
        _writer.Write(seed);
    }

    public NetPackageHornWeaponFire Setup(int entityId, float horRot, float verRot, int count, uint seed)
    {
        base.Setup(entityId, horRot, verRot);
        this.count = count;
        this.seed = seed;
        return this;
    }

    protected int count;
    protected uint seed;
}

