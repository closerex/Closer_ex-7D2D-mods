using UnityEngine;

public class WeaponLabelControllerDevotion : WeaponLabelControllerBase
{
    [SerializeField]
    private ApexWeaponHudControllerBase[] controllers;
    public override bool setLabelColor(int index, Color color)
    {
        if (controllers == null || index >= controllers.Length || !controllers[index].gameObject.activeSelf)
            return false;

        controllers[index].SetColor(color);
        return true;
    }

    public override bool setLabelText(int index, string data)
    {
        if (controllers == null || index >= controllers.Length || !controllers[index].gameObject.activeSelf)
            return false;
        
        controllers[index].SetText(data);
        return true;
    }
}
