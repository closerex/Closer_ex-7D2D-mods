using KFCommonUtilityLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static EntityDrone;
using static KFCommonUtilityLib.VoxelCaster;
using static UnityEngine.UI.Image;

public class AreaSweep : MonoBehaviour
{
    private static bool debugLog = false;
    private struct EntityHitInfo
    {
        public int entityId;
        public EnumBodyPartHit bodyPart;
        public int hitIndex;

        public EntityHitInfo(int _entityId, EnumBodyPartHit _bodyPart, int _hitIndex)
        {
            entityId = _entityId;
            bodyPart = _bodyPart;
            hitIndex = _hitIndex;
        }
    }

    public static FastTags<TagGroup.Global> triggerTags = FastTags<TagGroup.Global>.Parse("AreaSweep");
    public EntityAlive attackingEntity;
    public ItemValue attackingItemValue;
    public ItemAction attackingAction;
    public ItemActionData attackingActionData;
    public int hitMask;

    public event Action<AreaSweep> OnDestroyed;
    public static AreaSweep lastFiredInstance;

    public Vector3 previousPosition;
    public Vector3 flyDirection;
    public Vector3 velocity;
    public Vector3 extents;
    public Vector2 blockExtents;
    public Vector3 initialScale;
    public Vector4 scaleFactors;
    public float stateTime;
    public float life;
    public float range;
    public float deathDelay;
    public int entityPenetration;
    public int blockPenetrationFactor;
    public string surfaceCategory;
    public bool fixedBlockExtents;
    private bool dead;
    private float collisionStartBack = 0.1f;
    private EnumDamageTypes damageType;
    private HashSet<int> hitEntities = new();
    private HashSet<Vector3i> hitBlocks = new();
    private List<EntityHitInfo> pendingEntityHits = new();
    private FastTags<TagGroup.Global> attackingTags;
    private MinEventParams attackingEventParams = new();
    private BodyPartSortingOrder sortOrder = new();
    private float falloffRange, blockDamage, entityDamage;
    private Vector3 initialSize;
    private Vector3 currentScaleFactor;

    public void Fire(Vector3 position, Quaternion rotation, Vector3 fullExtents, Vector2 blockExtents, Vector3 initialScale, Vector4 scaleFactors, EntityAlive attacker, ItemValue itemValue, ItemAction action, ItemActionData actionData, EnumDamageTypes damageType, float lifetime, bool fixedBlockExtents, string surfaceOverride = null, int _hmOverride = 0, float deathDelay = 0f)
    {
        lastFiredInstance = this;
        Transform transform = this.transform;
        transform.rotation = rotation;
        initialSize = transform.localScale;
        extents = fullExtents;
        this.blockExtents = blockExtents;
        this.fixedBlockExtents = fixedBlockExtents;
        this.initialScale = initialScale;
        currentScaleFactor = initialScale;
        transform.localScale = new Vector3(initialSize.x * initialScale.x, initialSize.y * initialScale.y, initialSize.z * initialScale.z);

        attackingEntity = attacker;
        attackingItemValue = itemValue;
        attackingAction = action;
        attackingActionData = actionData;
        this.damageType = damageType;
        hitMask = ((_hmOverride == 0) ? 80 : _hmOverride);
        stateTime = 0f;
        life = lifetime;
        this.deathDelay = deathDelay;
        surfaceCategory = string.IsNullOrEmpty(surfaceOverride) ? itemValue.ItemClass.MadeOfMaterial.SurfaceCategory : surfaceOverride;
        dead = false;

        attackingTags = itemValue.ItemClass.ItemTags;
        MultiActionManager.ModifyItemTags(itemValue, actionData, ref attackingTags);
        attackingTags |= actionData.ActionTags | FastTags<TagGroup.Global>.Parse("AreaSweep");

        flyDirection = transform.forward;
        velocity = EffectManager.GetValue(PassiveEffects.ProjectileVelocity, attackingItemValue, 0, attacker, null, attackingTags, true, false) * flyDirection;
        range = velocity.magnitude * lifetime;

        entityPenetration = Mathf.FloorToInt(EffectManager.GetValue(PassiveEffects.EntityPenetrationCount, attackingItemValue, 0, attacker, null, attackingTags, true, false));
        blockPenetrationFactor = Mathf.Max(1, Mathf.FloorToInt(EffectManager.GetValue(PassiveEffects.BlockPenetrationFactor, attackingItemValue, 1, attacker, null, attackingTags, true, false)));

        falloffRange = EffectManager.GetValue(PassiveEffects.DamageFalloffRange, attackingItemValue, range, attackingEntity, null, attackingTags, true, false);
        blockDamage = EffectManager.GetValue(PassiveEffects.BlockDamage, attackingItemValue, 0, attackingEntity, null, attackingTags, true, false);
        entityDamage = EffectManager.GetValue(PassiveEffects.EntityDamage, attackingItemValue, 0, attackingEntity, null, attackingTags, true, false);
        this.scaleFactors = new Vector4(EffectManager.GetValue(CustomEnums.CustomTaggedEffect, attackingItemValue, scaleFactors.x, attackingEntity, null, FastTags<TagGroup.Global>.Parse("AreaSweepScaleX"), true, false),
                                        EffectManager.GetValue(CustomEnums.CustomTaggedEffect, attackingItemValue, scaleFactors.y, attackingEntity, null, FastTags<TagGroup.Global>.Parse("AreaSweepScaleY"), true, false),
                                        EffectManager.GetValue(CustomEnums.CustomTaggedEffect, attackingItemValue, scaleFactors.z, attackingEntity, null, FastTags<TagGroup.Global>.Parse("AreaSweepScaleZ"), true, false),
                                        EffectManager.GetValue(CustomEnums.CustomTaggedEffect, attackingItemValue, scaleFactors.w, attackingEntity, null, FastTags<TagGroup.Global>.Parse("AreaSweepScaleTime"), true, false));

        OnActivateItemGameObjectReference activateRef = transform.GetComponent<OnActivateItemGameObjectReference>();
        if (activateRef)
        {
            activateRef.ActivateItem(_activate: true);
        }

        if (transform.parent)
        {
            transform.SetParent(null);
            Utils.SetLayerRecursively(transform.gameObject, 0);
        }

        previousPosition = position;
        transform.position = position - Origin.position;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        checkCollision();
    }

