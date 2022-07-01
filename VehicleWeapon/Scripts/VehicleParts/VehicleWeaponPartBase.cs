public class VehicleWeaponPartBase : VehiclePart
{
    public virtual void NoGUIUpdate(float _dt) { }
    public virtual void NoPauseUpdate(float _dt) { }
    public virtual void ApplyModEffect(ItemValue vehicleValue) { InitModProperties(); }

    protected virtual void InitModProperties() { }
    protected string GetModName() { return vehicle.GetName() + "_" + tag; }
}

