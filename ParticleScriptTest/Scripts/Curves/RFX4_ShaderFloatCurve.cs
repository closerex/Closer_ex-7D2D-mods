using UnityEngine;

public class RFX4_ShaderFloatCurve : MonoBehaviour {

    public RFX4_ShaderProperties ShaderFloatProperty = RFX4_ShaderProperties._Cutout;
    public AnimationCurve FloatCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float GraphTimeMultiplier = 1, GraphIntensityMultiplier = 1;
    public bool IsLoop;
    //public bool UseSharedMaterial;

    private bool canUpdate;
    private float startTime;
    //private Material mat;
    private int propertyID;
    private string shaderProperty;
    private bool isInitialized;

    private MaterialPropertyBlock props;
    private Renderer rend;

    private void Awake()
    {
        if (props == null) props = new MaterialPropertyBlock();
        if (rend == null) rend = GetComponent<Renderer>();

        shaderProperty = ShaderFloatProperty.ToString();
        propertyID = Shader.PropertyToID(shaderProperty);
    }

    private void OnEnable()
    {
        startTime = Time.time;
        canUpdate = true;

        rend.GetPropertyBlock(props);

        var eval = FloatCurve.Evaluate(0) * GraphIntensityMultiplier;
        props.SetFloat(propertyID, eval);

        rend.SetPropertyBlock(props);
    }

    private void Update()
    {
        rend.GetPropertyBlock(props);

        var time = Time.time - startTime;
        if (canUpdate)
        {
            var eval = FloatCurve.Evaluate(time / GraphTimeMultiplier) * GraphIntensityMultiplier;
            props.SetFloat(propertyID, eval);
        }
        if (time >= GraphTimeMultiplier)
        {
            if (IsLoop) startTime = Time.time;
            else canUpdate = false;
        }

        rend.SetPropertyBlock(props);
    }
}
