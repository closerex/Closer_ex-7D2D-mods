using System.Collections.Generic;
using System.IO;
using InControl;
using UnityEngine;

public class VPWeaponManager : VehiclePart
{
    private List<VehicleWeaponBase>[] list_weapons;
    public static readonly string VehicleWeaponManagerName = "vehicleWeaponManager";
    private int localPlayerSeat = -1;
    protected EntityPlayerLocal player = null;
    protected Vector3[] cameraOffsets;
    protected NetSyncHelper netSyncHelper;
    public static Vector3 CameraOffset { get; private set; } = Vector3.zero;
    public static bool ShouldNetSync { get; private set; } = false;

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);

        player = GameManager.Instance.World.GetPrimaryPlayer();
        netSyncHelper = new NetSyncHelper(this);
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();
        var parts = vehicle.GetParts();
        int seats = vehicle.GetTotalSeats();
        list_weapons = new List<VehicleWeaponBase>[seats];
        cameraOffsets = new Vector3[seats];
        foreach (var part in parts)
        {
            if (part is VehicleWeaponBase weapon)
            {
                if (list_weapons[weapon.Seat] == null)
                    list_weapons[weapon.Seat] = new List<VehicleWeaponBase>();
                list_weapons[weapon.Seat].Add(weapon);
            }
        }
        foreach (var weapons in list_weapons)
        {
            if (weapons != null)
            {
                SortWeapons(weapons);
            
                foreach(var weapon in weapons)
                    weapon.InitWeaponConnections(weapons);
            }

        }
    }

    public virtual void ApplyModEffect(ItemValue vehicleValue)
    {
        ParseModProperties(vehicleValue);

        foreach (var weapons in list_weapons)
        {
            if (weapons != null)
            {
                foreach (var weapon in weapons)
                    weapon.ApplyModEffect(vehicleValue);
            }
        }

        if(localPlayerSeat >= 0)
        {
            int curSeat = localPlayerSeat;
            OnPlayerDetach();
            OnPlayerEnter(curSeat);
        }
    }

    protected virtual void ParseModProperties(ItemValue vehicleValue)
    {
        for (int i = 0; i < cameraOffsets.Length; i++)
        {
            Vector3 cameraOffset = Vector3.up * 2;
            string str = "cameraOffset" + i;
            properties.ParseVec(str, ref cameraOffset);
            cameraOffsets[i] = StringParsers.ParseVector3(vehicleValue.GetVehicleWeaponPropertyOverride(vehicle.GetName() + "_" + tag, str, cameraOffset.ToString()));
        }
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);

        if(localPlayerSeat >= 0)
        {
            if (list_weapons[localPlayerSeat] != null && !GameManager.Instance.IsPaused())
            {
                bool GUIOpened = Platform.PlatformManager.NativePlatform.Input.PrimaryPlayer.GUIActions.Enabled;
                ShouldNetSync = ConnectionManager.Instance.IsClient || (ConnectionManager.Instance.IsServer && ConnectionManager.Instance.ClientCount() > 0);
                foreach (var weapon in list_weapons[localPlayerSeat])
                {
                    if (!weapon.Enabled || !weapon.Activated)
                        continue;

                    if(!GUIOpened)
                        weapon.NoGUIUpdate(_dt);
                    weapon.NoPauseUpdate(_dt);
                }

                if(!GUIOpened)
                    HandleUserInput();

                netSyncHelper.DoNetSync(vehicle.entity.entityId, player.entityId, localPlayerSeat);
            }
        }
    }

    public virtual void NetSyncUpdateAdd(VehicleWeaponBase weapon)
    {
        if(ShouldNetSync)
            netSyncHelper.AddToUpdate(weapon);
    }

    public virtual void NetSyncFireAdd(VehicleWeaponBase weapon, VehicleWeaponBase.FiringState state)
    {
        netSyncHelper.AddToFire(weapon, state);
    }

    public void NetSyncUpdate(int seat, PooledBinaryReader _br)
    {
        if (list_weapons[seat] != null)
        {
            int count = _br.ReadByte();
            for (int i = 0; i < count; i++)
            {
                int slot = _br.ReadByte();
                list_weapons[seat][slot].NetSyncRead(_br);
            }
        }
    }

    public void NetSyncFire(int seat, PooledBinaryReader _br)
    {
        if (list_weapons[seat] != null)
        {
            int count = _br.ReadUInt16();
            for (int i = 0; i < count; i++)
            {
                int slot = _br.ReadByte();
                VehicleWeaponBase.FiringState state = (VehicleWeaponBase.FiringState)_br.ReadByte();
                list_weapons[seat][slot].NetFireRead(_br, state);
            }
        }
    }

    public virtual void OnPlayerEnter(int seat)
    {
        //Log.Out("player enter seat: " + seat);
        if (localPlayerSeat == seat)
            return;
        PlayerActionsVehicleWeapon.Instance.Enabled = true;
        localPlayerSeat = seat;
        if (list_weapons[localPlayerSeat] != null)
        {
            foreach (var weapon in list_weapons[localPlayerSeat])
                weapon.OnPlayerEnter();
            CameraOffset = cameraOffsets[localPlayerSeat];
        }
    }

    public virtual void OnPlayerDetach()
    {
        //Log.Out("player detach from seat: " + localPlayerSeat);
        PlayerActionsVehicleWeapon.Instance.Enabled = false;
        if (list_weapons[localPlayerSeat] != null)
            foreach (var weapon in list_weapons[localPlayerSeat])
                weapon.OnPlayerDetach();
        localPlayerSeat = -1;
        CameraOffset = Vector3.zero;
        netSyncHelper.Clear();
    }

    protected void HandleUserInput()
    {
        int userData = CheckFireState();
        HandleWeaponUserInput(userData);
        HandleCustomInput(userData);
    }

    protected void HandleWeaponUserInput(int userData)
    {
        if (PlayerActionsVehicleExtra.Instance.HoldSwitchSeat.IsPressed)
            return;

        foreach (VehicleWeaponBase weapon in list_weapons[localPlayerSeat])
            if (weapon.Enabled)
                weapon.HandleUserInput(userData);
    }

    protected virtual void HandleCustomInput(int userData)
    {

    }

    protected int CheckFireState()
    {
        var fireState = PlayerActionsVehicleWeapon.Instance.FireShot;
        if (fireState.IsPressed || fireState.WasReleased)
            return DoFireLocal(fireState.WasReleased);
        return 0;
    }

    public virtual void Cleanup()
    {
        if (localPlayerSeat >= 0)
            OnPlayerDetach();
    }

    public int DoFireLocal(bool isRelease)
    {
        if(localPlayerSeat < 0 || list_weapons[localPlayerSeat] == null)
            return 0;

        int flags = 0;
        foreach(var weapon in list_weapons[localPlayerSeat])
            if(weapon.Enabled && weapon.Activated)
                weapon.DoFireLocal(ref flags, isRelease);
        return flags;
    }

    protected void SortWeapons(List<VehicleWeaponBase> weapons)
    {
        weapons.Sort(new WeaponSlotComparer());
        int i = 0;
        foreach (var weapon in weapons)
            weapon.Slot = i++;
    }

    public class WeaponSlotComparer : IComparer<VehicleWeaponBase>
    {
        public int Compare(VehicleWeaponBase weapon1, VehicleWeaponBase weapon2)
        {
            return weapon1.Slot - weapon2.Slot;
        }
    }

    protected class NetSyncHelper
    {
        private List<VehicleWeaponBase> list_update = new List<VehicleWeaponBase>();
        private List<KeyValuePair<VehicleWeaponBase, VehicleWeaponBase.FiringState>> list_fire = new List<KeyValuePair<VehicleWeaponBase, VehicleWeaponBase.FiringState>>();
        private VPWeaponManager manager;
        private byte updateCounter = 0;

        public NetSyncHelper(VPWeaponManager manager)
        {
            this.manager = manager;
        }

        public void AddToUpdate(VehicleWeaponBase weapon)
        {
            if(list_update.Contains(weapon))
                return;
            list_update.Add(weapon);
        }

        public void AddToFire(VehicleWeaponBase weapon, VehicleWeaponBase.FiringState state)
        {
            list_fire.Add(new KeyValuePair<VehicleWeaponBase, VehicleWeaponBase.FiringState>(weapon, state));
            updateCounter = 3;

            if(ShouldNetSync && state != VehicleWeaponBase.FiringState.Stop)
                AddToUpdate(weapon);
        }

        public void Clear()
        {
            list_update.Clear();
            list_fire.Clear();
        }

        public void DoNetSync(int vehicleId, int playerId, int seat)
        {
            if(updateCounter < 3 && list_fire.Count == 0)
            {
                updateCounter++;
                return;
            }
            updateCounter = 0;

            if(list_fire.Count > 0 || (ShouldNetSync && list_update.Count > 0))
            {
                byte[] updateData = null;
                byte[] fireData = null;
                
                using (PooledBinaryWriter _bw = MemoryPools.poolBinaryWriter.AllocSync(true))
                {
                    using (PooledExpandableMemoryStream ms = MemoryPools.poolMemoryStream.AllocSync(true))
                    {
                        _bw.SetBaseStream(ms);
                        if(list_update.Count > 0 && ShouldNetSync)
                        {
                            _bw.Write((byte)list_update.Count);
                            foreach(var weapon in list_update)
                            {
                                _bw.Write((byte)weapon.Slot);
                                weapon.InvokeUpdateCallbacks(_bw);
                                weapon.NetSyncWrite(_bw);
                            }
                            updateData = ms.ToArray();
                            ms.Cleanup();
                        }

                        if(list_fire.Count > 0)
                        {
                            _bw.Seek(0, SeekOrigin.Begin);
                            _bw.Write((ushort)list_fire.Count);
                            foreach(var pair in list_fire)
                            {
                                var weapon = pair.Key;
                                _bw.Write((byte)weapon.Slot);
                                _bw.Write((byte)pair.Value);
                                weapon.InvokeFireCallbacks(_bw);
                                weapon.NetFireWrite(_bw, pair.Value);
                            }
                            fireData = ms.ToArray();

                            using (PooledBinaryReader _br = MemoryPools.poolBinaryReader.AllocSync(true))
                            {
                                ms.Seek(0, SeekOrigin.Begin);
                                _br.SetBaseStream(ms);
                                manager.NetSyncFire(seat, _br);
                            }
                        }
                    }
                }

                if(ShouldNetSync)
                {
                    if (ConnectionManager.Instance.IsServer && ConnectionManager.Instance.ClientCount() > 0)
                        ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageVehicleWeaponManagerDataSync>().Setup(vehicleId, seat, updateData, fireData), false, -1, playerId, playerId, 75);
                    else if (ConnectionManager.Instance.IsClient)
                        ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageVehicleWeaponManagerDataSync>().Setup(vehicleId, seat, updateData, fireData));
                }

                Clear();
            }
        }
    }
}

