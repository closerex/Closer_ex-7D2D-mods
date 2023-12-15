using System.Collections.Generic;
using UnityEngine;

namespace KFCommonUtilityLib.Scripts.Utilities
{
    public static class Rewrites
    {
        public static void Hit(WorldRayHitInfo hitInfo, int _attackerEntityId, EnumDamageTypes _damageType, float _blockDamage,
                               float _entityDamage, float _staminaDamageMultiplier, float _weaponCondition, float _criticalHitChanceOLD,
                               float _dismemberChance, string _attackingDeviceMadeOf, DamageMultiplier _damageMultiplier,
                               List<string> _buffActions, ItemActionAttack.AttackHitInfo _attackDetails, int _flags = 1, int _actionExp = 0,
                               float _actionExpBonus = 0f, ItemActionAttack rangeCheckedAction = null,
                               Dictionary<string, ItemActionAttack.Bonuses> _toolBonuses = null,
                               ItemActionAttack.EnumAttackMode _attackMode = ItemActionAttack.EnumAttackMode.RealNoHarvesting,
                               Dictionary<string, string> _hitSoundOverrides = null, int ownedEntityId = -1, ItemValue damagingItemValue = null)
        {
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
                if (damagingItemValue == null)
                {
                    damagingItemValue = attackerEntity.inventory.holdingItemItemValue;
                }
                isHoldingDamageItem = damagingItemValue.Equals(attackerEntity.inventory.holdingItemItemValue);
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
                    if (attackerEntity && _attackMode != ItemActionAttack.EnumAttackMode.Simulate && attackerEntity is EntityPlayer && !damagingItemValue.ItemClass.ignoreKeystoneSound && !damagingItemValue.ToBlockValue().Block.IgnoreKeystoneOverlay)
                    {
                        attackerEntity.PlayOneShot("keystone_impact_overlay", false);
                    }
                    if (landProtectionModifier < 1f)
                    {
                        isProtectionApplied = true;
                    }
                }
                if (hitBlockPos != _attackDetails.hitPosition || landProtectionModifier != _attackDetails.hardnessScale || hitBlockValue.type != _attackDetails.blockBeingDamaged.type || (isHoldingDamageItem && damagingItemValue.SelectedAmmoTypeIndex != _attackDetails.ammoIndex))
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
                        _attackDetails.ammoIndex = damagingItemValue.SelectedAmmoTypeIndex;
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
                    modifiedBlockDamage = (int)EffectManager.GetValue(PassiveEffects.DamageModifier, damagingItemValue, modifiedBlockDamage, attackerEntity, null, FastTags.Parse(blockFaceDamageCategory) | _attackDetails.WeaponTypeTag | hitInfo.fmcHit.blockValue.Block.Tags, true, true, true, true, 1, true, false);
                }
                modifiedBlockDamage = ItemActionAttack.DegradationModifier(modifiedBlockDamage, _weaponCondition);
                modifiedBlockDamage = isProtectionApplied ? 0f : Utils.FastMax(1f, modifiedBlockDamage);
                _attackDetails.damage += modifiedBlockDamage;
                _attackDetails.bKilled = false;
                _attackDetails.damageTotalOfTarget = hitBlockValue.damage + _attackDetails.damage;
                if (_attackDetails.damage > 0f)
                {
                    BlockFace blockFaceFromHitInfo = GameUtils.GetBlockFaceFromHitInfo(hitBlockPos, hitBlockValue, hitInfo.hitCollider, hitInfo.hitTriangleIdx, out _, out _);
                    int blockFaceTexture = hittedChunk.GetBlockFaceTexture(hitBlockPos, blockFaceFromHitInfo);
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
                        attackerEntity.MinEventContext.ItemValue = damagingItemValue;
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
                damageSourceEntity.AttackingItem = damagingItemValue;
                damageSourceEntity.DismemberChance = _dismemberChance;
                damageSourceEntity.CreatorEntityId = ownedEntityId;
                bool isCriticalHit = _attackDetails.isCriticalHit;
                int finalEntityDamage = (int)_entityDamage;
                if (attackerEntity != null && hitEntityAlive != null)
                {
                    FastTags equipmentTags = FastTags.none;
                    if (hitEntityAlive.Health > 0)
                    {
                        equipmentTags = FastTags.Parse(damageSourceEntity.GetEntityDamageEquipmentSlotGroup(hitEntityAlive).ToStringCached());
                    }
                    finalEntityDamage = (int)EffectManager.GetValue(PassiveEffects.DamageModifier, damagingItemValue, finalEntityDamage, attackerEntity, null, equipmentTags | _attackDetails.WeaponTypeTag | hitEntityAlive.EntityClass.Tags, true, true, true, true, 1, true, false);
                    finalEntityDamage = (int)EffectManager.GetValue(PassiveEffects.InternalDamageModifier, damagingItemValue, finalEntityDamage, hitEntityAlive, null, equipmentTags | damagingItemValue.ItemClass.ItemTags, true, true, true, true, 1, true, false);
                }
                if (!hitEntityAlive || hitEntityAlive.Health > 0)
                {
                    finalEntityDamage = Utils.FastMax(1, difficultyModifier(finalEntityDamage, world.GetEntity(_attackerEntityId), hitEntity));
                }
                else if (_toolBonuses != null)
                {
                    finalEntityDamage = (int)(finalEntityDamage * calculateHarvestToolDamageBonus(_toolBonuses, EntityClass.list[hitEntity.entityClass].itemsToDrop));
                }
                bool isAlreadyDead = hitEntity.IsDead();
                int deathHealth = (hitEntityAlive != null) ? hitEntityAlive.DeathHealth : 0;
                if (_attackMode != ItemActionAttack.EnumAttackMode.Simulate)
                {
                    if (attackerEntity != null)
                    {
                        MinEventParams minEventContext = attackerEntity.MinEventContext;
                        minEventContext.Other = hitEntityAlive;
                        minEventContext.ItemValue = damagingItemValue;
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
                            attackerMinEventParams.ItemValue = damagingItemValue;
                            attackerMinEventParams.StartPosition = hitInfo.ray.origin;
                            if (ownedEntityId != -1)
                            {
                                damagingItemValue.FireEvent(MinEventTypes.onSelfAttackedOther, attackerEntity.MinEventContext);
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
                                    float bufProcChance = EffectManager.GetValue(PassiveEffects.BuffProcChance, null, 1f, attackerEntity, null, FastTags.Parse(buff.Name), true, true, true, true, 1, true, false);
                                    if (hitEntityAlive.rand.RandomFloat <= bufProcChance)
                                    {
                                        hitEntityAlive.Buffs.AddBuff(_buffActions[i], attackerEntity.entityId, true, false, false, -1f);
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
                            float trapXP = EffectManager.GetValue(PassiveEffects.ElectricalTrapXP, attackerPlayer.inventory.holdingItemItemValue, 0f, attackerPlayer, null, default, true, true, true, true, 1, true, false);
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

        private static int difficultyModifier(int _strength, Entity _attacker, Entity _target)
        {
            if (_attacker == null || _target == null)
            {
                return _strength;
            }
            if (_attacker.IsClientControlled() && _target.IsClientControlled())
            {
                return _strength;
            }
            if (!_attacker.IsClientControlled() && !_target.IsClientControlled())
            {
                return _strength;
            }
            int difficulty = GameStats.GetInt(EnumGameStats.GameDifficulty);
            if (_attacker.IsClientControlled())
            {
                switch (difficulty)
                {
                    case 0:
                        _strength = Mathf.RoundToInt(_strength * 2f);
                        break;
                    case 1:
                        _strength = Mathf.RoundToInt(_strength * 1.5f);
                        break;
                    case 3:
                        _strength = Mathf.RoundToInt(_strength * 0.83f);
                        break;
                    case 4:
                        _strength = Mathf.RoundToInt(_strength * 0.66f);
                        break;
                    case 5:
                        _strength = Mathf.RoundToInt(_strength * 0.5f);
                        break;
                }
            }
            else
            {
                switch (difficulty)
                {
                    case 0:
                        _strength = Mathf.RoundToInt(_strength * 0.5f);
                        break;
                    case 1:
                        _strength = Mathf.RoundToInt(_strength * 0.75f);
                        break;
                    case 3:
                        _strength = Mathf.RoundToInt(_strength * 1.5f);
                        break;
                    case 4:
                        _strength = Mathf.RoundToInt(_strength * 2f);
                        break;
                    case 5:
                        _strength = Mathf.RoundToInt(_strength * 2.5f);
                        break;
                }
            }
            return _strength;
        }
    }
}