    private void SetDead()
    {
        dead = true;
        stateTime = 0;
        if (deathDelay <= 0f)
        {
            OnDead();
            return;
        }
        Transform transform = this.transform;
        Transform explTrans = transform.Find("MeshExplode");
        if (explTrans)
        {
            explTrans.gameObject.SetActive(true);
        }
        //patch with explosion
    }

    private void OnDead()
    {
        if (OnDestroyed != null)
        {
            OnDestroyed(this);
        }
        else
        {
            Destroy(gameObject);
        }
        if (lastFiredInstance == this)
        {
            lastFiredInstance = null;
        }
    }

    protected void FixedUpdate()
    {
        GameManager instance = GameManager.Instance;
        if (!instance || instance.World == null)
        {
            SetDead();
            return;
        }

        float fixedDeltaTime = Time.fixedDeltaTime;
        stateTime += fixedDeltaTime;

        if (dead)
        {
            if (stateTime >= deathDelay)
            {
                OnDead();
            }
            return;
        }

        Transform transform = this.transform;
        Vector3 moveBy = velocity * fixedDeltaTime;
        Vector3 targetPos = transform.position + moveBy;
        transform.LookAt(targetPos, transform.up);
        transform.position = targetPos;

        float maxScaleTime = scaleFactors.w;
        float curScaleRatio = Mathf.InverseLerp(0, maxScaleTime, stateTime);
        currentScaleFactor = Vector3.Lerp(initialScale, scaleFactors, curScaleRatio);
        transform.localScale = new Vector3(initialSize.x * currentScaleFactor.x, initialSize.y * currentScaleFactor.y, initialSize.z * currentScaleFactor.z);

        if (stateTime >= life)
        {
            SetDead();
        }
        else
        {
            checkCollision();
        }
    }

