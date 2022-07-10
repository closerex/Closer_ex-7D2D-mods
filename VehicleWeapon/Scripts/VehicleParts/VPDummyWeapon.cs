using System.Collections.Generic;

public class VPDummyWeapon : VehicleWeaponBase
{
    List<VehicleWeaponPartBase> list_parts;
    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();
        string str = null;
        properties.ParseString("CustomParts", ref str);
        if(!string.IsNullOrEmpty(str))
        {
            string[] parts = str.Split(',');
            foreach(string part in parts)
            {
                VehicleWeaponPartBase partBase = vehicle.FindPart(part) as VehicleWeaponPartBase;
                if(partBase != null)
                {
                    if (list_parts == null)
                        list_parts = new List<VehicleWeaponPartBase>();
                    list_parts.Add(partBase);
                }
            }
        }
    }

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        base.ApplyModEffect(vehicleValue);
        if (list_parts != null)
            foreach (var part in list_parts)
                part.ApplyModEffect(vehicleValue);
    }

    public override void NoGUIUpdate(float _dt)
    {
        base.NoGUIUpdate(_dt);
        if(list_parts != null)
        {
            foreach (VehicleWeaponPartBase part in list_parts)
                part.NoGUIUpdate(_dt);
        }
    }

    public override void NoPauseUpdate(float _dt)
    {
        base.NoPauseUpdate(_dt);
        if(list_parts != null)
        {
            foreach (VehicleWeaponPartBase part in list_parts)
                part.NoPauseUpdate(_dt);
        }
    }

    protected internal override bool CanFire(int flags, bool isRelease, out bool forceStop)
    {
        forceStop = false;
        return false;
    }
}

