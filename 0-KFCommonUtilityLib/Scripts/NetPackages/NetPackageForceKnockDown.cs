using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib
{
    public class NetPackageForceKnockDown : NetPackage
    {
        private int entityID;
        private EnumEntityStunType stunType;
        private EnumBodyPartHit bodyPart;
        private Utils.EnumHitDirection hitDirection;
        private bool criticalHit;
        private float random;
        private float duration;

        public override NetPackageDirection PackageDirection => NetPackageDirection.Both;

        public NetPackageForceKnockDown Setup(int entityID, EnumEntityStunType stun, EnumBodyPartHit _bodyPart, Utils.EnumHitDirection _hitDirection, bool _criticalHit, float random, float duration)
        {
            this.entityID = entityID;
            stunType = stun;
            bodyPart = _bodyPart;
            hitDirection = _hitDirection;
            criticalHit = _criticalHit;
            this.random = random;
            this.duration = duration;
            return this;
        }

        public override int GetLength()
        {
            return 20;
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null || _callbacks == null)
                return;
            var entity = _world.GetEntity(entityID) as EntityAlive;
            if (!entity)
                return;
            MinEventActionKnockDownTarget.ForceStunTargetServer(entity, stunType, bodyPart, hitDirection, criticalHit, random, duration);
        }

        public override void read(PooledBinaryReader _reader)
        {
            entityID = _reader.ReadInt32();
            stunType = (EnumEntityStunType)_reader.ReadByte();
            bodyPart = (EnumBodyPartHit)_reader.ReadByte();
            hitDirection = (Utils.EnumHitDirection)_reader.ReadByte();
            criticalHit = _reader.ReadBoolean();
            random = _reader.ReadSingle();
            duration = _reader.ReadSingle();
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            _writer.Write(entityID);
            _writer.Write((byte)stunType);
            _writer.Write((byte)bodyPart);
            _writer.Write((byte)hitDirection);
            _writer.Write(criticalHit);
            _writer.Write(random);
            _writer.Write(duration);
        }
    }
}
