using System;
using System.Collections.Generic;

public class VPCycleFireWeapon : VehicleWeaponBase
{
    protected VehicleWeaponBase[] cycleArray;
    protected Dictionary<string, int> cycleMap = new Dictionary<string, int>();
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
        for (int i = 0; i < cycleArray.Length; i++)
            cycleMap.Add(cycleArray[i].tag, i + 1);
    }

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        base.ApplyModEffect(vehicleValue);
        string name = GetModName();

        cycleInterval = 0;
        properties.ParseFloat("cycleInterval", ref cycleInterval);
        cycleInterval = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "cycleInterval", cycleInterval.ToString()));

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
        foreach (var weapon in cycleArray)
        {
            weapon.Seat = seat;
            weapon.Slot = slot;
            weapon.DynamicUpdateDataCreation += RecursiveWriteUpdateData;
            weapon.DynamicUpdateDataCreation += WriteCycleIndex;
            weapon.DynamicFireDataCreation += RecursiveWriteFireData;
            weapon.DynamicFireDataCreation += WriteCycleIndex;
        }

        foreach (var weapon in cycleArray)
            weapon.InitWeaponConnections(cycleArray);
    }

    private void WriteCycleIndex(PooledBinaryWriter _bw, VehicleWeaponBase target)
    {
        cycleMap.TryGetValue(target.tag, out int index);
        _bw.Write((byte)index);
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

    protected override void NetFireWrite(PooledBinaryWriter _bw)
    {
        _bw.Write((byte)0);
        base.NetFireWrite(_bw);
    }

    protected internal override bool CanFire(bool firstShot, bool isRelease, bool fromSlot)
    {
        if(base.CanFire(firstShot, isRelease, fromSlot))
        {
            bool flag = true;
            if (cycleInterval > 0)
                flag = cycleCooldown <= 0 && !cycleArray[LastCycleIndex].IsCoRunning && cycleArray[curCycleIndex].CanFire(firstShot, isRelease, fromSlot);
            else
            {
                int i = 0;
                canFireList.Clear();
                foreach (var weapon in cycleArray)
                {
                    if (weapon.CanFire(firstShot, isRelease, fromSlot))
                        canFireList.Add(i);
                    i++;
                }
                flag = canFireList.Count > 0;
            }

            return flag;
        }

        return false;
    }

    protected internal override void DoFire()
    {
        NetSyncUpdate(true);
        if(cycleInterval > 0)
        {
            cycleArray[curCycleIndex].DoFire();
            cycleCooldown = cycleInterval;
            curCycleIndex = curCycleIndex == cycleArray.Length - 1 ? 0 : curCycleIndex + 1;
        }
        else
            foreach(int index in canFireList)
                cycleArray[index].DoFire();
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
        foreach (var weapon in cycleArray)
            weapon.OnDeactivated();
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

    public override void DoFireClient(int count, PooledBinaryReader _br)
    {
        if (_br == null)
            return;

        int slot = _br.ReadByte();
        if (slot > 0)
            cycleArray[slot - 1].DoFireClient(count, _br);
    }
}

