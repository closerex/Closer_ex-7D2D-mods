using KFCommonUtilityLib;
using System;
using System.Xml.Linq;

public class MinEventActionKnockDownTarget : MinEventActionTargetedBase
{
    private bool forceHitInfo = false;
    private EnumBodyPartHit bodyPart = EnumBodyPartHit.None;
    private Utils.EnumHitDirection hitDirection = Utils.EnumHitDirection.None;
    private bool criticalHit = false;
    private float duration = 1f;
    private static bool debug = false;

    public override void Execute(MinEventParams _params)
    {
        foreach (var target in targets)
        {
            if (target.emodel?.avatarController == null || target.bodyDamage.CurrentStun != EnumEntityStunType.None)
            {
                continue;
            }

            if (!forceHitInfo && _params.Self != null && target.lastDamageResponse.Source != null && target.lastDamageResponse.Source.ownerEntityId == _params.Self.entityId)
            {
                DamageResponse damageResponse = target.lastDamageResponse;
                ForceStunTargetServer(target, EnumEntityStunType.Prone, damageResponse.HitBodyPart, (Utils.EnumHitDirection)Utils.Get4HitDirectionAsInt(damageResponse.Source.getDirection(), target.GetLookVector()), damageResponse.Critical, damageResponse.Random, duration);
                if (debug)
                    Log.Out($"MinEventActionKnockDownTarget: Knocked down target {target.GetDebugName()} (ID {target.entityId}) with hit body part {damageResponse.HitBodyPart}, hit direction {(Utils.EnumHitDirection)Utils.Get4HitDirectionAsInt(damageResponse.Source.getDirection(), target.GetLookVector())}, critical hit {damageResponse.Critical}, random {damageResponse.Random}, duration {duration}");
            }
            else
            {
                ForceStunTargetServer(target, EnumEntityStunType.Prone, bodyPart, hitDirection, criticalHit, GameManager.Instance.World.GetGameRandom().RandomFloat, duration);
                if (debug)
                    Log.Out($"MinEventActionKnockDownTarget: Knocked down target {target.GetDebugName()} (ID {target.entityId}) with forced hit body part {bodyPart}, hit direction {hitDirection}, critical hit {criticalHit}, random {GameManager.Instance.World.GetGameRandom().RandomFloat}, duration {duration}");
            }
        }
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if (!base.ParseXmlAttribute(_attribute))
        {
            switch (_attribute.Name.LocalName)
            {
                case "body_part":
                    bodyPart = Enum.Parse<EnumBodyPartHit>(_attribute.Value, true);
                    forceHitInfo = true;
                    return true;
                case "hit_direction":
                    hitDirection = Enum.Parse<Utils.EnumHitDirection>(_attribute.Value, true);
                    forceHitInfo = true;
                    return true;
                case "critical_hit":
                    criticalHit = bool.Parse(_attribute.Value);
                    forceHitInfo = true;
                    return true;
                case "duration":
                    duration = float.Parse(_attribute.Value);
                    return true;
            }
        }
        return false;
    }

    public static void ForceStunTargetLocal(EntityAlive target, EnumEntityStunType stun, EnumBodyPartHit _bodyPart, Utils.EnumHitDirection _hitDirection, bool _criticalHit, float random, float duration)
    {
        if (!target || target.IsDead() || !target.emodel?.avatarController || target.bodyDamage.CurrentStun != EnumEntityStunType.None)
            return;
        target.emodel.avatarController.BeginStun(stun, _bodyPart, _hitDirection, _criticalHit, random);
        target.SetStun(stun);
        target.bodyDamage.StunDuration = duration;
    }

    public static void ForceStunTargetServer(EntityAlive target, EnumEntityStunType stun, EnumBodyPartHit _bodyPart, Utils.EnumHitDirection _hitDirection, bool _criticalHit, float random, float duration)
    {
        if (!target || target.IsDead())
            return;
        if (target.isEntityRemote)
        {
            if (ConnectionManager.Instance.IsServer)
            {
                ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageForceKnockDown>().Setup(target.entityId, stun, _bodyPart, _hitDirection, _criticalHit, random, duration), true, target.entityId);
            }
            else
            {
                ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageForceKnockDown>().Setup(target.entityId, stun, _bodyPart, _hitDirection, _criticalHit, random, duration));
            }
        }
        else
        {
            ForceStunTargetLocal(target, stun, _bodyPart, _hitDirection, _criticalHit, random, duration);
        }
    }
}
