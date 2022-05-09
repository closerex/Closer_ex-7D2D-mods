class NetPackageSyncWeaponLabelText : NetPackage
{
    public NetPackageSyncWeaponLabelText Setup(int entityId, int slot, string data)
    {
        this.entityId = entityId;
        this.slot = slot;
        this.data = data;
        return this;
    }
    public override int GetLength()
    {
        return 6 + data.Length;
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null)
            return;

        netSyncSetWeaponLabelText(_world.GetEntity(entityId) as EntityAlive, slot, data, true);
    }

    public override void read(PooledBinaryReader _reader)
    {
        entityId = _reader.ReadInt32();
        slot = (int)_reader.ReadChar();
        data = _reader.ReadString();
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);
        _writer.Write(entityId);
        _writer.Write((char)slot);
        _writer.Write(data);
    }

    public static void netSyncSetWeaponLabelText(EntityAlive holdingEntity, int slot, string data, bool fromNet = false)
    {
        if (!holdingEntity || (holdingEntity.isEntityRemote && !fromNet))
        {
            if (holdingEntity)
                Log.Out("netsync failed! isEntityRemote: " + holdingEntity.isEntityRemote + " fromNet: " + fromNet);
            else
                Log.Out("Entity not found!");
            return;
        }

        if(setWeaponLabelText(holdingEntity, slot, data))
        {
            Log.Out("trying to set weapon label on " + (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer ? "server" : "client") + " slot: " + slot + " text: " + data + " entity: " + holdingEntity.entityId);
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
            {
                int allButAttachedToEntityId = holdingEntity.entityId;
                if (holdingEntity.AttachedMainEntity)
                    allButAttachedToEntityId = holdingEntity.AttachedMainEntity.entityId;
                Log.Out("all but attached to entity id: " + allButAttachedToEntityId);
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageSyncWeaponLabelText>().Setup(holdingEntity.entityId, slot, data), false, -1, allButAttachedToEntityId);
            }
            else if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient && !fromNet)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageSyncWeaponLabelText>().Setup(holdingEntity.entityId, slot, data));
        }
    }

    private static bool setWeaponLabelText(EntityAlive holdingEntity, int slot, string data)
    {
        if (GameManager.IsDedicatedServer)
            return true;
        Log.Out("setting weapon label on " + holdingEntity.entityId + " slot: " + slot + " data: " + data);

        WeaponLabelController controller = null;
        if (holdingEntity.emodel.avatarController is AvatarMultiBodyController multiBody && multiBody.HeldItemTransform != null)
            controller = multiBody.HeldItemTransform.GetComponent<WeaponLabelController>();
        else if (holdingEntity.emodel.avatarController is LegacyAvatarController legacy && legacy.HeldItemTransform)
            controller = legacy.HeldItemTransform.GetComponent<WeaponLabelController>();

        if (controller)
            controller.setLabelText(slot, data);
        else
            return false;

        return true;
    }

    private int entityId;
    private int slot;
    private string data;
}

