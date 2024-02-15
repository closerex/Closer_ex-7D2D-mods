using System;
using TMPro;
using UnityEngine;

public class ApexWeaponHudControllerBase : MonoBehaviour
{
    [SerializeField]
    protected ComputeShader cptShader;
    [SerializeField, Range(0, 100)]
    protected int interPerc;
    [SerializeField]
    protected TMP_Text boundText;
    [SerializeField]
    protected TMP_Text[] miscText;
    [SerializeField]
    protected Renderer screenRenderer;
    [SerializeField]
    protected Texture maskTexture;
    [SerializeField]
    protected int matIndex;
    [SerializeField, Range(0, 32)]
    protected int depth = 0;
    [SerializeField]
    protected RenderTextureFormat renderTextureFormat = RenderTextureFormat.Default;
    [SerializeField]
    protected FilterMode filterMode = FilterMode.Point;
    [SerializeField]
    protected UnityEngine.Experimental.Rendering.GraphicsFormat graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB;
    [SerializeField]
    private string kernalName;
    [SerializeField, Range(0, 1)]
    protected float xScale = 1, yScale = 1;
    protected int kernalIndex = -1;
    protected Material mat;
    protected int xGroupCount, yGroupCount;
    protected CustomRenderTexture targetTexture;
    protected static bool shaderEnabled;
    protected static bool stateChecked = false;
    //max count, elem count, inter pixels, map size
    protected readonly int[] dataArray = new int[3];
    protected Color color = Color.white;

    protected static readonly int id_color = Shader.PropertyToID("color");
    protected static readonly int id_dataArray = Shader.PropertyToID("dataArray");
    protected static readonly int id_Mask = Shader.PropertyToID("Mask");
    protected static readonly int id_EmissionMap = Shader.PropertyToID("EmissionMap");
    protected static readonly int id_EmissionColor = Shader.PropertyToID("_EmissionColor");

    protected virtual void Awake()
    {
        if (!stateChecked)
        {
            shaderEnabled = SystemInfo.supportsComputeShaders && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && !Application.isBatchMode;
            stateChecked = true;
            Console.WriteLine("Compute shader support: " + shaderEnabled);
        }

        dataArray[0] = 1;
        dataArray[1] = 0;
        dataArray[2] = interPerc;
        xGroupCount = (int)(maskTexture.width * xScale / 8);
        yGroupCount = (int)(maskTexture.height * yScale / 8);
        mat = screenRenderer.materials[matIndex];
    }

    protected virtual void OnEnable()
    {
        if (!shaderEnabled)
            return;

        if (targetTexture == null)
        {
            targetTexture = new CustomRenderTexture(maskTexture.width, maskTexture.height, renderTextureFormat)
            {
                enableRandomWrite = true,
                updateMode = CustomRenderTextureUpdateMode.OnDemand,
                depth = depth,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = filterMode,
                graphicsFormat = graphicsFormat,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        if (!targetTexture.IsCreated())
            targetTexture.Create();

        mat.SetTexture("_EmissionMap", targetTexture);
        mat.SetColor(id_EmissionColor, Color.white);
        mat.EnableKeyword("_EMISSION");
        if (cptShader.HasKernel(kernalName))
            kernalIndex = cptShader.FindKernel(kernalName);
    }

    protected virtual void OnDisable()
    {

    }

    protected virtual void OnDestroy()
    {
        OnDisable();
        if (targetTexture != null)
            targetTexture.Release();
    }

    public virtual void SetColor(Color color)
    {
        if (boundText != null)
            boundText.color = color;
        if (miscText != null)
            foreach (var t in miscText)
                t.color = color;

        this.color = color;
        //mat.SetColor(id_EmissionColor, color);
        if (CanDispatch())
            Dispatch(dataArray);
    }

    public virtual void SetText(string text)
    {
        if (text.StartsWith("#"))
        {
            dataArray[0] = Mathf.Max(int.Parse(text.Substring(1)), 1);
        }
        else
        {
            if (boundText != null)
                boundText.SetText(text);
            dataArray[1] = int.Parse(text);
        }

        if (CanDispatch())
            Dispatch(dataArray);
    }

    protected virtual bool CanDispatch()
    {
        return shaderEnabled && kernalIndex >= 0;
    }

    protected virtual void Dispatch(int[] dataArray)
    {
        cptShader.SetInts(id_dataArray, dataArray);
        cptShader.SetVector(id_color, color);
        cptShader.SetTexture(kernalIndex, id_Mask, maskTexture);
        cptShader.SetTexture(kernalIndex, id_EmissionMap, targetTexture, 0);
        cptShader.Dispatch(kernalIndex, xGroupCount, yGroupCount, 1);
        //targetTexture.GenerateMips();
        //targetTexture.Update();
    }
}
