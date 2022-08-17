using System;
using System.Collections.Generic;
using UnityEngine;

public class VPRaycastWeapon : VehicleWeaponBase
{
    protected ItemValue boundItemValue;
    protected FastTags tags;
    protected Transform raycastTrans;
    protected Transform muzzleTrans;
    protected ParticleEffect muzzleFlash;
    protected ParticleEffect muzzleSmoke;
    protected int hitmaskOverride;
    protected EnumDamageTypes damageType;
    protected List<string> buffs = new List<string>();
    protected string material;
    protected float aaSize;
    protected bool aaDebug;

    protected float spreadMultiplier = 1f;
    protected PerlinNoise MeanderNoise = new PerlinNoise(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
    protected DamageMultiplier damageMultiplier;
    protected ItemActionAttack.AttackHitInfo attackDetails = new ItemActionAttack.AttackHitInfo()
    {
        isCriticalHit = false,
        WeaponTypeTag = ItemActionAttack.RangedTag
    };
    protected Dictionary<string, ItemActionAttack.Bonuses> ToolBonuses = new Dictionary<string, ItemActionAttack.Bonuses>();
    protected Ray shootRay = new Ray();
    protected AimAssistHelper aimAssist;
    protected ParticleSystemUpdater muzzleFlashManager = new ParticleSystemUpdater();
    protected ParticleSystemUpdater muzzleSmokeManager = new ParticleSystemUpdater();
    protected List<float> queue_pending_shots = new List<float>();

    public override bool IsBurstPending => queue_pending_shots.Count > 0;

    private static FastTags headTag = FastTags.Parse("head");
    private static FastTags armTag = FastTags.Parse("arm");
    private static FastTags legTag = FastTags.Parse("leg");

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        damageMultiplier = new DamageMultiplier(properties, null);
        raycastTrans = GetTransform("raycastTransform");
        muzzleTrans = GetTransform("muzzleTransform");

        aaDebug = false;
        _properties.ParseBool("AADebug", ref aaDebug);
    }

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        base.ApplyModEffect(vehicleValue);

        string str = null;
        properties.ParseString("itemName", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "itemName", str);
        boundItemValue = ItemClass.GetItem(str);
        boundItemValue.Quality = vehicleValue.Quality;
        boundItemValue.Seed = vehicleValue.Seed;
        tags = boundItemValue.ItemClass.ItemTags | VehicleWeaponTag | ItemActionAttack.PrimaryTag;

