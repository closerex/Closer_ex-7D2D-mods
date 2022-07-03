using System.Collections.Generic;
using UnityEngine;

public class NetPackageVehicleWeaponFire : NetPackageVehicleWeaponUpdate
{
    public override int GetLength()
    {
        return base.GetLength() + 3 + fireData.Length;
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
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageVehicleWeaponFire>().Setup(entityId, seat, slot, updateData, count, fireData));
            }
            manager.NetSyncUpdate(seat, slot, updateData);
            manager.DoFireClient(seat, slot, count, fireData);
        }
    }

    public override void read(PooledBinaryReader _reader)
    {
        base.read(_reader);
        count = _reader.ReadByte();
        int length = _reader.ReadUInt16();
        if(length > 0)
            fireData = _reader.ReadBytes(length);
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write((byte)count);
        if (fireData == null || fireData.Length <= 0)
            _writer.Write((ushort)0);
        else
        {
            _writer.Write((ushort)fireData.Length);
            _writer.Write(fireData);
        }
    }

    public NetPackageVehicleWeaponFire Setup(int entityId, int seat, int slot, byte[] updateData, int count, byte[] fireData)
    {
        base.Setup(entityId, seat, slot, updateData);
        this.count = count;
        this.fireData = fireData;
        return this;
    }

    protected int count;
    protected byte[] fireData;
}

