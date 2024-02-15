using TMPro;
using UnityEngine;

public class WeaponDataHandlerTMP : WeaponDataHandlerBase
{
    [SerializeField]
    private TMP_Text label;
    public override void SetColor(Color color)
    {
        label.color = color;
    }

    public override void SetText(string text)
    {
        label.SetText(text);
    }
}
