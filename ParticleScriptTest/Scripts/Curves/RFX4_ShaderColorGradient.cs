using UnityEngine;

public class RFX4_ShaderColorGradient : MonoBehaviour {

    public RFX4_ShaderProperties ShaderColorProperty = RFX4_ShaderProperties._TintColor;
    public Gradient Color = new Gradient();
    public float TimeMultiplier = 1;
    public bool IsLoop;
    //public bool UseSharedMaterial;
    [HideInInspector] public float HUE = -1;

    [HideInInspector]
    public bool canUpdate;
    //private Material mat;
    private int propertyID;
    private float startTime;
    private Color startColor;
  
    private bool isInitialized;
    private string shaderProperty;

    private MaterialPropertyBlock props;
    private Renderer rend;

    void Awake()
    {
        if (props == null) props = new MaterialPropertyBlock();
        if (rend == null) rend = GetComponent<Renderer>();

        shaderProperty = ShaderColorProperty.ToString();
        propertyID = Shader.PropertyToID(shaderProperty);
        startColor = rend.sharedMaterial.GetColor(propertyID);
    }


    private void OnEnable()
    {
        startTime = Time.time;
        canUpdate = true;
      
        rend.GetPropertyBlock(props);

        startColor = rend.sharedMaterial.GetColor(propertyID);
        props.SetColor(propertyID, startColor * Color.Evaluate(0));

        rend.SetPropertyBlock(props);
    }

    private void Update()
    {
        rend.GetPropertyBlock(props);

        var time = Time.time - startTime;
        if (canUpdate)
        {
            var eval = Color.Evaluate(time / TimeMultiplier);
            if (HUE > -0.9f)
            {
                eval = RFX4_ColorHelper.ConvertRGBColorByHUE(eval, HUE);
                startColor = RFX4_ColorHelper.ConvertRGBColorByHUE(startColor, HUE);
            }
            props.SetColor(propertyID, eval * startColor);
        }
        if (time>= TimeMultiplier) {
            if (IsLoop) startTime = Time.time;
            else canUpdate = false;
        }
       
       rend.SetPropertyBlock(props);
    }

}
