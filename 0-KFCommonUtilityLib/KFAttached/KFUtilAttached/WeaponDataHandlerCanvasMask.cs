using UnityEngine;
using UnityEngine.UI;

public class WeaponDataHandlerCanvasMask : WeaponDataHandlerBase
{
    [SerializeField]
    protected RectMask2D mask;
    [SerializeField]
    protected Image image;

    protected float maxVal = 1, curVal = 1;
    //protected bool updated = true;

    //protected virtual void OnEnable()
    //{
    //    LayoutRebuilder.MarkLayoutForRebuild(mask.rectTransform);
    //}
    
    public override void SetColor(Color color)
    {
        image.color = color;
    }

    public override void SetText(string text)
    {
        if (text.StartsWith("#"))
        {
            maxVal = Mathf.Max(float.Parse(text.Substring(1)), 1);
        }
        else
        {
            curVal = Mathf.Max(float.Parse(text), 0);
        }
        if (curVal > maxVal)
            maxVal = curVal;
        float perc = curVal / maxVal;
        Vector4 padding = mask.padding;
        padding.w = mask.rectTransform.rect.height * Mathf.Clamp01(1 - perc);
        mask.padding = padding;
        //updated = true;
    }

    //protected virtual void LateUpdate()
    //{
    //    if (updated)
    //    {
    //        LayoutRebuilder.ForceRebuildLayoutImmediate(mask.rectTransform);
    //        updated = false;
    //    }
    //}
}
