using KFCommonUtilityLib;
using System.Collections.Generic;
using UnityEngine;

namespace KFCommonUtilityLib.Scripts.Utilities
{
    /// <summary>
    /// This new script make following changes:
    /// - projectile ItemValue fields are reused for custom passive calculation:
    /// -- Meta => launcher ItemClass id
    /// -- SelectedAmmoIndex => launcher action index
    /// -- Activated => launcher data strain perc
    /// -- cosmetics and mods reference launcher ItemValue
    /// -- Quality and Durability is copied from launcher ItemValue
    /// - MinEventParams.itemActionData is set to correct launcher data.
    /// </summary>
    public class CustomProjectileMoveScript : ProjectileMoveScript
    {
        public override void checkCollision()
        {
            GameManager gameManager = GameManager.Instance;
            if (this.firingEntity == null || state != State.Active || gameManager == null)
                return;
            World world = gameManager?.World;
            if (world == null)
            {
                return;
            }
            Vector3 checkPos;
            if (isOnIdealPos)
            {
                checkPos = transform.position + Origin.position;
            }
            else
            {
                checkPos = idealPosition;
            }
            Vector3 dir = checkPos - previousPosition;
            float magnitude = dir.magnitude;
            if (magnitude < 0.04f)
            {
                return;
            }
            EntityAlive firingEntity = (EntityAlive)this.firingEntity;
            Ray ray = new Ray(previousPosition, dir.normalized);
            waterCollisionParticles.CheckCollision(ray.origin, ray.direction, magnitude, (firingEntity != null) ? firingEntity.entityId : (-1));
            int prevLayer = 0;
            if (firingEntity != null && firingEntity.emodel != null)
            {
                prevLayer = firingEntity.GetModelLayer();
                firingEntity.SetModelLayer(2);
            }
            bool flag = Voxel.Raycast(world, ray, magnitude, -538750997, hitMask, 0);
            if (firingEntity != null && firingEntity.emodel != null)
            {
                firingEntity.SetModelLayer(prevLayer);
            }
            if (flag && (GameUtils.IsBlockOrTerrain(Voxel.voxelRayHitInfo.tag) || Voxel.voxelRayHitInfo.tag.StartsWith("E_")))
            {
                if (firingEntity != null && !firingEntity.isEntityRemote)
                {
                    firingEntity.MinEventContext.Other = ItemActionAttack.FindHitEntity(Voxel.voxelRayHitInfo) as EntityAlive;
                    firingEntity.MinEventContext.ItemActionData = actionData;
                    firingEntity.MinEventContext.ItemValue = itemValueLauncher;
                    firingEntity.MinEventContext.Position = Voxel.voxelRayHitInfo.hit.pos;
                    ItemActionAttack.AttackHitInfo attackHitInfo = new ItemActionAttack.AttackHitInfo
                    {
                        WeaponTypeTag = ItemActionAttack.RangedTag
                    };
                    float strainPerc = itemValueProjectile.Activated / (float)byte.MaxValue;
                    MultiActionProjectileRewrites.ProjectileHit(Voxel.voxelRayHitInfo,
                                 ProjectileOwnerID,
                                 EnumDamageTypes.Piercing,
                                 Mathf.Lerp(1f, MultiActionProjectileRewrites.GetProjectileDamageBlock(itemActionProjectile, itemValueProjectile, ItemActionAttack.GetBlockHit(world, Voxel.voxelRayHitInfo), firingEntity, actionData.indexInEntityOfAction), strainPerc),
                                 Mathf.Lerp(1f, MultiActionProjectileRewrites.GetProjectileDamageEntity(itemActionProjectile, itemValueProjectile, firingEntity, actionData.indexInEntityOfAction), strainPerc),
                                 1f,
                                 1f,
                                 MultiActionReversePatches.ProjectileGetValue(PassiveEffects.CriticalChance, itemValueProjectile, itemProjectile.CritChance.Value, firingEntity, null, itemProjectile.ItemTags, true, false),
                                 ItemAction.GetDismemberChance(actionData, Voxel.voxelRayHitInfo),
                                 itemProjectile.MadeOfMaterial.SurfaceCategory,
                                 itemActionProjectile.GetDamageMultiplier(),
                                 getBuffActions(),
                                 attackHitInfo,
                                 1,
                                 itemActionProjectile.ActionExp,
                                 itemActionProjectile.ActionExpBonusMultiplier,
                                 null,
                                 null,
                                 ItemActionAttack.EnumAttackMode.RealNoHarvesting,
                                 null,
                                 -1,
                                 itemValueProjectile,
                                 itemValueLauncher);
                    if (firingEntity.MinEventContext.Other == null)
                    {
                        firingEntity.FireEvent(MinEventTypes.onSelfPrimaryActionMissEntity, false);
                    }
                    firingEntity.FireEvent(MinEventTypes.onProjectileImpact, false);
                    MinEventParams.CachedEventParam.Self = firingEntity;
                    MinEventParams.CachedEventParam.Position = Voxel.voxelRayHitInfo.hit.pos;
                    MinEventParams.CachedEventParam.ItemValue = itemValueProjectile;
                    MinEventParams.CachedEventParam.ItemActionData = actionData;
                    MinEventParams.CachedEventParam.Other = firingEntity.MinEventContext.Other;
                    itemProjectile.FireEvent(MinEventTypes.onProjectileImpact, MinEventParams.CachedEventParam);
                    if (itemActionProjectile.Explosion.ParticleIndex > 0)
                    {
                        Vector3 vector3 = Voxel.voxelRayHitInfo.hit.pos - dir.normalized * 0.1f;
                        Vector3i vector3i = World.worldToBlockPos(vector3);
                        if (!world.GetBlock(vector3i).isair)
                        {
                            BlockFace blockFace;
                            vector3i = Voxel.OneVoxelStep(vector3i, vector3, -dir.normalized, out vector3, out blockFace);
                        }
                        gameManager.ExplosionServer(Voxel.voxelRayHitInfo.hit.clrIdx, vector3, vector3i, Quaternion.identity, itemActionProjectile.Explosion, ProjectileOwnerID, 0f, false, itemValueProjectile);
                        SetState(State.Dead);
                        return;
                    }
                    if (itemProjectile.IsSticky)
                    {
                        GameRandom gameRandom = world.GetGameRandom();
                        if (GameUtils.IsBlockOrTerrain(Voxel.voxelRayHitInfo.tag))
                        {
                            if (gameRandom.RandomFloat < MultiActionReversePatches.ProjectileGetValue(PassiveEffects.ProjectileStickChance, itemValueProjectile, 0.5f, firingEntity, null, itemProjectile.ItemTags | FastTags<TagGroup.Global>.Parse(Voxel.voxelRayHitInfo.fmcHit.blockValue.Block.blockMaterial.SurfaceCategory), true, false))
                            {
                                MultiActionProjectileRewrites.RestoreProjectileValue(itemValueProjectile);
                                ProjectileID = ProjectileManager.AddProjectileItem(transform, -1, Voxel.voxelRayHitInfo.hit.pos, dir.normalized, itemValueProjectile.type);
                                SetState(State.Sticky);
                            }
                            else
                            {
                                gameManager.SpawnParticleEffectServer(new ParticleEffect("impact_metal_on_wood", Voxel.voxelRayHitInfo.hit.pos, Utils.BlockFaceToRotation(Voxel.voxelRayHitInfo.fmcHit.blockFace), 1f, Color.white, string.Format("{0}hit{1}", Voxel.voxelRayHitInfo.fmcHit.blockValue.Block.blockMaterial.SurfaceCategory, itemProjectile.MadeOfMaterial.SurfaceCategory), null), firingEntity.entityId, false, false);
                                SetState(State.Dead);
                            }
                        }
                        else if (gameRandom.RandomFloat < MultiActionReversePatches.ProjectileGetValue(PassiveEffects.ProjectileStickChance, itemValueProjectile, 0.5f, firingEntity, null, itemProjectile.ItemTags, true, false))
                        {
                            MultiActionProjectileRewrites.RestoreProjectileValue(itemValueProjectile);
                            ProjectileID = ProjectileManager.AddProjectileItem(transform, -1, Voxel.voxelRayHitInfo.hit.pos, dir.normalized, itemValueProjectile.type);
                            Utils.SetLayerRecursively(ProjectileManager.GetProjectile(ProjectileID).gameObject, 14, null);
                            SetState(State.Sticky);
                        }
                        else
                        {
                            gameManager.SpawnParticleEffectServer(new ParticleEffect("impact_metal_on_wood", Voxel.voxelRayHitInfo.hit.pos, Utils.BlockFaceToRotation(Voxel.voxelRayHitInfo.fmcHit.blockFace), 1f, Color.white, "bullethitwood", null), firingEntity.entityId, false, false);
                            SetState(State.Dead);
                        }
                    }
                    else
                    {
                        SetState(State.Dead);
                    }
                }
                else
                {
                    SetState(State.Dead);
                }

                if (state == State.Active)
                {
                    SetState(State.Dead);
                }
            }
            previousPosition = checkPos;
        }
    }

