public class VehicleWeaponPartBase : VehiclePart
{
    public virtual void NoGUIUpdate(float _dt) { }
    public virtual void NoPauseUpdate(float _dt) { }
    public virtual void ApplyModEffect(ItemValue vehicleValue) { }

    protected string GetModName() { return vehicle.GetName() + "_" + tag; }
}

