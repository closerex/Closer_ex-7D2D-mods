using System.Collections.Generic;
using UnityEngine;

public class NetPackageWeaponRotatorUpdate : NetPackage
{
    public NetPackageWeaponRotatorUpdate Setup(int entityId, float horRot, float verRot, int seat, int slot, IEnumerable<int> userData)
    {
        this.entityId = entityId;
        this.horEuler = horRot;
        this.verEuler = verRot;
        this.seat = seat;
        this.slot = slot;
        if(userData != null)
            this.userDataWrite = new List<int>(userData);
        return this;
    }

    public override int GetLength()
    {
        return 41 + userDataWrite.Count * 4;
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
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageWeaponRotatorUpdate>().Setup(entityId, horEuler, verEuler, seat, slot, userDataRead), false, -1, player.entityId);
            manager.NetSyncUpdate(seat, slot, horEuler, verEuler, userDataRead);
        }
    }

    public override void read(PooledBinaryReader _reader)
    {
        entityId = _reader.ReadInt32();
        horEuler = _reader.ReadSingle();
        verEuler = _reader.ReadSingle();
        seat = _reader.ReadByte();
        slot = _reader.ReadByte();
        int userDataCount = _reader.ReadByte();
        if(userDataCount > 0)
        {
            userDataRead = new Stack<int>(userDataCount);
            for(int i = 0; i < userDataCount; i++)
                userDataRead.Push(_reader.ReadInt32());
        }
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write(entityId);
        _writer.Write(horEuler);
        _writer.Write(verEuler);
        _writer.Write((byte)seat);
        _writer.Write((byte)slot);
        if (userDataWrite == null || userDataWrite.Count <= 0)
            _writer.Write((byte)0);
        else
        {
            _writer.Write((byte)userDataWrite.Count);
            for (int i = userDataWrite.Count - 1; i >= 0; --i)
                _writer.Write(userDataWrite[i]);
        }
    }

    protected int entityId;
    protected float horEuler;
    protected float verEuler;
    protected int seat;
    protected int slot;
    protected Stack<int> userDataRead;
    protected List<int> userDataWrite;
}

