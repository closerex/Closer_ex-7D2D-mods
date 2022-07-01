using System;
using System.Collections.Generic;

public class VPCycleFireWeapon : VehicleWeaponBase
{
    protected VehicleWeaponBase[] cycleArray;
    protected float cycleInterval;
    protected float cycleCooldown = 0f;
    protected int curCycleIndex = 0;

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
        int i = 1;
        foreach (var weapon in cycleArray)
        {
            weapon.Seat = seat;
            weapon.Slot = slot;
            weapon.UserData.Add(i++);
        }
        UserData.Add(0);

        foreach (var weapon in cycleArray)
            weapon.InitWeaponConnections(cycleArray);
    }

    protected internal override bool CanFire(bool firstShot, bool isRelease, bool fromSlot)
    {
        return base.CanFire(firstShot, isRelease, fromSlot) && cycleCooldown <= 0 && cycleArray[curCycleIndex].CanFire(firstShot, isRelease, fromSlot);
    }

    protected internal override void DoFire()
    {
        base.DoFire();
        if (rotator != null)
            rotator.NetSyncSendPacket();
        cycleArray[curCycleIndex].DoFire();
    }

    public override void NoPauseUpdate(float _dt)
    {
        base.NoPauseUpdate(_dt);
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

    public override void NetSyncUpdate(float horRot, float verRot, Stack<int> userData)
    {
        int data = userData.Pop();
        if (data > 0)
            cycleArray[data - 1].NetSyncUpdate(horRot, verRot, userData);
        else
            base.NetSyncUpdate(horRot, verRot, userData);
    }

    protected internal override void Fired()
    {
        cycleCooldown = cycleInterval;
        curCycleIndex++;
        curCycleIndex = curCycleIndex >= cycleArray.Length ? 0 : curCycleIndex;
        base.Fired();
    }
}

