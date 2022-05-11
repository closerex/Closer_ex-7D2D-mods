using System.Collections.Generic;
using UnityEngine;

public class SubExplosionInitializer : MonoBehaviour
{
    public ExplosionData data;
    public ItemValue value = null;
    public int clrIdx = 0;
    public EntityAlive entityAlive = null;
    private bool explodeOnDeath = false;
    private bool explodeOnCollision = true;
    private ParticleSystem ps;
    private List<ParticleCollisionEvent> list_events;
    private World world;
    //private BallisticParticleJob job;
    private ParticleSystem.Particle[] particles;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        list_events = new List<ParticleCollisionEvent>();
        world = GameManager.Instance.World;
        var collision = ps.collision;
        collision.collidesWith = Physics.AllLayers;
    }

    public void SetExplodeOnDeath(bool both)
    {
        explodeOnDeath = true;
        explodeOnCollision = both;
        particles = new ParticleSystem.Particle[ps.main.maxParticles];
    }

    void OnParticleCollision(GameObject other)
    {
        if (!explodeOnCollision)
            return;
        int numCollisionEvents = ps.GetCollisionEvents(other, list_events);
        //Log.Out("Particle collided! Count: " + numCollisionEvents.ToString());
        int i = 0;
        bool otherIsBorT = GameUtils.IsBlockOrTerrain(other.tag);
        while (i < numCollisionEvents)
        {
            Vector3 pos = list_events[i].intersection + Origin.position;
            Vector3 velocity = list_events[i].velocity;
            //GameManager.Instance.ExplosionServer(0, pos, World.worldToBlockPos(pos), Quaternion.identity, data, entityid, 0, false, value);
            //Log.Out("Explosion at: " + pos.ToString() + World.worldToBlockPos(pos).ToString());

            Vector3 vec = otherIsBorT ? pos - velocity.normalized * 0.1f : pos;
            Vector3i blockpos = World.worldToBlockPos(vec);
            if (!world.GetBlock(blockpos).isair)
                blockpos = Voxel.OneVoxelStep(blockpos, vec, -velocity.normalized, out vec, out BlockFace blockFace);

            DoExplosionServer(vec, blockpos);
            //Log.Out("Explosion at: " + vec.ToString() + blockpos.ToString());

            /*
            Ray ray = new Ray(pos - velocity, velocity.normalized);
            float magnitude = velocity.magnitude + 1f;
            bool flag = Voxel.Raycast(world, ray, magnitude, -538750981, 16 | 64, 0);
            //Log.Out(pos.ToString() + velocity.ToString() + ray.ToString() + magnitude.ToString() + Voxel.voxelRayHitInfo.tag);
            if (flag && (GameUtils.IsBlockOrTerrain(Voxel.voxelRayHitInfo.tag) || Voxel.voxelRayHitInfo.tag.StartsWith("E_")))
            {
                Vector3 vec = GameUtils.IsBlockOrTerrain(Voxel.voxelRayHitInfo.tag) ? Voxel.voxelRayHitInfo.hit.pos - velocity.normalized * 0.1f : pos;
                Vector3i blockpos = World.worldToBlockPos(vec);
                if (!world.GetBlock(blockpos).isair)
                    blockpos = Voxel.OneVoxelStep(blockpos, vec, -velocity.normalized, out vec, out BlockFace blockFace);
            
                GameManager.Instance.ExplosionServer(Voxel.voxelRayHitInfo.hit.clrIdx, vec, blockpos, Quaternion.identity, data, entityid, 0, false, value);
                Log.Out("Explosion at: " + vec.ToString() + blockpos.ToString());
            }
            */

            i++;
        }
    }

    void FixedUpdate()
    {
        if (!explodeOnDeath)
            return;

        int count = ps.GetParticles(particles);
        for(int i = 0; i < count; ++i)
        {
            if(particles[i].remainingLifetime <= Time.fixedDeltaTime)
            {
                Vector3 finalPos = particles[i].position + particles[i].totalVelocity * Time.fixedDeltaTime + Origin.position;
                Vector3i blockPos = World.worldToBlockPos(finalPos);
                if (!world.GetBlock(blockPos).isair)
                    blockPos = Voxel.OneVoxelStep(blockPos, finalPos, -particles[i].totalVelocity.normalized, out finalPos, out BlockFace blockFace);
                DoExplosionServer(finalPos, blockPos);
                //Log.Out("Explode! remaining lifetime: " + particles[i].remainingLifetime);
            }
        }
    }

    private void DoExplosionServer(Vector3 worldPos, Vector3i blockPos)
    {
        if (entityAlive != null && value != null)
        {
            /*
            if (otherIsBorT)
            {
                ChunkCluster chunkCluster = world.ChunkClusters[clrIdx];
                if (chunkCluster != null)
                {
                    BlockValue blockValue = BlockValue.Air;
                    blockValue = chunkCluster.GetBlock(blockpos);
                    if (!blockValue.isair && blockValue.Block != null)
                    {
                        if (blockValue.ischild)
                        {
                            Vector3i pblockpos = blockValue.Block.multiBlockPos.GetParentPos(blockpos, blockValue);
                            blockValue = chunkCluster.GetBlock(pblockpos);
                        }
                        if (!blockValue.Equals(BlockValue.Air))
                        {
                            entityAlive.MinEventContext.ItemValue = value;
                            entityAlive.MinEventContext.BlockValue = blockValue;
                            entityAlive.MinEventContext.Tags = blockValue.Block.Tags;
                            entityAlive.FireEvent(MinEventTypes.onSelfDamagedBlock, false);
                        }
                    }
                }
            }
            */
            entityAlive.MinEventContext.Position = worldPos;
            entityAlive.MinEventContext.ItemValue = value;
            entityAlive.FireEvent(MinEventTypes.onProjectileImpact, false);

            MinEventParams.CachedEventParam.Self = entityAlive;
            MinEventParams.CachedEventParam.Position = worldPos;
            MinEventParams.CachedEventParam.ItemValue = value;
            value.ItemClass.FireEvent(MinEventTypes.onProjectileImpact, MinEventParams.CachedEventParam);
        }
        GameManager.Instance.ExplosionServer(clrIdx, worldPos, blockPos, Quaternion.identity, data, entityAlive != null ? entityAlive.entityId : -1, 0, false, value);
    }

    /*
    private static bool DoVoxelCast(World world, Ray ray, float distance, float radius, int index, EntityAlive entityAlive, ItemValue itemValue)
    {
        bool hit = Voxel.Raycast(world, ray, distance, 16 | 64, radius);
        if(hit && (GameUtils.IsBlockOrTerrain(Voxel.voxelRayHitInfo.tag) || Voxel.voxelRayHitInfo.tag.StartsWith("E_")))
        {
            if(entityAlive)
            {
                entityAlive.MinEventContext.Other = (ItemActionAttack.FindHitEntity(Voxel.voxelRayHitInfo) as EntityAlive);
                entityAlive.MinEventContext.Position = Voxel.voxelRayHitInfo.hit.pos;
                entityAlive.MinEventContext.ItemValue = itemValue;
                entityAlive.FireEvent(MinEventTypes.onProjectileImpact, false);

                if(itemValue != null)
                {
                    MinEventParams.CachedEventParam.Self = entityAlive;
                    MinEventParams.CachedEventParam.Position = Voxel.voxelRayHitInfo.hit.pos;
                    MinEventParams.CachedEventParam.ItemValue = itemValue;
                    itemValue.ItemClass.FireEvent(MinEventTypes.onProjectileImpact, MinEventParams.CachedEventParam);
                }
            }
            Vector3 vector2 = Voxel.voxelRayHitInfo.hit.pos - ray.direction * 0.1f;
            Vector3i vector3i = World.worldToBlockPos(vector2);
            if (!world.GetBlock(vector3i).isair)
                vector3i = Voxel.OneVoxelStep(vector3i, vector2, -ray.direction.normalized, out vector2, out BlockFace blockFace);

            if (CustomParticleEffectLoader.GetCustomParticleComponents(index, out CustomParticleComponents component))
                world.gameManager.ExplosionServer(Voxel.voxelRayHitInfo.hit.clrIdx, vector2, vector3i, Quaternion.identity, component.BoundExplosionData, entityAlive ? entityAlive.entityId : -1, 0, false, itemValue);
        }
        return hit;
    }

    private struct BallisticParticleJob : IJobParticleSystem
    {
        int entityId;
        int itemClassId;
        int particleIndex;
        public BallisticParticleJob(int entityId, int itemClassId, int particleIndex)
        {
            this.entityId = entityId;
            this.itemClassId = itemClassId;
            this.particleIndex = particleIndex;
        }
        void IJobParticleSystem.Execute(ParticleSystemJobData particles)
        {
            World world = GameManager.Instance.World;
            if (world == null)
                return;
            EntityAlive entity = world.GetEntity(entityId) as EntityAlive;
            ItemValue value = null;
            if (itemClassId >= 0)
                value = new ItemValue(itemClassId);
            //Log.Out("particle job: itemclass " + itemClassId + " particle index " + particleIndex + " entity id " + entityId + " particle count: " + particles.count);
            for (int i = 0; i < particles.count; ++i)
            {
                Vector3 pos = particles.positions[i];
                Vector3 vel = particles.velocities[i];
                Ray ray = new Ray(pos + Origin.position, vel.normalized);
                if (SubExplosionInitializer.DoVoxelCast(world, ray, vel.magnitude * Time.fixedDeltaTime, 0, particleIndex, entity, value))
                {
                    var aliveTime = particles.aliveTimePercent;
                    aliveTime[i] = 100;
                    //Log.Out("Voxel cast hit!");
                }
            }
        }
    }
    */
}


