using System;
using System.Collections.Generic;

public class VPCycleFireWeapon : VehicleWeaponBase
{
    protected VehicleWeaponBase[] cycleArray;
    protected float cycleInterval;
    protected float cycleCooldown = 0f;
    protected int curCycleIndex = 0;
    protected List<int> canFireList = new List<int>();
    protected int LastCycleIndex
    {
        get
        {
            return curCycleIndex > 0 ? curCycleIndex - 1 : cycleArray.Length - 1;
        }
    }

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);

        List<VehicleWeaponBase> list = new List<VehicleWeaponBase>();
        foreach (KeyValuePair<string, DynamicProperties> keyValuePair in _properties.Classes.Dict)
        {
            DynamicProperties value = keyValuePair.Value;
            if (value.Values.ContainsKey("class"))
            {
                string text = value.Values["class"];
                try
                {
                    VehicleWeaponBase weapon = Activator.CreateInstance(ReflectionHelpers.GetTypeWithPrefix("VP", text)) as VehicleWeaponBase;
                    weapon.SetVehicle(vehicle);
                    weapon.SetTag(keyValuePair.Key);
                    weapon.SetProperties(value);
                    list.Add(weapon);
                }
                catch (Exception ex)
                {
                    Log.Out(ex.Message);
                    Log.Out(ex.StackTrace);
                    throw new Exception("No vehicle part class 'VP" + text + "' found!");
                }
            }
        }

        list.Sort(new VPWeaponManager.WeaponSlotComparer());
        cycleArray = list.ToArray();
    }

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        base.ApplyModEffect(vehicleValue);

        cycleInterval = 0;
        properties.ParseFloat("cycleInterval", ref cycleInterval);
        cycleInterval = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "cycleInterval", cycleInterval.ToString()));

        foreach (var weapon in cycleArray)
            weapon.ApplyModEffect(vehicleValue);

        cycleCooldown = cycleInterval;
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();
        foreach (var weapon in cycleArray)
            weapon.InitPrefabConnections();
    }

    public override void InitWeaponConnections(IEnumerable<VehicleWeaponBase> weapons)
    {
        base.InitWeaponConnections(weapons);
        for(int i = 0; i < cycleArray.Length; i++)
        {
            var weapon = cycleArray[i];
            int j = i + 1;
            weapon.Seat = seat;
            weapon.Slot = slot;
            Action<PooledBinaryWriter, VehicleWeaponBase> handler = (PooledBinaryWriter _bw, VehicleWeaponBase target) => _bw.Write((byte)j);
            weapon.DynamicUpdateDataCreation += RecursiveWriteUpdateData;
            weapon.DynamicUpdateDataCreation += handler;
            weapon.DynamicFireDataCreation += RecursiveWriteFireData;
            weapon.DynamicFireDataCreation += handler;
        }

        foreach (var weapon in cycleArray)
            weapon.InitWeaponConnections(cycleArray);
    }

    private void RecursiveWriteUpdateData(PooledBinaryWriter _bw, VehicleWeaponBase target)
    {
        InvokeUpdateCallbacks(_bw);
    }

    private void RecursiveWriteFireData(PooledBinaryWriter _bw, VehicleWeaponBase target)
    {
        InvokeFireCallbacks(_bw);
    }

    public override void NetSyncWrite(PooledBinaryWriter _bw)
    {
        _bw.Write((byte)0);
        base.NetSyncWrite(_bw);
    }

    public override void NetSyncRead(PooledBinaryReader _br)
    {
        if (_br == null)
            return;

        int slot = _br.ReadByte();
        if (slot > 0)
            cycleArray[slot - 1].NetSyncRead(_br);
        else
            base.NetSyncRead(_br);
    }

    public override void NetFireRead(PooledBinaryReader _br, VehicleWeaponBase.FiringState state)
    {
        if (_br == null)
            return;

        int slot = _br.ReadByte();
        if (slot > 0)
            cycleArray[slot - 1].NetFireRead(_br, state);
    }

    protected internal override bool CanFire(int flags, bool isRelease, out bool forceStop)
    {
        bool flag = true;
        forceStop = isRelease;

        if (pressed && !fullauto)
        {
            forceStop = true;
            return false;
        }

        StopFireAll(false);
        if (isRelease)
            flag = false;
        else if (cycleInterval > 0)
        {
            flag = cycleCooldown <= 0 && !cycleArray[LastCycleIndex].IsBurstPending && cycleArray[curCycleIndex].CanFire(flags, isRelease, out _);
            canFireList.Add(curCycleIndex);
        }else
        {
            int i = 0;
            foreach (var weapon in cycleArray)
            {
                if (weapon.CanFire(flags, isRelease, out _))
                    canFireList.Add(i);
                i++;
            }
            flag = canFireList.Count > 0;
            if (!flag)
                for (i = 0; i < cycleArray.Length; i++)
                    canFireList.Add(i);
        }

        if(!flag)
            StopFireAll(false);

        pressed = !isRelease;

        return flag;
    }

    protected internal override void DoFire()
    {
        NetSyncUpdate(true);

        foreach(int index in canFireList)
            cycleArray[index].DoFire();

        if(cycleInterval > 0)
        {
            cycleCooldown = cycleInterval;
            curCycleIndex = curCycleIndex == cycleArray.Length - 1 ? 0 : curCycleIndex + 1;
        }
    }

    protected override void StopFire()
    {
        StopFireAll(true);
    }

    protected void StopFireAll(bool all = false)
    {
        int temp = 0;
        if (all)
            foreach (var weapon in cycleArray)
                weapon.DoFireLocal(ref temp, true);
        else
            foreach (var index in canFireList)
            {
                cycleArray[index].DoFireLocal(ref temp, true);
                //Log.Out("cycle release index: " + index + " weapon: " + cycleArray[index].tag);
            }

        canFireList.Clear();
    }

    public override void NoPauseUpdate(float _dt)
    {
        base.NoPauseUpdate(_dt);
        if(cycleCooldown > 0)
            cycleCooldown -= _dt;
        foreach (var weapon in cycleArray)
            weapon.NoPauseUpdate(_dt);
    }

    public override void NoGUIUpdate(float _dt)
    {
        base.NoGUIUpdate(_dt);
        foreach (var weapon in cycleArray)
            weapon.NoGUIUpdate(_dt);
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        foreach (var weapon in cycleArray)
            weapon.Update(_dt);
    }

    public override void OnPlayerEnter()
    {
        base.OnPlayerEnter();
        foreach (var weapon in cycleArray)
            weapon.OnPlayerEnter();
    }

    public override void OnPlayerDetach()
    {
        base.OnPlayerDetach();
        foreach (var weapon in cycleArray)
            weapon.OnPlayerDetach();
    }

    protected internal override void OnActivated()
    {
        base.OnActivated();
        foreach (var weapon in cycleArray)
            weapon.OnActivated();
    }

    protected internal override void OnDeactivated()
    {
        base.OnDeactivated();
        curCycleIndex = 0;
        StopFireAll(true);
        foreach (var weapon in cycleArray)
            weapon.OnDeactivated();
    }
}