    public static class MultiActionProjectileRewrites
    {
        public static void RestoreProjectileValue(ItemValue itemValueProjectile)
        {
            itemValueProjectile.Modifications = ItemValue.emptyItemValueArray;
            itemValueProjectile.CosmeticMods = ItemValue.emptyItemValueArray;
            itemValueProjectile.Quality = 0;
            itemValueProjectile.UseTimes = 0;
            itemValueProjectile.Meta = 0;
            itemValueProjectile.SelectedAmmoTypeIndex = 0;
        }

        public static void ProjectileHit(WorldRayHitInfo hitInfo, int _attackerEntityId, EnumDamageTypes _damageType, float _blockDamage,
                               float _entityDamage, float _staminaDamageMultiplier, float _weaponCondition, float _criticalHitChanceOLD,
                               float _dismemberChance, string _attackingDeviceMadeOf, DamageMultiplier _damageMultiplier,
                               List<string> _buffActions, ItemActionAttack.AttackHitInfo _attackDetails, int _flags = 1, int _actionExp = 0,
                               float _actionExpBonus = 0f, ItemActionAttack rangeCheckedAction = null,
                               Dictionary<string, ItemActionAttack.Bonuses> _toolBonuses = null,
                               ItemActionAttack.EnumAttackMode _attackMode = ItemActionAttack.EnumAttackMode.RealNoHarvesting,
                               Dictionary<string, string> _hitSoundOverrides = null, int ownedEntityId = -1, ItemValue projectileValue = null, ItemValue launcherValue = null)
        {
            if (_attackDetails != null)
            {
                _attackDetails.hitPosition = Vector3i.zero;
                _attackDetails.bKilled = false;
            }
            if (hitInfo == null || hitInfo.tag == null)
            {
                return;
            }
            World world = GameManager.Instance.World;
            bool canHarvest = true;
            if (_attackMode == ItemActionAttack.EnumAttackMode.RealNoHarvestingOrEffects)
            {
                canHarvest = false;
                _attackMode = ItemActionAttack.EnumAttackMode.RealNoHarvesting;
            }
            if (_attackDetails != null)
            {
                _attackDetails.itemsToDrop = null;
                _attackDetails.bBlockHit = false;
                _attackDetails.entityHit = null;
            }
            string blockFaceParticle = null;
            string surfaceCategory = null;
            float lightValueAtBlockPos = 1f;
            Color blockFaceColor = Color.white;
            bool isProtectionApplied = false;
            EntityAlive attackerEntity = world.GetEntity(_attackerEntityId) as EntityAlive;
            bool isHoldingDamageItem = false;
            if (attackerEntity != null)
            {
                if (launcherValue == null)
                {
                    launcherValue = attackerEntity.inventory.holdingItemItemValue;
                }
                isHoldingDamageItem = launcherValue.Equals(attackerEntity.inventory.holdingItemItemValue);
            }
            bool isHitTargetPlayer = true;
            //if hits block or terrain
            if (GameUtils.IsBlockOrTerrain(hitInfo.tag))
            {
                if (ItemAction.ShowDebugDisplayHit)
                {
                    DebugLines.Create(null, attackerEntity.RootTransform, Camera.main.transform.position + Origin.position, hitInfo.hit.pos, new Color(1f, 0.5f, 1f), new Color(1f, 0f, 1f), ItemAction.DebugDisplayHitSize * 2f, ItemAction.DebugDisplayHitSize, ItemAction.DebugDisplayHitTime);
                }
                ChunkCluster hittedChunk = world.ChunkClusters[hitInfo.hit.clrIdx];
                if (hittedChunk == null)
                {
                    return;
                }
                Vector3i hitBlockPos = hitInfo.hit.blockPos;
                BlockValue hitBlockValue = hittedChunk.GetBlock(hitBlockPos);
                if (hitBlockValue.isair && hitInfo.hit.blockValue.Block.IsDistantDecoration && hitInfo.hit.blockValue.damage >= hitInfo.hit.blockValue.Block.MaxDamage - 1)
                {
                    hitBlockValue = hitInfo.hit.blockValue;
                    world.SetBlockRPC(hitBlockPos, hitBlockValue);
                }
                Block hitBlock = hitBlockValue.Block;
                if (hitBlock == null)
                {
                    return;
                }
                if (hitBlockValue.ischild)
                {
                    hitBlockPos = hitBlock.multiBlockPos.GetParentPos(hitBlockPos, hitBlockValue);
                    hitBlockValue = hittedChunk.GetBlock(hitBlockPos);
                    hitBlock = hitBlockValue.Block;
                    if (hitBlock == null)
                    {
                        return;
                    }
                }
                if (hitBlockValue.isair)
                {
                    return;
                }
                float landProtectionModifier = 0;
                if (!world.IsWithinTraderArea(hitInfo.hit.blockPos) && hitBlock.blockMaterial.id != "Mbedrock")
                {
                    landProtectionModifier = world.GetLandProtectionHardnessModifier(hitInfo.hit.blockPos, attackerEntity, world.GetGameManager().GetPersistentLocalPlayer());
                }
                if (landProtectionModifier != 1f)
                {
                    if (attackerEntity && _attackMode != ItemActionAttack.EnumAttackMode.Simulate && attackerEntity is EntityPlayer && !launcherValue.ItemClass.ignoreKeystoneSound && !launcherValue.ToBlockValue().Block.IgnoreKeystoneOverlay)
                    {
                        attackerEntity.PlayOneShot("keystone_impact_overlay", false);
                    }
                    if (landProtectionModifier < 1f)
                    {
                        isProtectionApplied = true;
                    }
                }
                if (hitBlockPos != _attackDetails.hitPosition || landProtectionModifier != _attackDetails.hardnessScale || hitBlockValue.type != _attackDetails.blockBeingDamaged.type || (isHoldingDamageItem && projectileValue.SelectedAmmoTypeIndex != _attackDetails.ammoIndex))
                {
                    float finalHardness = Mathf.Max(hitBlock.GetHardness(), 0.1f) * landProtectionModifier;
                    float finalBlockDamage = _blockDamage * ((_damageMultiplier != null) ? _damageMultiplier.Get(hitBlock.blockMaterial.DamageCategory) : 1f);
                    if (attackerEntity)
                    {
                        finalBlockDamage *= attackerEntity.GetBlockDamageScale();
                    }
                    if (_toolBonuses != null && _toolBonuses.Count > 0)
                    {
                        finalBlockDamage *= calculateHarvestToolDamageBonus(_toolBonuses, hitBlock.itemsToDrop);
                        _attackDetails.bHarvestTool = true;
                    }
                    _attackDetails.damagePerHit = isProtectionApplied ? 0f : (finalBlockDamage / finalHardness);
                    _attackDetails.damage = 0f;
                    _attackDetails.hardnessScale = landProtectionModifier;
                    _attackDetails.hitPosition = hitBlockPos;
                    _attackDetails.blockBeingDamaged = hitBlockValue;
                    if (isHoldingDamageItem)
                    {
                        _attackDetails.ammoIndex = projectileValue.SelectedAmmoTypeIndex;
                    }
                }
                _attackDetails.raycastHitPosition = hitInfo.hit.blockPos;
                Block fmcHitBlock = hitInfo.fmcHit.blockValue.Block;
                lightValueAtBlockPos = world.GetLightBrightness(hitInfo.fmcHit.blockPos);
                blockFaceColor = fmcHitBlock.GetColorForSide(hitInfo.fmcHit.blockValue, hitInfo.fmcHit.blockFace);
                blockFaceParticle = fmcHitBlock.GetParticleForSide(hitInfo.fmcHit.blockValue, hitInfo.fmcHit.blockFace);
                MaterialBlock materialForSide = fmcHitBlock.GetMaterialForSide(hitInfo.fmcHit.blockValue, hitInfo.fmcHit.blockFace);
                surfaceCategory = materialForSide.SurfaceCategory;
                float modifiedBlockDamage = _attackDetails.damagePerHit * _staminaDamageMultiplier;
                if (attackerEntity)
                {
                    string blockFaceDamageCategory = materialForSide.DamageCategory ?? string.Empty;
                    modifiedBlockDamage = (int)MultiActionReversePatches.ProjectileGetValue(PassiveEffects.DamageModifier, projectileValue, modifiedBlockDamage, attackerEntity, null, FastTags<TagGroup.Global>.Parse(blockFaceDamageCategory) | _attackDetails.WeaponTypeTag | hitInfo.fmcHit.blockValue.Block.Tags, true, false);
                }
                modifiedBlockDamage = ItemActionAttack.DegradationModifier(modifiedBlockDamage, _weaponCondition);
                modifiedBlockDamage = isProtectionApplied ? 0f : Utils.FastMax(1f, modifiedBlockDamage);
                _attackDetails.damage += modifiedBlockDamage;
                _attackDetails.bKilled = false;
                _attackDetails.damageTotalOfTarget = hitBlockValue.damage + _attackDetails.damage;
                if (_attackDetails.damage > 0f)
                {
                    BlockFace blockFaceFromHitInfo = GameUtils.GetBlockFaceFromHitInfo(hitBlockPos, hitBlockValue, hitInfo.hitCollider, hitInfo.hitTriangleIdx, out _, out _);
                    int blockFaceTexture = hittedChunk.GetBlockFaceTexture(hitBlockPos, blockFaceFromHitInfo, 0);
                    int blockCurDamage = hitBlockValue.damage;
                    bool isBlockBroken = blockCurDamage >= hitBlock.MaxDamage;
                    int ownerAttackerID = ((ownedEntityId != -1 && ownedEntityId != -2) ? ownedEntityId : _attackerEntityId);
                    int blockNextDamage = (_attackMode != ItemActionAttack.EnumAttackMode.Simulate) ? hitBlock.DamageBlock(world, hittedChunk.ClusterIdx, hitBlockPos, hitBlockValue, (int)_attackDetails.damage, ownerAttackerID, _attackDetails, _attackDetails.bHarvestTool, false) : 0;
                    if (blockNextDamage == 0)
                    {
                        _attackDetails.damage = 0f;
                    }
                    else
                    {
                        _attackDetails.damage -= blockNextDamage - blockCurDamage;
                    }
                    if (_attackMode != ItemActionAttack.EnumAttackMode.Simulate && canHarvest && attackerEntity is EntityPlayerLocal && blockFaceTexture > 0 && hitBlock.MeshIndex == 0 && blockNextDamage >= hitBlock.MaxDamage * 1f)
                    {
                        ParticleEffect particleEffect = new ParticleEffect("paint_block", hitInfo.fmcHit.pos - Origin.position, Utils.BlockFaceToRotation(hitInfo.fmcHit.blockFace), lightValueAtBlockPos, blockFaceColor, null, null)
                        {
                            opqueTextureId = BlockTextureData.list[blockFaceTexture].TextureID
                        };
                        GameManager.Instance.SpawnParticleEffectClient(particleEffect, _attackerEntityId, false, false);
                    }
                    _attackDetails.damageGiven = ((!isBlockBroken) ? (blockNextDamage - blockCurDamage) : 0);
                    _attackDetails.damageMax = hitBlock.MaxDamage;
                    _attackDetails.bKilled = !isBlockBroken && blockNextDamage >= hitBlock.MaxDamage;
                    _attackDetails.itemsToDrop = hitBlock.itemsToDrop;
                    _attackDetails.bBlockHit = true;
                    _attackDetails.materialCategory = hitBlock.blockMaterial.SurfaceCategory;
                    if (attackerEntity != null && _attackMode != ItemActionAttack.EnumAttackMode.Simulate)
                    {
                        attackerEntity.MinEventContext.BlockValue = hitBlockValue;
                        attackerEntity.MinEventContext.Tags = hitBlock.Tags;
                        if (_attackDetails.bKilled)
                        {
                            attackerEntity.FireEvent(MinEventTypes.onSelfDestroyedBlock, isHoldingDamageItem);
                            attackerEntity.NotifyDestroyedBlock(_attackDetails);
                        }
                        else
                        {
                            attackerEntity.FireEvent(MinEventTypes.onSelfDamagedBlock, isHoldingDamageItem);
                        }
                    }
                }
            }
            else if (hitInfo.tag.StartsWith("E_"))
            {
                Entity hitEntity = ItemActionAttack.FindHitEntityNoTagCheck(hitInfo, out string hitBodyPart);
                if (hitEntity == null)
                {
                    return;
                }
                if (hitEntity.entityId == _attackerEntityId)
                {
                    return;
                }
                if (!hitEntity.CanDamageEntity(_attackerEntityId))
                {
                    return;
                }
                EntityAlive hitEntityAlive = hitEntity as EntityAlive;
                DamageSourceEntity damageSourceEntity = new DamageSourceEntity(EnumDamageSource.External, _damageType, _attackerEntityId, hitInfo.ray.direction, hitInfo.transform.name, hitInfo.hit.pos, Voxel.phyxRaycastHit.textureCoord);
                damageSourceEntity.AttackingItem = projectileValue;
                damageSourceEntity.DismemberChance = _dismemberChance;
                damageSourceEntity.CreatorEntityId = ownedEntityId;
                bool isCriticalHit = _attackDetails.isCriticalHit;
                int finalEntityDamage = (int)_entityDamage;
                if (attackerEntity != null && hitEntityAlive != null)
                {
                    FastTags<TagGroup.Global> equipmentTags = FastTags<TagGroup.Global>.none;
                    if (hitEntityAlive.Health > 0)
                    {
                        equipmentTags = FastTags<TagGroup.Global>.Parse(damageSourceEntity.GetEntityDamageEquipmentSlotGroup(hitEntityAlive).ToStringCached());
                        equipmentTags |= DamagePatches.GetBodyPartTags(damageSourceEntity.GetEntityDamageBodyPart(hitEntityAlive));
                    }
                    finalEntityDamage = (int)MultiActionReversePatches.ProjectileGetValue(PassiveEffects.DamageModifier, projectileValue, finalEntityDamage, attackerEntity, null, equipmentTags | _attackDetails.WeaponTypeTag | hitEntityAlive.EntityClass.Tags, true, false);
                    finalEntityDamage = (int)MultiActionReversePatches.ProjectileGetValue(PassiveEffects.InternalDamageModifier, projectileValue, finalEntityDamage, hitEntityAlive, null, equipmentTags | projectileValue.ItemClass.ItemTags, true, false);
                }
                if (!hitEntityAlive || hitEntityAlive.Health > 0)
                {
                    finalEntityDamage = Utils.FastMax(1, ItemActionAttack.difficultyModifier(finalEntityDamage, world.GetEntity(_attackerEntityId), hitEntity));
                }
                else if (_toolBonuses != null)
                {
                    finalEntityDamage = (int)(finalEntityDamage * calculateHarvestToolDamageBonus(_toolBonuses, EntityClass.list[hitEntity.entityClass].itemsToDrop));
                }
                //Log.Out("Final entity damage: " + finalEntityDamage);
                bool isAlreadyDead = hitEntity.IsDead();
                int deathHealth = (hitEntityAlive != null) ? hitEntityAlive.DeathHealth : 0;
                if (_attackMode != ItemActionAttack.EnumAttackMode.Simulate)
                {
                    if (attackerEntity != null)
                    {
                        MinEventParams minEventContext = attackerEntity.MinEventContext;
                        minEventContext.Other = hitEntityAlive;
                        minEventContext.StartPosition = hitInfo.ray.origin;
                    }
                    if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && (attackerEntity as EntityPlayer == null || !attackerEntity.isEntityRemote) && hitEntity.isEntityRemote && rangeCheckedAction != null)
                    {
                        EntityPlayer hitPlayer = hitEntity as EntityPlayer;
                        if (hitPlayer != null)
                        {
                            isHitTargetPlayer = false;
                            Ray lookRay = attackerEntity.GetLookRay();
                            lookRay.origin -= lookRay.direction * 0.15f;
                            float range = Utils.FastMax(rangeCheckedAction.Range, rangeCheckedAction.BlockRange) * ItemActionAttack.attackRangeMultiplier;
                            string hitTransformPath = null;
                            List<string> list_buffs = _buffActions;
                            if (list_buffs != null)
                            {
                                if (hitEntityAlive)
                                {
                                    hitTransformPath = (hitBodyPart != null) ? GameUtils.GetChildTransformPath(hitEntity.transform, hitInfo.transform) : null;
                                }
                                else
                                {
                                    list_buffs = null;
                                }
                            }
                            if (attackerEntity != null)
                            {
                                attackerEntity.FireEvent(MinEventTypes.onSelfAttackedOther, isHoldingDamageItem);
                                if (hitEntityAlive != null && hitEntityAlive.RecordedDamage.Strength > 0)
                                {
                                    attackerEntity.FireEvent(MinEventTypes.onSelfDamagedOther, isHoldingDamageItem);
                                }
                            }
                            if (!isAlreadyDead && hitEntity.IsDead() && attackerEntity != null)
                            {
                                attackerEntity.FireEvent(MinEventTypes.onSelfKilledOther, isHoldingDamageItem);
                            }
                            if (hitEntityAlive && hitEntityAlive.RecordedDamage.ArmorDamage > hitEntityAlive.RecordedDamage.Strength)
                            {
                                surfaceCategory = "metal";
                            }
                            else
                            {
                                surfaceCategory = EntityClass.list[hitEntity.entityClass].Properties.Values["SurfaceCategory"];
                            }
                            blockFaceParticle = surfaceCategory;
                            lightValueAtBlockPos = hitEntity.GetLightBrightness();
                            string hitParticle = string.Format("impact_{0}_on_{1}", _attackingDeviceMadeOf, blockFaceParticle);
                            string hitSound = (surfaceCategory != null) ? string.Format("{0}hit{1}", _attackingDeviceMadeOf, surfaceCategory) : null;
                            if (_hitSoundOverrides != null && _hitSoundOverrides.ContainsKey(surfaceCategory))
                            {
                                hitSound = _hitSoundOverrides[surfaceCategory];
                            }
                            ParticleEffect particleEffect2 = new ParticleEffect(hitParticle, hitInfo.fmcHit.pos, Utils.BlockFaceToRotation(hitInfo.fmcHit.blockFace), lightValueAtBlockPos, blockFaceColor, hitSound, null);
                            hitPlayer.ServerNetSendRangeCheckedDamage(lookRay.origin, range, damageSourceEntity, finalEntityDamage, isCriticalHit, list_buffs, hitTransformPath, particleEffect2);
                        }
                    }
                    if (isHitTargetPlayer)
                    {
                        int damageDealt = hitEntity.DamageEntity(damageSourceEntity, finalEntityDamage, isCriticalHit, 1f);
                        if (damageDealt != -1 && attackerEntity)
                        {
                            MinEventParams attackerMinEventParams = attackerEntity.MinEventContext;
                            attackerMinEventParams.Other = hitEntityAlive;
                            attackerMinEventParams.StartPosition = hitInfo.ray.origin;
                            if (ownedEntityId != -1)
                            {
                                launcherValue.FireEvent(MinEventTypes.onSelfAttackedOther, attackerEntity.MinEventContext);
                            }
                            attackerEntity.FireEvent(MinEventTypes.onSelfAttackedOther, isHoldingDamageItem);
                            if (hitEntityAlive && hitEntityAlive.RecordedDamage.Strength > 0)
                            {
                                attackerEntity.FireEvent(MinEventTypes.onSelfDamagedOther, isHoldingDamageItem);
                            }
                        }
                        if (!isAlreadyDead && hitEntity.IsDead() && attackerEntity)
                        {
                            attackerEntity.FireEvent(MinEventTypes.onSelfKilledOther, isHoldingDamageItem);
                        }
                        if (damageDealt != -1 && hitEntityAlive && _buffActions != null && _buffActions.Count > 0)
                        {
                            for (int i = 0; i < _buffActions.Count; i++)
                            {
                                BuffClass buff = BuffManager.GetBuff(_buffActions[i]);
                                if (buff != null)
                                {
                                    float bufProcChance = MultiActionReversePatches.ProjectileGetValue(PassiveEffects.BuffProcChance, null, 1f, attackerEntity, null, FastTags<TagGroup.Global>.Parse(buff.Name), true, false);
                                    if (hitEntityAlive.rand.RandomFloat <= bufProcChance)
                                    {
                                        hitEntityAlive.Buffs.AddBuff(_buffActions[i], attackerEntity.entityId, true, false, -1f);
                                    }
                                }
                            }
                        }
                    }
                }
                if (hitEntityAlive && hitEntityAlive.RecordedDamage.ArmorDamage > hitEntityAlive.RecordedDamage.Strength)
                {
                    surfaceCategory = "metal";
                }
                else
                {
                    surfaceCategory = EntityClass.list[hitEntity.entityClass].Properties.Values["SurfaceCategory"];
                }
                blockFaceParticle = surfaceCategory;
                lightValueAtBlockPos = hitEntity.GetLightBrightness();
                EntityPlayer attackerPlayer = attackerEntity as EntityPlayer;
                if (attackerPlayer)
                {
                    if (isAlreadyDead && hitEntity.IsDead() && hitEntityAlive && hitEntityAlive.DeathHealth + finalEntityDamage > -1 * EntityClass.list[hitEntity.entityClass].DeadBodyHitPoints)
                    {
                        _attackDetails.damageTotalOfTarget = (float)(-1 * hitEntityAlive.DeathHealth);
                        _attackDetails.damageGiven = deathHealth + Mathf.Min(EntityClass.list[hitEntity.entityClass].DeadBodyHitPoints, Mathf.Abs(hitEntityAlive.DeathHealth));
                        _attackDetails.damageMax = EntityClass.list[hitEntity.entityClass].DeadBodyHitPoints;
                        _attackDetails.bKilled = -1 * hitEntityAlive.DeathHealth >= EntityClass.list[hitEntity.entityClass].DeadBodyHitPoints;
                        _attackDetails.itemsToDrop = EntityClass.list[hitEntity.entityClass].itemsToDrop;
                        _attackDetails.entityHit = hitEntity;
                        _attackDetails.materialCategory = surfaceCategory;
                    }
                    if (!isAlreadyDead && (hitEntityAlive.IsDead() || hitEntityAlive.Health <= 0) && EntityClass.list.ContainsKey(hitEntity.entityClass))
                    {
                        if ((_flags & 2) > 0)
                        {
                            float trapXP = MultiActionReversePatches.ProjectileGetValue(PassiveEffects.ElectricalTrapXP, attackerPlayer.inventory.holdingItemItemValue, 0f, attackerPlayer, null, default, true, false);
                            if (trapXP > 0f)
                            {
                                attackerPlayer.AddKillXP(hitEntityAlive, trapXP);
                            }
                        }
                        else
                        {
                            attackerPlayer.AddKillXP(hitEntityAlive, 1f);
                        }
                    }
                }
                if (hitEntity is EntityDrone)
                {
                    _attackDetails.entityHit = hitEntity;
                }
            }
            if ((_flags & 8) > 0)
            {
                canHarvest = false;
            }
            if (isHitTargetPlayer && _attackMode != ItemActionAttack.EnumAttackMode.Simulate && canHarvest && blockFaceParticle != null && ((_attackDetails.bBlockHit && !_attackDetails.bKilled) || !_attackDetails.bBlockHit))
            {
                string hitParticle = string.Format("impact_{0}_on_{1}", _attackingDeviceMadeOf, blockFaceParticle);
                if (_attackMode == ItemActionAttack.EnumAttackMode.RealAndHarvesting && (_flags & 4) > 0 && ParticleEffect.IsAvailable(hitParticle + "_harvest"))
                {
                    hitParticle += "_harvest";
                }
                string hitSound = (surfaceCategory != null) ? string.Format("{0}hit{1}", _attackingDeviceMadeOf, surfaceCategory) : null;
                if (_hitSoundOverrides != null && _hitSoundOverrides.ContainsKey(surfaceCategory))
                {
                    hitSound = _hitSoundOverrides[surfaceCategory];
                }
                world.GetGameManager().SpawnParticleEffectServer(new ParticleEffect(hitParticle, hitInfo.fmcHit.pos, Utils.BlockFaceToRotation(hitInfo.fmcHit.blockFace), lightValueAtBlockPos, blockFaceColor, hitSound, null), _attackerEntityId, false, true);
            }
            if ((_flags & 1) > 0 && attackerEntity != null && attackerEntity.inventory != null)
            {
                attackerEntity.inventory.CallOnToolbeltChangedInternal();
            }
        }

