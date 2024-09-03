using TMPro;
using UnityEngine;

[AddComponentMenu("KFAttachments/Weapon Display Controllers/Weapon Text Controller TMP")]
public class WeaponTextProController : WeaponLabelControllerBase
{
    [SerializeField]
    private TMP_Text[] labels;

    public override bool setLabelText(int index, string data)
    {
        if (labels == null || labels.Length <= index || !labels[index] || !labels[index].gameObject.activeSelf)
            return false;
        labels[index].SetText(data);
        return true;
    }

    public override bool setLabelColor(int index, Color color)
    {
        if (labels == null || labels.Length <= index || !labels[index] || !labels[index].gameObject.activeSelf)
            return false;
        labels[index].color = color;
        return true;
    }
}
