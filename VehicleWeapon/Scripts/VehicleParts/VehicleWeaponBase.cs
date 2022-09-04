using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public class VehicleWeaponBase : VehicleWeaponPartBase
{
    protected int burstCount;
    protected int burstRepeat;
    protected float burstInterval;
    protected float burstDelay;
    protected float repeatInterval = 0f;
    protected float repeatCooldown = 0f;
    protected string emptySound;
    protected string fireSound;
    protected string loopSound;
    protected string endSound;
    protected bool fullauto;
    protected ItemValue ammoValue;

    protected bool hasOperator = false;
    protected EntityPlayerLocal player = null;
    protected string notReadySound;
    protected string notOnTargetSound;
    protected string activationSound;
    protected string deactivationSound;
    protected VehicleWeaponRotatorBase rotator = null;
    protected int seat = 0;
    protected int slot = int.MaxValue;
    protected FiringJuncture timing;
    protected bool pressed = false;
    protected bool activated = true;
    protected bool enabled;
    protected Transform enableTrans = null;
    protected MinEffectController effects = new MinEffectController()
    {
        ParentType = MinEffectController.SourceParentType.None,
        EffectGroups = new List<MinEffectGroup>(),
        PassivesIndex = new HashSet<PassiveEffects>(EffectManager.PassiveEffectsComparer)
    };

    protected VPWeaponManager manager;
    protected float denySoundCooldown = 0f;
    protected FiringState curFiringState = FiringState.Stop;
    public virtual bool IsBurstPending{ get => false; }
    protected FiringState NextFiringState { get => fullauto ? curFiringState == FiringState.Stop ? FiringState.LoopStart : FiringState.Loop : FiringState.Start; }

    public event Action<PooledBinaryWriter, VehicleWeaponBase> DynamicUpdateDataCreation
    {
        add => list_update_data_callbacks.Add(value);
        remove => list_update_data_callbacks.Remove(value);
    }

    public event Action<PooledBinaryWriter, VehicleWeaponBase> DynamicFireDataCreation
    {
        add => list_fire_data_callbacks.Add(value);
        remove => list_fire_data_callbacks.Remove(value);
    }

    protected internal List<Action<PooledBinaryWriter, VehicleWeaponBase>> list_update_data_callbacks = new List<Action<PooledBinaryWriter, VehicleWeaponBase>>();
    protected internal List<Action<PooledBinaryWriter, VehicleWeaponBase>> list_fire_data_callbacks = new List<Action<PooledBinaryWriter, VehicleWeaponBase>>();

    protected static FastTags VehicleWeaponTag = FastTags.Parse("vehicleWeapon");

    public void InvokeUpdateCallbacks(PooledBinaryWriter _bw)
    {
        foreach (var handler in list_update_data_callbacks)
            handler(_bw, this);
    }

    public void InvokeFireCallbacks(PooledBinaryWriter _bw)
    {
        foreach (var handler in list_fire_data_callbacks)
            handler(_bw, this);
    }

    protected enum FiringJuncture
    {
        Anytime = 0,
        FirstShot = 1,
        FromSlotKey = 2,
        OnTarget = 4
    }

    public enum FiringState
    {
        Start,
        LoopStart,
        Loop,
        Stop
    }
    public bool HasOperator { get => hasOperator; }
    public bool Activated { get => activated; }
    public bool Enabled { get => enabled; }
    public VehicleWeaponRotatorBase Rotator { get => rotator; }
    public int Seat { get => seat; protected internal set => seat = value; }
    public int Slot { get => slot; set => slot = value; }

    private Ray lookRay;
    public Ray LookRay { get => lookRay; private set => lookRay = value; }
    protected virtual Ray CreateLookRay()
    {
        return player.playerCamera.ScreenPointToRay(GetDynamicMousePosition());
    }

    protected static int dynamicScaleMode = 0;
    protected static float dynamicScaleOverride = 1;
    public static void OnVideoSettingChanged()
    {
        dynamicScaleMode = GamePrefs.GetInt(EnumGamePrefs.OptionsGfxDynamicMode);
        if (dynamicScaleMode == 2)
            dynamicScaleOverride = GamePrefs.GetFloat(EnumGamePrefs.OptionsGfxDynamicScale);
    }
    protected Vector3 GetDynamicMousePosition()
    {
        Vector3 dynamicMousePos;

        if (!GameRenderManager.dynamicIsEnabled)
            dynamicMousePos = Input.mousePosition;
        else
        {
            float scale;
            if (dynamicScaleMode == 1)
                scale = (float)player.renderManager.GetDynamicRenderTexture().width / Screen.width;
            else
                scale = dynamicScaleOverride;
            dynamicMousePos = Input.mousePosition * scale;
        }

        return dynamicMousePos;
    }

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        string str = null;
        _properties.ParseInt("seat", ref seat);
        if (seat < 0)
        {
            Log.Error("seat can not be less than 0! setting to 0...");
            seat = 0;
        }
        _properties.ParseInt("slot", ref slot);

        str = null;
        _properties.ParseString("enableTransform", ref str);
        if (!string.IsNullOrEmpty(str))
            enableTrans = GetTransform(str);

        player = GameManager.Instance.World.GetPrimaryPlayer();
    }

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        base.ApplyModEffect(vehicleValue);
        enabled = true;
        properties.ParseBool("enabled", ref enabled);
        enabled = bool.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "enabled", enabled.ToString()));
        if (enableTrans)
            enableTrans.gameObject.SetActive(enabled);

        activationSound = String.Empty;
        properties.ParseString("activationSound", ref activationSound);
        activationSound = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "activationSound", activationSound);
        deactivationSound = String.Empty;
        properties.ParseString("deactivationSound", ref deactivationSound);
        deactivationSound = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "deactivationSound", deactivationSound);
        notOnTargetSound = String.Empty;
        properties.ParseString("notOnTargetSound", ref notOnTargetSound);
        notOnTargetSound = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "notOnTargetSound", notOnTargetSound);
        notReadySound = String.Empty;
        properties.ParseString("notReadySound", ref notReadySound);
        notReadySound = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "notReadySound", notReadySound);

        string str = null;
        properties.ParseString("fireWhen", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "fireWhen", str);
        timing = FiringJuncture.Anytime;
        if(!string.IsNullOrEmpty(str))
        {
            string[] arr = str.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach(string s in arr)
            {
                Enum.TryParse<FiringJuncture>(s.Trim(), out var res);
                timing |= res;
            }
        }

        burstCount = 1;
        properties.ParseInt("burstCount", ref burstCount);
        burstCount = int.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "burstCount", burstCount.ToString()));
        burstInterval = 0f;
        properties.ParseFloat("burstInterval", ref burstInterval);
        burstInterval = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "burstInterval", burstInterval.ToString()));
        burstRepeat = 1;
        properties.ParseInt("burstRepeat", ref burstRepeat);
        burstRepeat = int.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "burstRepeat", burstRepeat.ToString()));
        burstDelay = 0f;
        properties.ParseFloat("burstDelay", ref burstDelay);
        burstDelay = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "burstDelay", burstDelay.ToString()));
        fullauto = false;
        properties.ParseBool("fullauto", ref fullauto);
        fullauto = bool.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "fullauto", fullauto.ToString()));

        str = null;
        ammoValue = ItemValue.None.Clone();
        properties.ParseString("ammo", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "ammo", str);
        if (!string.IsNullOrEmpty(str))
            ammoValue = ItemClass.GetItem(str, false);

        emptySound = String.Empty;
        properties.ParseString("emptySound", ref emptySound);
        emptySound = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "emptySound", emptySound);
        fireSound = String.Empty;
        properties.ParseString("fireSound", ref fireSound);
        fireSound = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "fireSound", fireSound);
        loopSound = String.Empty;
        properties.ParseString("loopSound", ref loopSound);
        loopSound = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "loopSound", loopSound);
        properties.ParseString("endSound", ref endSound);
        endSound = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "endSound", endSound);

        ParseEffectGroups(vehicleValue, this);
        if (rotator != null)
            rotator.ApplyModEffect(vehicleValue);
    }

    protected static void ParseEffectGroups(ItemValue vehicleValue, VehicleWeaponBase weapon)
    {
        weapon.effects.EffectGroups.Clear();
        weapon.effects.PassivesIndex.Clear();
        weapon.effects.EffectGroupXml.Clear();
        weapon.effects.ParentPointer = vehicleValue.GetItemId();

        if(vehicleValue.Modifications != null && vehicleValue.Modifications.Length > 0)
            foreach (var mod in vehicleValue.Modifications)
                if (mod != null && mod.ItemClass is ItemClassModifier)
                    ParseEffectGroup(mod.ItemClass.Effects, weapon);

        if (vehicleValue.CosmeticMods != null && vehicleValue.CosmeticMods.Length > 0)
            foreach (var cos in vehicleValue.CosmeticMods)
                if (cos != null && cos.ItemClass is ItemClassModifier)
                    ParseEffectGroup(cos.ItemClass.Effects, weapon);

        foreach (var effect in weapon.effects.EffectGroups)
            foreach (var passive in effect.PassiveEffects)
                Log.Out(passive.Type.ToString() + " " + passive.Modifier.ToString() + " " + passive.Tags);
    }

    protected static void ParseEffectGroup(MinEffectController effects, VehicleWeaponBase weapon)
    {
        int i = 0;
        if (effects == null || effects.EffectGroupXml == null)
            return;
        foreach (var effectNode in effects.EffectGroupXml)
        {
            XmlElement element = effectNode as XmlElement;
            if (element != null && element.HasAttribute("vehicle_weapon") && element.GetAttribute("vehicle_weapon") == weapon.ModName)
            {
                weapon.effects.EffectGroups.Add(effects.EffectGroups[i]);
                weapon.effects.EffectGroupXml.Add(effectNode);
                weapon.effects.PassivesIndex.UnionWith(effects.EffectGroups[i].PassivesIndex);
                Log.Out("Adding effect group to " + weapon.ModName);
            }
            i++;
        }
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();
        manager = vehicle.FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;

        string rotatorName = null;
        properties.ParseString("rotator", ref rotatorName);
        if (!string.IsNullOrEmpty(rotatorName))
            rotator = vehicle.FindPart(rotatorName) as VehicleWeaponRotatorBase;

        if (rotator != null)
            rotator.SetWeapon(this);
    }

    public virtual void InitWeaponConnections(IEnumerable<VehicleWeaponBase> weapons)
    {
    }

    public override void NoPauseUpdate(float _dt)
    {
        if (rotator != null)
            rotator.NoPauseUpdate(_dt);

        if (!IsBurstPending && repeatCooldown > 0)
            repeatCooldown -= _dt;

        if (denySoundCooldown > 0)
            denySoundCooldown -= _dt;

        NetSyncUpdate();
    }

    public override void NoGUIUpdate(float _dt)
    {
        if (rotator != null && GameManager.Instance.GameIsFocused)
        {
            LookRay = CreateLookRay();
            rotator.NoGUIUpdate(_dt);
        }
    }

    protected void NetSyncUpdate(bool forced = false)
    {
        if(forced || ShouldNetSyncUpdate())
            manager.NetSyncUpdateAdd(this);
    }

    public override bool ShouldNetSyncUpdate()
    {
        return rotator != null && rotator.ShouldNetSyncUpdate();
    }

    public override void NetSyncRead(PooledBinaryReader _br)
    {
        if (rotator != null && _br != null)
            rotator.NetSyncRead(_br);
    }

    public override void NetSyncWrite(PooledBinaryWriter _bw)
    {
        if(rotator != null)
            rotator.NetSyncWrite(_bw);
    }

    public virtual void OnPlayerEnter()
    {
        hasOperator = true;
        if (rotator != null)
            rotator.ForceUpdate(0);
        NetSyncUpdate(true);

        if (activated && enabled)
            OnActivated();
    }

    public virtual void OnPlayerDetach()
    {
        hasOperator = false;

        OnDeactivated();
    }

    protected internal virtual void OnActivated()
    {
        if (rotator != null)
            rotator.CreatePreview();
    }

    protected internal virtual void OnDeactivated()
    {
        pressed = false;
        StopFire();
        if (rotator != null)
            rotator.DestroyPreview();
    }

    protected void ToggleActivated()
    {
        activated = !activated;
        if (activated)
        {
            OnActivated();
            vehicle.entity.PlayOneShot(activationSound);
        }
        else
        {
            OnDeactivated();
            vehicle.entity.PlayOneShot(deactivationSound);
        }
    }

    public virtual void HandleUserInput(int userData)
    {
        var vextra = PlayerActionsVehicleExtra.Instance;
        if (slot < vextra.ActivateActions.Count && (vextra.ActivateActions[slot].IsPressed || vextra.ActivateActions[slot].WasReleased))
        {
            if (PlayerActionsVehicleWeapon.Instance.HoldToggleActivated.IsPressed)
            {
                if(vextra.ActivateActions[slot].WasPressed)
                    ToggleActivated();
            }
            else
            {
                if(activated)
                {
                    userData |= (int)FiringJuncture.FromSlotKey;
                    DoFireLocal(ref userData, vextra.ActivateActions[slot].WasReleased);
                }
            }
        }
    }

    public void DoFireLocal(ref int flags, bool isRelease)
    {
        bool flag = CanFire(flags, isRelease, out bool forceStop);
        pressed = !isRelease;
        if (flag)
        {
            flags |= (int)FiringJuncture.FirstShot;
            DoFire();
        }
        else if (forceStop)
            StopFire();
    }

    protected internal virtual bool CanFire(int flags, bool isRelease, out bool forceStop)
    {
        forceStop = isRelease;
        if (isRelease)
            return false;
        
        if (pressed && (!fullauto || curFiringState == FiringState.Stop))
        {
            forceStop = true;
            return false;
        }

        if (repeatCooldown > 0)
            return false;

        if (IsBurstPending)
        {
            if(!fullauto)
            {
                TryPlayDenySoundLocal(notReadySound);
                forceStop = true;
            }
            return false;
        }

        if (ammoValue.type > 0 && player.bag.GetItemCount(ammoValue) < burstRepeat)
        {
            TryPlayDenySoundLocal(emptySound);
            forceStop = true;
            return false;
        }

        if (timing == FiringJuncture.Anytime)
            return true;

        if ((timing & FiringJuncture.FirstShot) > 0 && (flags & (int)FiringJuncture.FirstShot) > 0)
            return false;

        if ((timing & FiringJuncture.FromSlotKey) > 0 && (flags & (int)FiringJuncture.FromSlotKey) == 0)
            return false;

        if ((timing & FiringJuncture.OnTarget) > 0 && rotator != null && !rotator.OnTarget)
        {
            TryPlayDenySoundLocal(notOnTargetSound);
            return false;
        }
        return true;
    }

    protected virtual void StopFire()
    {
        if (curFiringState != FiringState.Stop)
            NetSyncFire(FiringState.Stop);
    }

    protected internal virtual void DoFire()
    {
    }

    protected void DoFireNow()
    {
        for (int i = 0; i < burstRepeat; ++i)
            NetSyncFire(NextFiringState);
        OnFireEnd();
    }

    protected virtual void OnFireEnd()
    {
        repeatCooldown = repeatInterval;
    }

    protected void NetSyncFire(FiringState state)
    {
        manager.NetSyncFireAdd(this, state);
        curFiringState = state;
        //Log.Out(tag + " " + state.ToStringCached());
        if (state != FiringState.Stop)
        {
            //curRepeatCount++;
            if (ammoValue.type > 0)
                ConsumeAmmo(1);
        }
        //else
            //curRepeatCount = 0;
    }

    public virtual void NetFireWrite(PooledBinaryWriter _bw, FiringState state)
    {
    }

    public virtual void NetFireRead(PooledBinaryReader _br, FiringState state)
    {
        if (hasOperator && state != FiringState.Stop)
            OnBurstShot();

        FiringStateReaction(state);
    }

    protected virtual void ConsumeAmmo(int count)
    {
        player.bag.DecItem(ammoValue, count);
    }

    protected virtual void FiringStateReaction(FiringState state)
    {
        switch(state)
        {
            case FiringState.Start:
                OnStart();
                break;
            case FiringState.LoopStart:
                OnLoopStart();
                break;
            case FiringState.Loop:
                OnLoop();
                break;
            case FiringState.Stop:
                OnStop();
                break;
        }
    }

    protected virtual void OnStart()
    {
        Audio.Manager.Play(vehicle.entity, fireSound);

        if (ConnectionManager.Instance.IsServer)
            GameManager.Instance.World.aiDirector.OnSoundPlayedAtPosition(vehicle.entity.GetAttached(seat) != null ? vehicle.entity.GetAttached(seat).entityId : -1, vehicle.entity.position - Origin.position, fireSound, 1f);
    }

    protected virtual void OnLoopStart()
    {
        Audio.Manager.Play(vehicle.entity, fireSound);
        if (!string.IsNullOrEmpty(loopSound))
        {
            Audio.Manager.Stop(vehicle.entity.entityId, loopSound);
            Audio.Manager.Play(vehicle.entity, loopSound);
        }

        if (ConnectionManager.Instance.IsServer)
        {
            GameManager.Instance.World.aiDirector.OnSoundPlayedAtPosition(vehicle.entity.GetAttached(seat) != null ? vehicle.entity.GetAttached(seat).entityId : -1, vehicle.entity.position - Origin.position, fireSound, 1f);
            if (!string.IsNullOrEmpty(loopSound))
                GameManager.Instance.World.aiDirector.OnSoundPlayedAtPosition(vehicle.entity.GetAttached(seat) != null ? vehicle.entity.GetAttached(seat).entityId : -1, vehicle.entity.position - Origin.position, loopSound, 1f);
        }
    }

    protected virtual void OnLoop()
    {
        if (string.IsNullOrEmpty(loopSound))
            Audio.Manager.Play(vehicle.entity, fireSound);
        if (ConnectionManager.Instance.IsServer && string.IsNullOrEmpty(loopSound))
            GameManager.Instance.World.aiDirector.OnSoundPlayedAtPosition(vehicle.entity.GetAttached(seat) != null ? vehicle.entity.GetAttached(seat).entityId : -1, vehicle.entity.position - Origin.position, fireSound, 1f);
    }

    protected virtual void OnStop()
    {
        if (!string.IsNullOrEmpty(loopSound))
            Audio.Manager.Stop(vehicle.entity.entityId, loopSound);
        if(!string.IsNullOrEmpty(endSound))
        {
            Audio.Manager.Stop(vehicle.entity.entityId, endSound);
            Audio.Manager.Play(vehicle.entity, endSound);
        }
    }

    protected internal virtual void OnBurstShot()
    {
    }

    protected virtual void FireEvent(MinEventTypes e)
    {
        player.FireEvent(e, false);
        effects.FireEvent(e, (vehicle.entity.GetAttached(seat) as EntityAlive).MinEventContext);
    }

    protected void TryPlayDenySoundLocal(string sound)
    {
        if(denySoundCooldown <= 0)
        {
            Audio.Manager.Play(vehicle.entity, sound);
            denySoundCooldown = 1f;
        }
    }
}

