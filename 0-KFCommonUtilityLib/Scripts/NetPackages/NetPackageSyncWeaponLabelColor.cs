using UnityEngine;

namespace KFCommonUtilityLib
{
    class NetPackageSyncWeaponLabelColor : NetPackage
    {
        public NetPackageSyncWeaponLabelColor Setup(int entityId, bool isText, Color color, int index0, int index1, int nameId)
        {
            this.entityId = entityId;
            this.isText = isText;
            this.color = color;
            this.index0 = index0;
            if (!isText)
            {
                this.index1 = index1;
                this.nameId = nameId;
            }
            return this;
        }

        public override int GetLength()
        {
            return isText ? 20 : 28;
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null)
                return;

            NetSyncSetWeaponLabelColor(_world.GetEntity(entityId) as EntityAlive, isText, index0, color, index1, nameId, true);
        }

        public static void NetSyncSetWeaponLabelColor(EntityAlive holdingEntity, bool isText, int slot0, Color color, int slot1, int nameId, bool fromNet = false)
        {
            if (!holdingEntity || holdingEntity.isEntityRemote && !fromNet)
            {
                if (holdingEntity)
                    Log.Out("netsync failed! isEntityRemote: " + holdingEntity.isEntityRemote + " fromNet: " + fromNet);
                else
                    Log.Out("Entity not found!");
                return;
            }

            if (SetWeaponLabelColor(holdingEntity, isText, slot0, color, slot1, nameId))
            {
                //Log.Out("trying to set weapon label on " + (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer ? "server" : "client") + " color: " + color.ToString() + " entity: " + holdingEntity.entityId + " from net: " + fromNet);
                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
                {
                    int allButAttachedToEntityId = holdingEntity.entityId;
                    if (holdingEntity && holdingEntity.AttachedMainEntity)
                        allButAttachedToEntityId = holdingEntity.AttachedMainEntity.entityId;
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageSyncWeaponLabelColor>().Setup(holdingEntity.entityId, isText, color, slot0, slot1, nameId), false, -1, allButAttachedToEntityId, allButAttachedToEntityId, null, 15);
                }
                else if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient && !fromNet)
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageSyncWeaponLabelColor>().Setup(holdingEntity.entityId, isText, color, slot0, slot1, nameId));
            }
        }

        public static bool SetWeaponLabelColor(EntityAlive holdingEntity, bool isText, int slot0, Color color, int slot1, int nameId)
        {
            if (GameManager.IsDedicatedServer)
                return true;

            if (isText)
            {
                WeaponLabelControllerBase controller = holdingEntity.inventory.GetHoldingItemTransform()?.GetComponent<WeaponLabelControllerBase>();
                //if (holdingEntity.emodel.avatarController is AvatarMultiBodyController multiBody && multiBody.HeldItemTransform != null)
                //    controller = multiBody.HeldItemTransform.GetComponent<WeaponLabelControllerBase>();
                //else if (holdingEntity.emodel.avatarController is LegacyAvatarController legacy && legacy.HeldItemTransform != null)
                //    controller = legacy.HeldItemTransform.GetComponent<WeaponLabelControllerBase>();
                return controller && controller.setLabelColor(slot0, color);
            }
            else
            {
                WeaponColorControllerBase controller = holdingEntity.inventory.GetHoldingItemTransform()?.GetComponent<WeaponColorControllerBase>();
                //if (holdingEntity.emodel.avatarController is AvatarMultiBodyController multiBody && multiBody.HeldItemTransform != null)
                //    controller = multiBody.HeldItemTransform.GetComponent<WeaponColorControllerBase>();
                //else if (holdingEntity.emodel.avatarController is LegacyAvatarController legacy && legacy.HeldItemTransform != null)
                //    controller = legacy.HeldItemTransform.GetComponent<WeaponColorControllerBase>();
                return controller && controller.setMaterialColor(slot0, slot1, nameId, color);
            }
        }

        public override void read(PooledBinaryReader _reader)
        {
            entityId = _reader.ReadInt32();
            isText = _reader.ReadBoolean();
            color = StreamUtils.ReadColor(_reader);
            index0 = _reader.ReadChar();
            if (!isText)
            {
                index1 = _reader.ReadChar();
                nameId = _reader.ReadInt32();
            }
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            _writer.Write(entityId);
            _writer.Write(isText);
            StreamUtils.Write(_writer, color);
            _writer.Write((char)index0);
            if (!isText)
            {
                _writer.Write((char)index1);
                _writer.Write(nameId);
            }
        }

        private int entityId;
        private bool isText;
        private Color color;
        private int index0;
        private int index1;
        private int nameId;
    }
}