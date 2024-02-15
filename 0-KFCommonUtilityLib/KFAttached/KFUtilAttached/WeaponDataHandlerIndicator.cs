using UnityEngine;

public class WeaponDataHandlerIndicator : WeaponDataHandlerCanvasMask
{
    [SerializeField]
    protected RectTransform indicator;
    [SerializeField]
    protected float offset;
    [SerializeField]
    protected Color normalColor;
    [SerializeField]
    protected Color warningColor;

    protected float level = 0;

    //private void Start()
    //{
    //    LayoutRebuilder.MarkLayoutForRebuild(indicator.parent.GetComponent<RectTransform>());
    //}

    //protected override void OnEnable()
    //{
    //    base.OnEnable();
    //    LayoutRebuilder.MarkLayoutForRebuild(indicator.parent.GetComponent<RectTransform>());
    //}

    public override void SetText(string text)
    {
        if (text.StartsWith("$"))
        {
            level = Mathf.Clamp01(float.Parse(text.Substring(1)) / maxVal);
            Vector3 pos = indicator.anchoredPosition;
            pos.y = mask.rectTransform.rect.height * level + offset;
            indicator.anchoredPosition = pos;
            //updated = true;
        }
        else
        {
            base.SetText(text);
        }
        //Log.Out($"Setting text {text} max {maxVal} cur {curVal} level {level} indicator position {indicator.position.y} mask position {mask.padding.w}");
        if (curVal / maxVal < level)
        {
            SetColor(warningColor);
        }
        else
        {
            SetColor(normalColor);
        }
    }

    //protected override void LateUpdate()
    //{
    //    if (updated)
    //    {
    //        LayoutRebuilder.ForceRebuildLayoutImmediate(indicator.parent.GetComponent<RectTransform>());
    //    }
    //    base.LateUpdate();
    //}
}
