using UnityEngine;

[AddComponentMenu("KFAttachments/Weapon Display Controllers/Weapon Label Controller TextMesh")]
public class WeaponLabelController : WeaponLabelControllerBase
{
    [SerializeField]
    private TextMesh[] labels;

    public override bool setLabelText(int index, string data)
    {
        if (labels == null || labels.Length <= index || !labels[index] || !labels[index].gameObject.activeSelf)
            return false;
        labels[index].text = data;
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