        private static float calculateHarvestToolDamageBonus(Dictionary<string, ItemActionAttack.Bonuses> _toolBonuses, Dictionary<EnumDropEvent, List<Block.SItemDropProb>> _harvestItems)
        {
            if (!_harvestItems.ContainsKey(EnumDropEvent.Harvest))
            {
                return 1f;
            }
            List<Block.SItemDropProb> list = _harvestItems[EnumDropEvent.Harvest];
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].toolCategory != null && _toolBonuses.ContainsKey(list[i].toolCategory))
                {
                    return _toolBonuses[list[i].toolCategory].Damage;
                }
            }
            return 1f;
        }

        public static float GetProjectileDamageBlock(this ItemActionAttack self, ItemValue _itemValue, BlockValue _blockValue, EntityAlive _holdingEntity = null, int actionIndex = 0)
        {
            FastTags<TagGroup.Global> tmpTag = ((actionIndex != 1) ? ItemActionAttack.PrimaryTag : ItemActionAttack.SecondaryTag);
            ItemClass launcherClass = ItemClass.GetForId(_itemValue.Meta);
            tmpTag |= ((launcherClass == null) ? ItemActionAttack.MeleeTag : launcherClass.ItemTags);
            if (_holdingEntity != null)
            {
                tmpTag |= _holdingEntity.CurrentStanceTag | _holdingEntity.CurrentMovementTag;
            }

            tmpTag |= _blockValue.Block.Tags;
            float value = MultiActionReversePatches.ProjectileGetValue(PassiveEffects.BlockDamage, _itemValue, self.damageBlock, _holdingEntity, null, tmpTag, true, false)/* * GetProjectileBlockDamagePerc(_itemValue, _holdingEntity)*/;
            //Log.Out($"block damage {value} base damage {self.GetBaseDamageBlock(null)} action index {actionIndex} launcher {launcherClass.Name} projectile {_itemValue.ItemClass.Name}");
            return value;
        }

        public static float GetProjectileDamageEntity(this ItemActionAttack self, ItemValue _itemValue, EntityAlive _holdingEntity = null, int actionIndex = 0)
        {
            FastTags<TagGroup.Global> tmpTag = ((actionIndex != 1) ? ItemActionAttack.PrimaryTag : ItemActionAttack.SecondaryTag);
            ItemClass launcherClass = ItemClass.GetForId(_itemValue.Meta);
            tmpTag |= ((launcherClass == null) ? ItemActionAttack.MeleeTag : launcherClass.ItemTags);
            if (_holdingEntity != null)
            {
                tmpTag |= _holdingEntity.CurrentStanceTag | _holdingEntity.CurrentMovementTag;
            }

            var res = MultiActionReversePatches.ProjectileGetValue(PassiveEffects.EntityDamage, _itemValue, self.damageEntity, _holdingEntity, null, tmpTag, true, false)/* * GetProjectileEntityDamagePerc(_itemValue, _holdingEntity)*/;
#if DEBUG
            Log.Out($"get projectile damage entity for action index {actionIndex}, item {launcherClass.Name}, result {res}");
#endif
            return res;
        }

        //public static float GetProjectileBlockDamagePerc(ItemValue _itemValue, EntityAlive _holdingEntity)
        //{
        //    float value = MultiActionReversePatches.ProjectileGetValue(CustomEnums.ProjectileImpactDamagePercentBlock, _itemValue, 1, _holdingEntity, null);
        //    //Log.Out("Block damage perc: " +  value);
        //    return value;
        //}

        //public static float GetProjectileEntityDamagePerc(ItemValue _itemValue, EntityAlive _holdingEntity)
        //{
        //    float value = MultiActionReversePatches.ProjectileGetValue(CustomEnums.ProjectileImpactDamagePercentEntity, _itemValue, 1, _holdingEntity, null);
        //    //Log.Out("Entity damage perc: " +  value);
        //    return value;
        //}

        public static void ProjectileValueModifyValue(this ItemValue _projectileItemValue, EntityAlive _entity, ItemValue _originalItemValue, PassiveEffects _passiveEffect, ref float _originalValue, ref float _perc_value, FastTags<TagGroup.Global> _tags, bool _useMods = true, bool _useDurability = false)
        {
            if (_originalItemValue != null)
            {
                Log.Warning($"original item value present: item {_originalItemValue.ItemClass.Name}");
                return;
            }
            int seed = MinEventParams.CachedEventParam.Seed;
            if (_entity != null)
            {
                seed = _entity.MinEventContext.Seed;
            }

            ItemClass launcherClass = ItemClass.GetForId(_projectileItemValue.Meta);
            int actionIndex = _projectileItemValue.SelectedAmmoTypeIndex;
            if (launcherClass != null)
            {
                if (launcherClass.Actions != null && launcherClass.Actions.Length != 0 && launcherClass.Actions[actionIndex] is ItemActionRanged)
                {
                    ItemClass ammoClass = _projectileItemValue.ItemClass;
                    if (ammoClass != null && ammoClass.Effects != null)
                    {
                        ammoClass.Effects.ModifyValue(_entity, _passiveEffect, ref _originalValue, ref _perc_value, 0f, _tags);
                    }
                }

                if (launcherClass.Effects != null)
                {
                    MinEventParams.CachedEventParam.Seed = (int)_projectileItemValue.Seed + (int)((_projectileItemValue.Seed != 0) ? _passiveEffect : PassiveEffects.None);
                    if (_entity != null)
                    {
                        _entity.MinEventContext.Seed = MinEventParams.CachedEventParam.Seed;
                    }

                    float prevOriginal = _originalValue;
                    launcherClass.Effects.ModifyValue(_entity, _passiveEffect, ref _originalValue, ref _perc_value, _projectileItemValue.Quality, _tags);
                    if (_useDurability)
                    {
                        float percentUsesLeft = _projectileItemValue.PercentUsesLeft;
                        switch (_passiveEffect)
                        {
                            case PassiveEffects.PhysicalDamageResist:
                                if (percentUsesLeft < 0.5f)
                                {
                                    float diff = _originalValue - prevOriginal;
                                    _originalValue = prevOriginal + diff * percentUsesLeft * 2f;
                                }

                                break;
                            case PassiveEffects.ElementalDamageResist:
                                if (percentUsesLeft < 0.5f)
                                {
                                    float diff = _originalValue - prevOriginal;
                                    _originalValue = prevOriginal + diff * percentUsesLeft * 2f;
                                }

                                break;
                            case PassiveEffects.BuffResistance:
                                if (percentUsesLeft < 0.5f)
                                {
                                    float diff = _originalValue - prevOriginal;
                                    _originalValue = prevOriginal + diff * percentUsesLeft * 2f;
                                }

                                break;
                        }
                    }
                }
            }
            else
            {
                Log.Warning($"launcher class not found: item id{_projectileItemValue.Meta}");
            }

            if (_useMods)
            {
                for (int i = 0; i < _projectileItemValue.CosmeticMods.Length; i++)
                {
                    if (_projectileItemValue.CosmeticMods[i] != null && _projectileItemValue.CosmeticMods[i].ItemClass is ItemClassModifier && !MultiActionManager.ShouldExcludePassive(_projectileItemValue.type, _projectileItemValue.CosmeticMods[i].type, actionIndex))
                    {
                        _projectileItemValue.CosmeticMods[i].ModifyValue(_entity, _projectileItemValue, _passiveEffect, ref _originalValue, ref _perc_value, _tags);
                    }
                }

                for (int i = 0; i < _projectileItemValue.Modifications.Length; i++)
                {
                    if (_projectileItemValue.Modifications[i] != null && _projectileItemValue.Modifications[i].ItemClass is ItemClassModifier && !MultiActionManager.ShouldExcludePassive(_projectileItemValue.type, _projectileItemValue.Modifications[i].type, actionIndex))
                    {
                        _projectileItemValue.Modifications[i].ModifyValue(_entity, _projectileItemValue, _passiveEffect, ref _originalValue, ref _perc_value, _tags);
                    }
                }
            }
        }
    }
}