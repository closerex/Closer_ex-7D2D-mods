using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class NetPackageVehicleSwitchSeat : NetPackage
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public override int GetLength()
    {
        return 10;
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        _callbacks.TrySwitchSeatServer(_world, entityId, vehicleId, seat);
    }

    public override void read(PooledBinaryReader _reader)
    {
        entityId = _reader.ReadInt32();
        vehicleId = _reader.ReadInt32();
        seat = _reader.ReadByte();
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write(entityId);
        _writer.Write(vehicleId);
        _writer.Write((byte)seat);
    }

    public NetPackageVehicleSwitchSeat Setup(int entityId, int vehicleId, int seat)
    {
        this.entityId = entityId;
        this.vehicleId = vehicleId;
        this.seat = seat;
        return this;
    }

    private int entityId;
    private int vehicleId;
    private int seat;
}

