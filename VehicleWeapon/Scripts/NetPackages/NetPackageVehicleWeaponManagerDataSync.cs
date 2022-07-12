using System;
using System.IO;

public class NetPackageVehicleWeaponManagerDataSync : NetPackage
{
    public NetPackageVehicleWeaponManagerDataSync Setup(int entityId, int seat, byte[] updateData, byte[] fireData)
    {
        this.entityId = entityId;
        this.seat = seat;
        this.updateData = updateData;
        this.fireData = fireData;
        return this;
    }

    public override int GetLength()
    {
        return 10 + (updateData != null ? updateData.Length : 0) + (fireData != null ? fireData.Length : 0);
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        EntityVehicle entity = _world.GetEntity(entityId) as EntityVehicle;
        if (entity)
        {
            var manager = entity.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
            var player = entity.GetAttached(seat);
            if (ConnectionManager.Instance.IsServer && player)
                ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageVehicleWeaponManagerDataSync>().Setup(entityId, seat, updateData, fireData), false, -1, player.entityId, entityId, 75);
            
            using (PooledBinaryReader _br = MemoryPools.poolBinaryReader.AllocSync(true))
            {
                if(updateData != null)
                {
                    using (MemoryStream ms = new MemoryStream(updateData))
                    {
                        _br.SetBaseStream(ms);
                        manager.NetSyncUpdate(seat, _br);
                    }
                }

                if(fireData != null)
                {
                    using (MemoryStream ms = new MemoryStream(fireData))
                    {
                        _br.SetBaseStream(ms);
                        manager.NetSyncFire(seat, _br);
                    }
                }
            }
        }
    }

    public override void read(PooledBinaryReader _reader)
    {
        entityId = _reader.ReadInt32();
        seat = _reader.ReadByte();

        int updateDataLength = _reader.ReadUInt16();
        if (updateDataLength > 0)
            updateData = _reader.ReadBytes(updateDataLength);

        int fireDataLength = _reader.ReadUInt16();
        if (fireDataLength > 0)
            fireData = _reader.ReadBytes(fireDataLength);
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write(entityId);
        _writer.Write((byte)seat);

        if (updateData == null || updateData.Length == 0)
            _writer.Write((ushort)0);
        else
        {
            _writer.Write((ushort)updateData.Length);
            _writer.Write(updateData);
        }

        if (fireData == null || fireData.Length == 0)
            _writer.Write((ushort)0);
        else
        {
            _writer.Write((ushort)fireData.Length);
             _writer.Write(fireData);
        }
    }

    private int entityId;
    private int seat;
    private byte[] updateData;
    private byte[] fireData;
}