    protected virtual void checkCollision()
    {
        GameManager instance = GameManager.Instance;
        if (!instance)
        {
            return;
        }

        World world = instance.World;
        if (world == null)
        {
            return;
        }

        Vector3 curPos = transform.position + extents.z * currentScaleFactor.z * flyDirection + Origin.position;
        Vector3 direction = curPos - previousPosition;
        float magnitude = direction.magnitude;
        direction = direction.normalized;
        if (magnitude < 0.04f)
        {
            return;
        }

        int prevLayer = -1;
        if (attackingEntity && attackingEntity.emodel)
        {
            prevLayer = attackingEntity.GetModelLayer();
            attackingEntity.SetModelLayer(2);
        }

        bool isAttackerLocal = attackingEntity && !attackingEntity.isEntityRemote;
        if (isAttackerLocal)
        {
            MinEventParams.CopyTo(attackingEntity.MinEventContext, attackingEventParams);
            attackingEventParams.ItemActionData = attackingActionData;
            attackingEventParams.ItemValue = attackingItemValue;
            attackingEventParams.Tags = triggerTags;
            attackingEventParams.StartPosition = previousPosition;
        }

        int hitCount = 0;
        Vector2 curExtents = new Vector2(extents.x * currentScaleFactor.x, extents.y * currentScaleFactor.y);
        Vector2 curBlockExtents = fixedBlockExtents ? new Vector2(blockExtents.x * extents.x, blockExtents.y * extents.y) : new Vector2(blockExtents.x * curExtents.x, blockExtents.y * curExtents.y);
        bool hitBlock = false;
        float blockDistance = 0f;
        if (curBlockExtents.x > 0 || curBlockExtents.y > 0)
        {
            hitBlock = checkCollisionWithBlock(world, curBlockExtents, magnitude, isAttackerLocal, ref hitCount, out blockDistance);
        }
        if ((!hitBlock || blockDistance > 0f) && (curExtents.x > 0 || curExtents.y > 0))
        {
            checkCollisionWithEntities(world, curExtents, hitBlock ? blockDistance : magnitude, isAttackerLocal, ref hitCount);
        }
        if (hitBlock)
        {
            entityPenetration = 0;
        }

        if (prevLayer >= 0)
        {
            attackingEntity.SetModelLayer(prevLayer);
        }
        if (isAttackerLocal && ItemActionDynamic.ShowDebugDisplayHit)
        {
            GameObject debugObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debugObj.transform.position = previousPosition + direction * magnitude * 0.5f - Origin.position;
            debugObj.transform.rotation = transform.rotation;
            debugObj.transform.localScale = new Vector3(curExtents.x, curExtents.y, magnitude);
            debugObj.layer = 2;
            debugObj.GetComponent<Renderer>().material.color = hitCount > 0 ? new Color(0f, 1f, 0f, 1f) : new Color(1f, 0f, 0f, 1f);
            ItemActionDynamic.DebugDisplayHits.Add(debugObj);
        }
        previousPosition = curPos;

        if (entityPenetration <= 0)
        {
            SetDead();
        }
    }

    private bool checkCollisionWithBlock(World world, Vector2 extents, float magnitude, bool isAttackerLocal, ref int hitCount, out float distance)
    {
        IEnumerable<VoxelCaster.HitInfo> results = VoxelCaster.BoxCastAll(world, previousPosition, transform.rotation, new Vector3(extents.x / 2, extents.y / 2, collisionStartBack), magnitude, -538750997, hitMask);

        foreach (var info in results)
        {
            if (!info.isBlock || !GameUtils.IsBlockOrTerrain(Voxel.voxelRayHitInfo.tag))
            {
                continue;
            }
            WorldRayHitInfo worldRayHitInfo = Voxel.voxelRayHitInfo.Clone();

            BlockValue blockHit = BlockValue.Air;
            if (entityPenetration <= 0)
            {
                continue;
            }

            blockHit = GetBlockHit(world, worldRayHitInfo, out Vector3i realBlockPos);
            if (blockHit.isair || hitBlocks.Contains(realBlockPos))
            {
                continue;
            }

            hitBlocks.Add(realBlockPos);
            hitCount++;
            //find a way to properly hit connected blocks
            //entityPenetration -= Mathf.FloorToInt((float)blockHit.Block.MaxDamage / (float)blockPenetrationFactor);
            //entityPenetration = 0;
            if (debugLog)
                Log.Out($"AreaSweep hit block world pos {worldRayHitInfo.hit.pos} raw pos {worldRayHitInfo.hit.blockPos} real pos {realBlockPos} block {blockHit.Block.GetLocalizedBlockName()}, remaining penetration {entityPenetration}");
            if (isAttackerLocal)
            {
                attackingEventParams.Other = null;
                attackingEventParams.BlockValue = blockHit;
                attackingEventParams.StartPosition = worldRayHitInfo.hit.pos - Mathf.Sqrt(worldRayHitInfo.hit.distanceSq) * flyDirection;
                DoHit(worldRayHitInfo, blockHit);

                if (ItemActionDynamic.ShowDebugDisplayHit)
                {
                    GameObject debugObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    debugObj.transform.position = worldRayHitInfo.hit.pos - Origin.position;
                    debugObj.transform.localScale = new Vector3(.1f, .1f, .1f);
                    debugObj.layer = 2;
                    debugObj.GetComponent<Renderer>().material.color = new Color(0f, 0f, 1f, 1f);
                    ItemActionDynamic.DebugDisplayHits.Add(debugObj);
                }
            }
            distance = Mathf.Sqrt(worldRayHitInfo.hit.distanceSq);
            return true;
        }
        distance = 0f;
        return false;
    }

