using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class VPWeapon : VehiclePart
{
    private Transform joint;
    private int slot = -1;
    private ItemValue weaponValue;

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        joint = GetTransform();
        _properties.ParseInt("slot", ref slot);
        string weaponName = string.Empty;
        _properties.ParseString("weapon", ref weaponName);
        if(!string.IsNullOrEmpty(weaponName))
        {
            ItemClass item = ItemClass.GetItemClass(weaponName);
        }
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
    }
}