        hitmaskOverride = 8;
        properties.ParseInt("hitmaskOverride", ref hitmaskOverride);
        hitmaskOverride = int.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "hitmaskOverride", hitmaskOverride.ToString()));

        str = null;
        properties.ParseString("damageType", ref str);
        if(!EnumUtils.TryParse<EnumDamageTypes>(str, out damageType, true))
            damageType = EnumDamageTypes.Piercing;

        material = "bullet";
        properties.ParseString("material", ref material);
        material = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "material", material);

        aaSize = 0.75f;
        properties.ParseFloat("AASize", ref aaSize);
        aaSize = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "AASize", aaSize.ToString()));

        if(muzzleTrans != null)
        {
            str = null;
            muzzleFlash = null;
            properties.ParseString("muzzleFlash", ref str);
            str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "muzzleFlash", str);
            if(!string.IsNullOrEmpty(str))
            {
                if(!ParticleEffect.IsAvailable(str))
                    ParticleEffect.RegisterBundleParticleEffect(str);
                muzzleFlash = new ParticleEffect(str, Vector3.zero, 1f, Color.clear, null, muzzleTrans, false);
            }

            str = null;
            muzzleSmoke = null;
            properties.ParseString("muzzleSmoke", ref str);
            str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "muzzleSmoke", str);
            if(!string.IsNullOrEmpty(str))
            {
                if(!ParticleEffect.IsAvailable(str))
                    ParticleEffect.RegisterBundleParticleEffect(str);
                muzzleSmoke = new ParticleEffect(str, Vector3.zero, 1f, Color.clear, null, muzzleTrans, false);
            }
        }

        str = null;
        buffs.Clear();
        HashSet<string> hash_buffs = new HashSet<string>();
        properties.ParseString("buffs", ref str);
        if (!string.IsNullOrEmpty(str))
            hash_buffs.UnionWith(str.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

        str = null;
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "buffAppend", str);
        if(!string.IsNullOrEmpty(str))
            hash_buffs.UnionWith(str.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

        str = null;
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "buffRemove", str);
        if(!string.IsNullOrEmpty(str))
            hash_buffs.ExceptWith(str.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

        buffs.AddRange(hash_buffs);

        if(aimAssist != null)
        {
            float scaleY = GetValue(PassiveEffects.MaxRange) / 2;
            aimAssist.transform.localScale = new Vector3(aaSize, scaleY, aaSize);
            aimAssist.transform.localPosition = new Vector3(0, 0, scaleY);
        }
    }

    public override void NoPauseUpdate(float _dt)
    {
        base.NoPauseUpdate(_dt);
        UpdateAccuracy(_dt);
        ProcessPendingShots(_dt);
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        muzzleFlashManager.Update(Vector3.zero, Quaternion.identity);
        muzzleSmokeManager.Update(Vector3.zero, Quaternion.identity);

        if (!GameManager.Instance.IsPaused() && !hasOperator)
            ProcessPendingShots(_dt);
    }

    protected virtual void ProcessPendingShots(float _dt)
    {
        while(queue_pending_shots.Count > 0 && _dt > 0)
        {
            if (queue_pending_shots[0] <= -2f)
            {
                FiringStateReaction(FiringState.Stop);
                queue_pending_shots.RemoveAt(0);
            }
            else if (queue_pending_shots[0] <= -1f)
            {
                if(hasOperator)
                    OnFireEnd();
                queue_pending_shots.RemoveAt(0);
            }
            else
            {
                queue_pending_shots[0] -= _dt;
                if (queue_pending_shots[0] <= 0)
                {
                    _dt = -queue_pending_shots[0];
                    if (hasOperator)
                    {
                        ConsumeAmmo(1);
                        OnBurstShot();
                    }
                    FiringStateReaction(FiringState.Loop);
                    queue_pending_shots.RemoveAt(0);
                }
                else
                    _dt = 0;
            }
        }
    }

    protected internal override void DoFire()
    {
        if(burstInterval <= 0 || burstRepeat == 1)
            DoFireNow();
        else
            NetSyncFire(FiringState.LoopStart);
    }

    public override void NetFireRead(PooledBinaryReader _br, FiringState state)
    {
        if (state != FiringState.Stop)
        {
            if (hasOperator)
                OnBurstShot();
            FiringStateReaction(state);

            if (burstInterval > 0 && burstRepeat > 1)
            {
                for (int i = 0; i < burstRepeat - 1; i++)
                    queue_pending_shots.Add(burstInterval);
                if (hasOperator)
                    queue_pending_shots.Add(-1f);
                //queue_pending_shots.Add(-2f);
                //Log.Out("cur pending count: " + queue_pending_shots.Count);
            }
        }
        else
        {
            if (burstInterval > 0 && burstRepeat > 1)
                queue_pending_shots.Add(-2f);
            else
            {
                FiringStateReaction(state);
                if (hasOperator)
                    OnFireEnd();
            }

        }
    }

    protected override void FiringStateReaction(FiringState state)
    {
        base.FiringStateReaction(state);

        if (state != FiringState.Stop && muzzleTrans != null)
        {
            if (muzzleFlash != null)
            {
                Transform flash = GameManager.Instance.SpawnParticleEffectClient(muzzleFlash, vehicle.entity.entityId);
                if(flash != null && flash.GetComponent<ParticleSystem>() != null)
                {
                    flash.SetParent(muzzleTrans);
                    muzzleFlashManager.Add(flash, false);
                }
            }
            if (muzzleSmoke != null)
            {
                Transform smoke = GameManager.Instance.SpawnParticleEffectClient(muzzleSmoke, vehicle.entity.entityId);
                if(smoke != null && smoke.GetComponent<ParticleSystem>() != null)
                {
                    smoke.SetParent(muzzleTrans);
                    muzzleSmokeManager.Add(smoke, false);
                }
            }
        }
    }

    protected internal override void OnBurstShot()
    {
        base.OnBurstShot();
        player.MinEventContext.ItemValue = boundItemValue;
        FireEvent(MinEventTypes.onSelfRangedBurstShotStart);
        for (int i = 0; i < burstCount; i++)
            FireShots(i);
        spreadMultiplier *= GetValue(PassiveEffects.IncrementalSpreadMultiplier, 1);
        spreadMultiplier = Mathf.Min(spreadMultiplier, 5f);
    }

    protected override void OnFireEnd()
    {
        float delayPerRound = 60f / GetValue(PassiveEffects.RoundsPerMinute);
        if (burstInterval > 0)
            repeatCooldown = Mathf.Max(burstRepeat * delayPerRound - burstRepeat * burstDelay, 0);
        else
            repeatCooldown = delayPerRound;
    }

    protected virtual void FireShots(int _shotIdx)
    {
        float range = GetValue(PassiveEffects.MaxRange);
        shootRay.origin = raycastTrans.position + Origin.position;
        shootRay.direction = getDirectionOffset(raycastTrans.forward, _shotIdx);

        int EntityPenetration = Mathf.FloorToInt(GetValue(PassiveEffects.EntityPenetrationCount)) + 1;
        int BlockPenetration = Mathf.Max(Mathf.FloorToInt(EffectManager.GetValue(PassiveEffects.BlockPenetrationFactor)), 1);
        World world = GameManager.Instance.World;

        EntityAlive entityHit = null;
        for (int i = 0; i < EntityPenetration; i++)
        {
            BlockValue blockHit = BlockValue.Air;

            attackDetails.hitPosition = Vector3i.zero;
            attackDetails.bKilled = false;

            bool targetAlive = false;
            if (Voxel.Raycast(world, shootRay, range, -538750997, hitmaskOverride, 0f))
            {
                WorldRayHitInfo hitInfo = Voxel.voxelRayHitInfo.Clone();
                if (hitInfo.hit.distanceSq > range * range)
                    return;
                shootRay.origin = hitInfo.hit.pos;
                if (hitInfo.tag.StartsWith("E_"))
                {
                    EntityDrone drone = hitInfo.transform.GetComponent<EntityDrone>();
                    if (drone && drone.isAlly(player))
                    {
                        shootRay.origin = hitInfo.hit.pos + shootRay.direction * 0.1f;
                        i--;
                        continue;
                    }
                    EntityAlive entityNext = ItemActionAttack.FindHitEntityNoTagCheck(hitInfo, out _) as EntityAlive;
                    if (entityNext == null || entityHit == entityNext || entityNext == vehicle.entity)
                    {
                        shootRay.origin = hitInfo.hit.pos + shootRay.direction * 0.1f;
                        i--;
                        continue;
                    }

                    player.MinEventContext.Other = entityNext;
                    entityHit = entityNext;
                    targetAlive = entityHit.IsAlive();
                }
                else
                {
                    blockHit = ItemActionAttack.GetBlockHit(world, hitInfo);
                    i += Mathf.Max(Mathf.FloorToInt((float)blockHit.Block.MaxDamage / (float)BlockPenetration), 1);
                    player.MinEventContext.BlockValue = blockHit;
                }
                player.MinEventContext.ItemValue = boundItemValue;
                player.MinEventContext.Position = hitInfo.hit.pos;
                player.MinEventContext.StartPosition = player.GetPosition();
                float damegeModifier = 1f;
                float falloffModifier = GetValue(PassiveEffects.DamageFalloffRange, range);
                if (hitInfo.hit.distanceSq > falloffModifier * falloffModifier)
                {
                    damegeModifier = 1f - (hitInfo.hit.distanceSq - falloffModifier * falloffModifier) / (range * range - falloffModifier * falloffModifier);
                }
                FireEvent(MinEventTypes.onSelfPrimaryActionRayHit);
                ItemActionAttack.Hit(hitInfo, player.entityId, damageType, blockHit.isair ? 0 : GetValue(PassiveEffects.BlockDamage, 1, blockHit.Block.Tags) * damegeModifier, GetValue(PassiveEffects.EntityDamage) * damegeModifier, 1f, 1f, boundItemValue.ItemClass.CritChance.Value, GetDismemberChance(hitInfo), material, damageMultiplier, null, attackDetails, 0, 1, 1, null, ToolBonuses, ItemActionAttack.EnumAttackMode.RealNoHarvesting, null, -1, boundItemValue);

                if(attackDetails.bBlockHit)
                {
                    player.MinEventContext.ItemValue = boundItemValue;
                    player.MinEventContext.BlockValue = attackDetails.blockBeingDamaged;
                    player.MinEventContext.Tags = attackDetails.blockBeingDamaged.Block.Tags;
                    if (attackDetails.bKilled)
                    {
                        boundItemValue.ItemClass.FireEvent(MinEventTypes.onSelfDestroyedBlock, player.MinEventContext);
                        effects.FireEvent(MinEventTypes.onSelfDestroyedBlock, player.MinEventContext);
                    }else
                    {
                        boundItemValue.ItemClass.FireEvent(MinEventTypes.onSelfDamagedBlock, player.MinEventContext);
                        effects.FireEvent(MinEventTypes.onSelfDamagedBlock, player.MinEventContext);
                    }
                }else if(targetAlive && entityHit != null)
                {
                    player.MinEventContext.ItemValue = boundItemValue;
                    player.MinEventContext.Other = entityHit;
                    if(entityHit.IsDead())
                    {
                        boundItemValue.FireEvent(MinEventTypes.onSelfKilledOther, player.MinEventContext);
                        effects.FireEvent(MinEventTypes.onSelfKilledOther, player.MinEventContext);
                    }else
                    {
                        boundItemValue.FireEvent(MinEventTypes.onSelfAttackedOther, player.MinEventContext);
                        effects.FireEvent(MinEventTypes.onSelfAttackedOther, player.MinEventContext);
                        if(entityHit.RecordedDamage.Strength > 0)
                        {
                            boundItemValue.FireEvent(MinEventTypes.onSelfDamagedOther, player.MinEventContext);
                            effects.FireEvent(MinEventTypes.onSelfDamagedOther, player.MinEventContext);
                        }

                        if(buffs.Count > 0)
                        {
                            foreach(string buff in buffs)
                            {
                                BuffClass buffClass = BuffManager.GetBuff(buff);
                                float chance = GetValue(PassiveEffects.BuffProcChance, 1, FastTags.Parse(buff), true);
                                if(buffClass != null && entityHit.rand.RandomFloat <= chance)
                                    entityHit.Buffs?.AddBuff(buff, player.entityId);
                            }
                        }
                    }
                }
            }
            else
            {
                player.MinEventContext.ItemValue = boundItemValue;
                FireEvent(MinEventTypes.onSelfPrimaryActionRayMiss);
            }
        }
    }

    protected virtual void UpdateAccuracy(float _dt)
    {
        float multiplier = 1f;
        if (vehicle.IsTurbo)
            multiplier = GetValue(PassiveEffects.SpreadMultiplierRunning);
        else if(vehicle.CurrentVelocity.sqrMagnitude > 1)
            multiplier = GetValue(PassiveEffects.SpreadMultiplierWalking);
        else
            multiplier = GetValue(PassiveEffects.SpreadMultiplierIdle);

        spreadMultiplier = Mathf.Lerp(spreadMultiplier, multiplier, _dt * Mathf.Clamp01(GetValue(PassiveEffects.WeaponHandling, 0.1f)) * 15f);
    }

    protected virtual Vector3 getDirectionOffset(Vector3 _forward, int _shotIdx)
    {
        float horSpread = GetValue(PassiveEffects.SpreadDegreesHorizontal, 45f) * spreadMultiplier * (float)MeanderNoise.Noise((double)Time.time, 0.0, _shotIdx) * 0.66f;
        float verSpread = GetValue(PassiveEffects.SpreadDegreesVertical, 45f) * spreadMultiplier * ((float)MeanderNoise.Noise(0.0, (double)Time.time, (double)_shotIdx) * 0.66f);
        Quaternion quaternion = Quaternion.LookRotation(_forward, Vector3.up);
        Vector3 vector = Quaternion.Euler(verSpread, horSpread, 0f) * Vector3.forward;
        vector = quaternion * vector;
        return vector;
    }

    protected float GetDismemberChance(WorldRayHitInfo hitInfo)
    {
        FastTags hitPartTag = default(FastTags);
        if (hitInfo.tag == "E_BP_Head")
            hitPartTag = headTag;
        else if (hitInfo.tag.ContainsCaseInsensitive("arm"))
            hitPartTag = armTag;
        else if (hitInfo.tag.ContainsCaseInsensitive("leg"))
            hitPartTag = legTag;
        return GetValue(PassiveEffects.DismemberChance, 0, hitPartTag);
    }

    protected float GetValue(PassiveEffects passive, float originalValue = 1, FastTags additionalTags = default(FastTags), bool replaceTag = false)
    {
        if (GameManager.Instance == null || GameManager.Instance.gameStateManager == null || !GameManager.Instance.gameStateManager.IsGameStarted())
            return originalValue;
        
        FastTags newTags = replaceTag ? additionalTags : tags | additionalTags;
        EntityPlayer player = vehicle.entity.GetAttached(seat) as EntityPlayer;
        player.MinEventContext.Seed = boundItemValue.Seed + (int)passive;
        player.MinEventContext.ItemValue = boundItemValue;
        float perc = 1f;
        
        if (EntityClass.list.TryGetValue(player.entityClass, out var _value))
            _value.Effects.ModifyValue(player, passive, ref originalValue, ref perc, 0f, tags);
        boundItemValue.ItemClass.Effects.ModifyValue(player, passive, ref originalValue, ref perc, boundItemValue.Quality, newTags, 1);
        player.Progression.ModifyValue(passive, ref originalValue, ref perc, newTags);
        player.Buffs.ModifyValue(passive, ref originalValue, ref perc, newTags);
        effects.ModifyValue(player, passive, ref originalValue, ref perc, boundItemValue.Quality, newTags, 1);

        return originalValue * perc;
    }

    protected internal override void OnDeactivated()
    {
        base.OnDeactivated();
        if (aimAssist)
            GameObject.Destroy(aimAssist.gameObject);
        queue_pending_shots.Clear();
    }

    protected internal override void OnActivated()
    {
        base.OnActivated();

        if (rotator != null)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            float scaleY = GetValue(PassiveEffects.MaxRange) / 2;
            cylinder.GetComponent<Collider>().isTrigger = true;
            cylinder.GetComponent<Renderer>().enabled = aaDebug;
            cylinder.transform.SetParent(raycastTrans);
            cylinder.transform.localScale = new Vector3(aaSize, scaleY, aaSize);
            cylinder.transform.localEulerAngles = new Vector3(90, 0, 0);
            cylinder.transform.localPosition = new Vector3(0, 0, scaleY);
            aimAssist = cylinder.AddComponent<AimAssistHelper>();
            aimAssist.localPlayer = player;
        }
    }

    protected override Ray CreateLookRay()
    {
        Ray lookRay = base.CreateLookRay();
        if(aimAssist.aimTarget != null && aimAssist.aimTarget.IsAlive())
        {
            Vector3 aaDir = (aimAssist.aimTarget.GetPosition() + aimAssist.aimTarget.getHeadPosition()) / 2 - Origin.position - raycastTrans.position;
            if (Mathf.Abs(Utils.GetAngleBetween(aaDir, lookRay.direction)) > 5f)
                return lookRay;

            return new Ray(raycastTrans.position, aaDir);
        }
        return lookRay;
    }

    protected override void FireEvent(MinEventTypes e)
    {
        boundItemValue.ItemClass.FireEvent(e, (vehicle.entity.GetAttached(seat) as EntityAlive).MinEventContext);
        base.FireEvent(e);
    }
}

