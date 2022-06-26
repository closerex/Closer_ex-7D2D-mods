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

    public override void SetupWeaponConnections(in List<VehicleWeaponBase> weapons)
    {
    }

    protected override bool DoFire(bool firstShot, bool isRelease, bool fromSlot)
    {
        return false;
    }

    protected override void Fired()
    {

    }
}

