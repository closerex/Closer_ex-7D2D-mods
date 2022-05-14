using UnityEngine;

public class NetPackageHornWeaponUpdate : NetPackage
{
    public NetPackageHornWeaponUpdate Setup(int entityId, float horRot, float verRot)
    {
        this.entityId = entityId;
        this.horEuler = horRot;
        this.verEuler = verRot;
        return this;
    }

    public override int GetLength()
    {
        return 38;
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        EntityVehicle entity = _world.GetEntity(entityId) as EntityVehicle;
        if(entity)
        {
            var horn = entity.GetVehicle().FindPart("hornWeapon") as VPHornWeapon;
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageHornWeaponUpdate>().Setup(entityId, horEuler, verEuler), false, -1, entity.AttachedMainEntity.entityId);
            horn.NetSyncUpdate(horEuler, verEuler);
        }
    }

    public override void read(PooledBinaryReader _reader)
    {
        entityId = _reader.ReadInt32();
        horEuler = _reader.ReadSingle();
        verEuler = _reader.ReadSingle();
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write(entityId);
        _writer.Write(horEuler);
        _writer.Write(verEuler);
    }

    protected int entityId;
    protected float horEuler;
    protected float verEuler;
}

