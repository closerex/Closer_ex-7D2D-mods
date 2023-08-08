using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FullautoLauncher.Scripts.ProjectileManager
{
    public class ProjectileParams
    {
        public int ProjectileID;
        public ItemInfo info;
        public Vector3 flyDirection;
        public Vector3 renderPosition;
        public Vector3 velocity;
        public Vector3 previousPosition;
        public Vector3 currentPosition;
        public Vector3 gravity;
        public Vector3 moveDir;
        public float timeShotStarted;
        public int hmOverride;
        public float radius;
        public bool bOnIdealPosition = false;
        public CollisionParticleController waterCollisionParticles = new CollisionParticleController();

        public ProjectileParams(int projectileID)
        {
            ProjectileID = projectileID;
        }

        public void Fire(ItemInfo _info, Vector3 _idealStartPosition, Vector3 _realStartPosition, Vector3 _flyDirection, Entity _firingEntity, int _hmOverride = 0, float _radius = 0f)
        {
            info = _info;
            flyDirection = _flyDirection.normalized;
            moveDir = flyDirection;
            previousPosition = currentPosition = _idealStartPosition;
            renderPosition = _realStartPosition;
            velocity = flyDirection.normalized * EffectManager.GetValue(PassiveEffects.ProjectileVelocity, info.itemValueLauncher, info.itemActionProjectile.Velocity, _firingEntity as EntityAlive, null, default(FastTags), true, true, true, true, 1, true, false);
            hmOverride = _hmOverride;
            radius = _radius;
            waterCollisionParticles.Init(_firingEntity.entityId, info.itemProjectile.MadeOfMaterial.SurfaceCategory, "water", 16);
            gravity = Vector3.up * EffectManager.GetValue(PassiveEffects.ProjectileGravity, info.itemValueLauncher, info.itemActionProjectile.Gravity, _firingEntity as EntityAlive, null, default(FastTags), true, true, true, true, 1, true, false);
            timeShotStarted = Time.time;
        }

        public override int GetHashCode()
        {
            return ProjectileID;
        }

        public bool UpdatePosition()
        {
            float flyTime = Time.time - timeShotStarted;
            if (flyTime >= info.itemActionProjectile.LifeTime)
            {
                return true;
            }
            if (flyTime >= info.itemActionProjectile.FlyTime)
            {
                velocity += gravity * Time.fixedDeltaTime;
            }
            moveDir = velocity * Time.fixedDeltaTime;
            previousPosition = currentPosition;
            currentPosition += moveDir;
            renderPosition += moveDir;
            if (!bOnIdealPosition)
            {
                bOnIdealPosition = flyTime > 0.5f;
            }
            if(!bOnIdealPosition)
            {
                renderPosition = Vector3.Lerp(renderPosition, currentPosition, flyTime * 2f);
            }
            //Log.Out($"projectile {ProjectileID} position {currentPosition} entity position {info.actionData.invData.holdingEntity.position}");
            return false;
        }

        public bool CheckCollision(EntityAlive entityAlive)
        {
            World world = GameManager.Instance.World;
            Vector3 dir = currentPosition - previousPosition;
            float magnitude = dir.magnitude;
            if (magnitude < 0.04f)
            {
                return false;
            }
            Ray ray = new Ray(previousPosition, dir.normalized);
            waterCollisionParticles.CheckCollision(ray.origin, ray.direction, magnitude, (entityAlive != null) ? entityAlive.entityId : (-1));
            int hitmask = ((hmOverride == 0) ? 80 : hmOverride);
            bool bHit = Voxel.Raycast(world, ray, magnitude, -538750997, hitmask, radius);
            if (bHit && (GameUtils.IsBlockOrTerrain(Voxel.voxelRayHitInfo.tag) || Voxel.voxelRayHitInfo.tag.StartsWith("E_")))
            {
                if (entityAlive != null && !entityAlive.isEntityRemote)
                {
                    entityAlive.MinEventContext.Other = ItemActionAttack.FindHitEntity(Voxel.voxelRayHitInfo) as EntityAlive;
                    ItemActionAttack.AttackHitInfo attackHitInfo = new ItemActionAttack.AttackHitInfo
                    {
                        WeaponTypeTag = ItemActionAttack.RangedTag
                    };
                    ItemActionAttack.Hit(Voxel.voxelRayHitInfo, entityAlive.entityId, EnumDamageTypes.Piercing, info.itemActionProjectile.GetDamageBlock(info.itemValueLauncher, ItemActionAttack.GetBlockHit(world, Voxel.voxelRayHitInfo), entityAlive, 0), info.itemActionProjectile.GetDamageEntity(info.itemValueLauncher, entityAlive, 0), 1f, 1f, EffectManager.GetValue(PassiveEffects.CriticalChance, info.itemValueLauncher, info.itemProjectile.CritChance.Value, entityAlive, null, info.itemProjectile.ItemTags, true, true, true, true, 1, true, false), ItemAction.GetDismemberChance(info.actionData, Voxel.voxelRayHitInfo), info.itemProjectile.MadeOfMaterial.SurfaceCategory, info.itemActionProjectile.GetDamageMultiplier(), info.itemActionProjectile.BuffActions, attackHitInfo, 1, info.itemActionProjectile.ActionExp, info.itemActionProjectile.ActionExpBonusMultiplier, null, null, ItemActionAttack.EnumAttackMode.RealNoHarvesting, null, -1, info.itemValueLauncher);
                    if (entityAlive.MinEventContext.Other == null)
                    {
                        entityAlive.FireEvent(MinEventTypes.onSelfPrimaryActionMissEntity, true);
                    }
                    entityAlive.FireEvent(MinEventTypes.onProjectileImpact, false);
                    MinEventParams.CachedEventParam.Self = entityAlive;
                    MinEventParams.CachedEventParam.Position = Voxel.voxelRayHitInfo.hit.pos;
                    MinEventParams.CachedEventParam.ItemValue = info.itemValueProjectile;
                    MinEventParams.CachedEventParam.Other = entityAlive.MinEventContext.Other;
                    info.itemProjectile.FireEvent(MinEventTypes.onProjectileImpact, MinEventParams.CachedEventParam);
                    if (info.itemActionProjectile.Explosion.ParticleIndex > 0)
                    {
                        Vector3 vector3 = Voxel.voxelRayHitInfo.hit.pos - dir.normalized * 0.1f;
                        Vector3i vector3i = World.worldToBlockPos(vector3);
                        if (!world.GetBlock(vector3i).isair)
                        {
                            BlockFace blockFace;
                            vector3i = Voxel.OneVoxelStep(vector3i, vector3, -dir.normalized, out vector3, out blockFace);
                        }
                        GameManager.Instance.ExplosionServer(Voxel.voxelRayHitInfo.hit.clrIdx, vector3, vector3i, Quaternion.identity, info.itemActionProjectile.Explosion, entityAlive.entityId, 0f, false, info.itemValueProjectile);
                    }
                }
                return true;
            }
            return false;
        }

        public class ItemInfo
        {
            public ItemActionProjectile itemActionProjectile;
            public ItemClass itemProjectile;
            public ItemValue itemValueProjectile;
            public ItemValue itemValueLauncher;
            public ItemActionData actionData;
        }
    }
}
