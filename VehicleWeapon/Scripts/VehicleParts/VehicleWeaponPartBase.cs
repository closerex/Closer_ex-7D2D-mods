public class VehicleWeaponPartBase : VehiclePart
{
    public virtual void NoGUIUpdate(float _dt) { }
    public virtual void NoPauseUpdate(float _dt) { }
    public virtual bool ShouldNetSyncUpdate() { return false; }
    public virtual void NetSyncWrite(PooledBinaryWriter _bw) { }
    public virtual void NetSyncRead(PooledBinaryReader _br) { }
    public virtual void ApplyModEffect(ItemValue vehicleValue) { }
    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        ModName = vehicle.GetName() + "_" + tag;
    }
    public string ModName { get; private set; }
}

