using System.Collections.Generic;
using UnityEngine;

public class NetPackageVehicleWeaponUpdate : NetPackage
{
    public NetPackageVehicleWeaponUpdate Setup(int entityId, int seat, int slot, byte[] updateData)
    {
        this.entityId = entityId;
        this.seat = seat;
        this.slot = slot;
        this.updateData = updateData;
        return this;
    }

    public override int GetLength()
    {
        return 10 + updateData.Length;
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
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageVehicleWeaponUpdate>().Setup(entityId, seat, slot, updateData), false, -1, player.entityId, entityId, 75);
            manager.NetSyncUpdate(seat, slot, updateData);
        }
    }

    public override void read(PooledBinaryReader _reader)
    {
        entityId = _reader.ReadInt32();
        seat = _reader.ReadByte();
        slot = _reader.ReadByte();
        int userDataCount = _reader.ReadUInt16();
        if (userDataCount > 0)
            updateData = _reader.ReadBytes(userDataCount);
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write(entityId);
        _writer.Write((byte)seat);
        _writer.Write((byte)slot);
        if (updateData == null || updateData.Length <= 0)
            _writer.Write((ushort)0);
        else
        {
            _writer.Write((ushort)updateData.Length);
            _writer.Write(updateData);
        }
    }

    protected int entityId;
    protected int seat;
    protected int slot;
    protected byte[] updateData;
}

