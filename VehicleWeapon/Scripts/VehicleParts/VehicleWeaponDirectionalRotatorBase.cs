using UnityEngine;

public class VehicleWeaponDirectionalRotatorBase : VehicleWeaponRotatorBase
{
    protected Transform indicatorTrans;

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        
        indicatorTrans = GetTransform("indicatorTransform");
    }

    public override void CreatePreview()
    {
        if (indicatorTrans != null)
            indicatorTrans.gameObject.SetActive(true);
    }

    public override void DestroyPreview()
    {
        if (indicatorTrans != null)
            indicatorTrans.gameObject.SetActive(false);
    }
}