    private void checkCollisionWithEntities(World world, Vector2 extents, float magnitude, bool isAttackerLocal, ref int hitCount)
    {
        IEnumerable<VoxelCaster.HitInfo> results = VoxelCaster.BoxCastAll(world, previousPosition, transform.rotation, new Vector3(extents.x / 2, extents.y / 2, collisionStartBack), magnitude, -538750997, hitMask);

        pendingEntityHits.Clear();
        foreach (var info in results)
        {
            if (info.isBlock || !Voxel.voxelRayHitInfo.tag.StartsWith("E_"))
            {
                continue;
            }
            WorldRayHitInfo worldRayHitInfo = Voxel.voxelRayHitInfo.Clone();

            EntityAlive hitEntity = ItemActionAttack.FindHitEntityNoTagCheck(worldRayHitInfo, out _) as EntityAlive;
            if (hitEntities.Contains(hitEntity.entityId) || shouldIgnoreTarget(hitEntity, attackingEntity))
            {
                continue;
            }

            bool pendingEntityFound = false;
            for (int i = 0; i < pendingEntityHits.Count; i++)
            {
                EntityHitInfo entityHitInfo = pendingEntityHits[i];
                if (entityHitInfo.entityId == hitEntity.entityId)
                {
                    EnumBodyPartHit bodyPartHit = DamageSource.TagToBodyPart(worldRayHitInfo.tag);
                    if (sortOrder.Compare(bodyPartHit, entityHitInfo.bodyPart) < 0)
                    {
                        pendingEntityHits[i] = new EntityHitInfo(hitEntity.entityId, bodyPartHit, info.hitIndex);
                        if (debugLog)
                            Log.Out($"AreaSweep updated pending hit for entity {hitEntity.entityId}, hit tag {worldRayHitInfo.tag}");
                    }
                    pendingEntityFound = true;
                    break;
                }
            }
            if (!pendingEntityFound && entityPenetration > 0)
            {
                pendingEntityHits.Add(new EntityHitInfo(hitEntity.entityId, DamageSource.TagToBodyPart(worldRayHitInfo.tag), info.hitIndex));
                hitCount++;
                if (hitEntity.IsAlive())
                {
                    entityPenetration--;
                }
                if (debugLog)
                    Log.Out($"AreaSweep hit entity {hitEntity.entityId}, hit tag {worldRayHitInfo.tag}, remaining penetration {entityPenetration}");
            }
        }

        for (int i = 0; i < pendingEntityHits.Count; i++)
        {
            var entityHitInfo = pendingEntityHits[i];
            hitEntities.Add(entityHitInfo.entityId);

            if (isAttackerLocal)
            {
                attackingEventParams.Other = world.GetEntity(entityHitInfo.entityId) as EntityAlive;
                attackingEventParams.BlockValue = BlockValue.Air;

                VoxelCaster.SelectEntityHitAsCurrent(entityHitInfo.hitIndex);
                WorldRayHitInfo worldRayHitInfo = Voxel.voxelRayHitInfo.Clone();
                attackingEventParams.StartPosition = worldRayHitInfo.hit.pos - Mathf.Sqrt(worldRayHitInfo.hit.distanceSq) * flyDirection;
                DoHit(worldRayHitInfo, BlockValue.Air);

                if (ItemActionDynamic.ShowDebugDisplayHit)
                {
                    GameObject debugObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    debugObj.transform.position = worldRayHitInfo.hit.pos - Origin.position;
                    debugObj.transform.localScale = new Vector3(.1f, .1f, .1f);
                    debugObj.layer = 2;
                    debugObj.GetComponent<Renderer>().material.color = new Color(1f, 0f, 0f, 1f);
                    ItemActionDynamic.DebugDisplayHits.Add(debugObj);
                }
            }
        }
    }

