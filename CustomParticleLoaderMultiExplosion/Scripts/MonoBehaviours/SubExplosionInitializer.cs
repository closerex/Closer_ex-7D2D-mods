﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;

public class SubExplosionInitializer : MonoBehaviour
{
    public ExplosionData data;
    public ItemValue value = null;
    public int clrIdx = 0;
    public EntityAlive entityAlive = null;
    public bool ballistic = false;
    private ParticleSystem ps;
    private List<ParticleCollisionEvent> list_events;
    private World world;
    private Collider collider;
    private BallisticParticleJob job;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        list_events = new List<ParticleCollisionEvent>();
        world = GameManager.Instance.World;
        var collision = ps.collision;
        collision.collidesWith = Physics.AllLayers;
        collider = GetComponent<Collider>();
        if (collider)
        {
            collider.isTrigger = true;
            collider.gameObject.layer = 14;
            Collider[] others = null;
            if (collider is SphereCollider sphereCollider)
                others = Physics.OverlapSphere(transform.TransformPoint(sphereCollider.center), sphereCollider.radius);
            else if (collider is BoxCollider boxCollider)
                others = Physics.OverlapBox(transform.TransformPoint(boxCollider.center), boxCollider.size * 0.5f, transform.rotation);
            else if(collider is CapsuleCollider capsuleCollider)
            {
                float x = capsuleCollider.center.x, y = capsuleCollider.center.y, z = capsuleCollider.center.z;
                float x1, x2, y1, y2, z1, z2;
                float halfHeight = capsuleCollider.height * 0.5f;
                switch(capsuleCollider.direction)
                {
                    case 0:
                        x1 = x - halfHeight;
                        x2 = x + halfHeight;
                        y1 = y2 = y;
                        z1 = z2 = y;
                        break;
                    case 1:
                        x1 = x2 = x;
                        y1 = y - halfHeight;
                        y2 = y + halfHeight;
                        z1 = z2 = z;
                        break;
                    case 2:
                        x1 = x2 = x;
                        y1 = y2 = y;
                        z1 = z - halfHeight;
                        z2 = z + halfHeight;
                        break;
                    default:
                        x1 = x2 = x;
                        y1 = y2 = y;
                        z1 = z2 = z;
                        break;
                }
                others = Physics.OverlapCapsule(transform.TransformPoint(new Vector3(x1, y1, z1)), transform.TransformPoint(new Vector3(x2, y2, z2)), capsuleCollider.radius);
            }
            if(others != null && others.Length > 0)
                foreach(Collider other in others)
                {
                    if(Physics.ComputePenetration(collider, transform.position, transform.rotation, other, other.transform.position, other.transform.rotation, out Vector3 dir, out float distance));
                        transform.position += dir * distance;
                }
        }
    }

    public void SetBallistic(bool flag)
    {
        ballistic = flag;
        if(ballistic)
        {
            job = new BallisticParticleJob(entityAlive ? entityAlive.entityId : -1, value != null ? value.GetItemId() : -1, data.ParticleIndex); ;
            var collision = ps.collision;
            collision.enabled = false;
        }
        Log.Out("Ballistic: " + ballistic);
    }
    /*
    void OnTriggerEnter(Collider other)
    {
        Physics.ComputePenetration(collider, transform.position, transform.rotation, other, other.transform.position, other.transform.rotation, out Vector3 dir, out float distance);
        transform.position += dir * distance;
        Log.Out("Trigger enter: dir" + dir + " distance " + distance);
    }
    */
    void OnParticleCollision(GameObject other)
    {
        if (ballistic)
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
                entityAlive.MinEventContext.Position = vec;
                entityAlive.MinEventContext.ItemValue = value;
                entityAlive.FireEvent(MinEventTypes.onProjectileImpact, false);

                MinEventParams.CachedEventParam.Self = entityAlive;
                MinEventParams.CachedEventParam.Position = vec;
                MinEventParams.CachedEventParam.ItemValue = value;
                value.ItemClass.FireEvent(MinEventTypes.onProjectileImpact, MinEventParams.CachedEventParam);
            }

            GameManager.Instance.ExplosionServer(clrIdx, vec, blockpos, Quaternion.identity, data, entityAlive != null ? entityAlive.entityId : -1, 0, false, value);
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
    void OnParticleUpdateJobScheduled()
    {
        if (ballistic)
            job.Schedule(ps);
    }

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
}

