using UnityEngine;
using UnityEngine.Rendering;

public class RFX4_MobileDistortion : MonoBehaviour
{
    public bool IsActive = true;

    private CommandBuffer buf;
    private Camera cam;
    private bool bufferIsAdded;

    void Awake()
    {
        cam = GetComponent<Camera>();
        CreateBuffer();
    }

    void CreateBuffer()
    {
       // CreateCommandBuffer(Camera.main, CameraEvent.BeforeForwardAlpha, "_GrabTextureMobile");
        var cam = Camera.main;
        buf = new CommandBuffer();
        buf.name = "_GrabOpaqueColor";

        int screenCopyId = Shader.PropertyToID("_ScreenCopyOpaqueColor");
        //var scale = IsSupportedHdr() ? -2 : -1;
        var scale = -1;
        var rtFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB565)
            ? RenderTextureFormat.RGB565
            : RenderTextureFormat.Default;
        buf.GetTemporaryRT(screenCopyId, scale, scale, 0, FilterMode.Bilinear, rtFormat);
        //buf.get
        buf.Blit(BuiltinRenderTextureType.CurrentActive, screenCopyId);

        buf.SetGlobalTexture("_GrabTexture", screenCopyId);
        buf.SetGlobalTexture("_GrabTextureMobile", screenCopyId);
        //buf.SetGlobalFloat("_GrabTextureMobileScale", (1.0f / scale) * -1);
       // cam.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, buf);
    }

    void OnEnable()
    {
        AddBuffer();
    }

    void OnDisable()
    {
        RemoveBuffer();
    }

    void AddBuffer()
    {
        cam.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, buf);
        bufferIsAdded = true;
    }

    void RemoveBuffer()
    {
        cam.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, buf);
        bufferIsAdded = false;
    }

    void Update()
    {
        if (IsActive)
        {
            if (!bufferIsAdded)
            {
                AddBuffer();
            }
        }
        else
        {
            if(bufferIsAdded) RemoveBuffer();
        }
    }

    bool IsSupportedHdr()
    {
        return Camera.main.allowHDR;
    }
}