    private void DoHit(WorldRayHitInfo worldRayHitInfo, BlockValue blockHit)
    {
        ItemActionData prevData = attackingEntity.MinEventContext.ItemActionData;
        ItemValue prevValue = attackingEntity.MinEventContext.ItemValue;
        Vector3 prevStartPosition = attackingEntity.MinEventContext.StartPosition;
        attackingEntity.MinEventContext.Tags |= triggerTags;
        attackingEntity.MinEventContext.ItemActionData = attackingActionData;
        attackingEntity.MinEventContext.ItemValue = attackingItemValue;
        attackingEntity.MinEventContext.StartPosition = attackingEventParams.StartPosition;
        float damageFactor = 1f;
        if (falloffRange < range && worldRayHitInfo.hit.distanceSq > falloffRange * falloffRange)
        {
            damageFactor = 1f - (worldRayHitInfo.hit.distanceSq - falloffRange * falloffRange) / (range * range - falloffRange * falloffRange);
        }

        MinEventTypes eventType = (attackingActionData.indexInEntityOfAction != 1) ? MinEventTypes.onSelfPrimaryActionRayHit : MinEventTypes.onSelfSecondaryActionRayHit;
        attackingItemValue.FireEvent(eventType, attackingEventParams);
        attackingEntity.FireEvent(eventType, false);

        ItemActionAttack.Hit(worldRayHitInfo, attackingEntity.entityId, damageType, blockDamage * damageFactor, entityDamage * damageFactor, 1f, 1f, 0, ItemAction.GetDismemberChance(attackingActionData, worldRayHitInfo), surfaceCategory, null, getBuffActions(), attackingActionData.attackDetails, 1, attackingAction.ActionExp, attackingAction.ActionExpBonusMultiplier, null, null, ItemActionAttack.EnumAttackMode.RealNoHarvesting, null, -1, attackingItemValue);
        attackingEntity.MinEventContext.Tags.Remove(triggerTags);
        attackingEntity.MinEventContext.ItemActionData = prevData;
        attackingEntity.MinEventContext.ItemValue = prevValue;
        attackingEntity.MinEventContext.StartPosition = prevStartPosition;
    }

    [PublicizedFrom(EAccessModifier.Protected)]
    public List<string> getBuffActions()
    {
        return attackingAction.BuffActions;
    }

    public static BlockValue GetBlockHit(World _world, WorldRayHitInfo hitInfo, out Vector3i realBlockPos)
    {
        realBlockPos = hitInfo.hit.blockPos;
        if (GameUtils.IsBlockOrTerrain(hitInfo.tag))
        {
            BlockValue block = BlockValue.Air;
            Vector3i blockPos = hitInfo.hit.blockPos;
            ChunkCluster chunkCluster = _world.ChunkClusters[hitInfo.hit.clrIdx];
            if (chunkCluster == null)
            {
                return BlockValue.Air;
            }

            block = chunkCluster.GetBlock(blockPos);
            if (block.isair && hitInfo.hit.blockValue.Block.IsDistantDecoration && hitInfo.hit.blockValue.damage >= hitInfo.hit.blockValue.Block.MaxDamage - 1)
            {
                block = hitInfo.hit.blockValue;
            }

            if (block.Block == null)
            {
                return BlockValue.Air;
            }

            if (block.ischild)
            {
                blockPos = block.Block.multiBlockPos.GetParentPos(blockPos, block);
                realBlockPos = blockPos;
                block = chunkCluster.GetBlock(blockPos);
                if (block.Block == null)
                {
                    return BlockValue.Air;
                }
            }

            if (block.Equals(BlockValue.Air))
            {
                return BlockValue.Air;
            }

            return block;
        }

        return BlockValue.Air;
    }

    public bool shouldIgnoreTarget(Entity _target, Entity _self)
    {
        if (_target == null)
        {
            return true;
        }
        if (_target.entityId == _self.entityId)
        {
            return true;
        }
        if (_target is EntityDrone)
        {
            return (_target as EntityDrone).isAlly(_self as EntityPlayer);
        }
        EntityPlayer entityPlayer = _self as EntityPlayer;
        EntityPlayer entityPlayer2 = _target as EntityPlayer;
        return entityPlayer != null && entityPlayer2 != null && !entityPlayer.FriendlyFireCheck(entityPlayer2);
    }
}