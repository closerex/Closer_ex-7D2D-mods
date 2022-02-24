using UnityEngine;

[ExecuteInEditMode]
public class RFX4_UVAnimation : MonoBehaviour
{
    public int TilesX = 4;
    public int TilesY = 4;
    [Range(1, 360)]
    public int FPS = 32;
    public int StartFrameOffset;
    public bool IsLoop = true;
    public bool IsReverse;
    public bool IsInterpolateFrames = true;
    public RFX4_TextureShaderProperties[] TextureNames = { RFX4_TextureShaderProperties._MainTex };

   // public AnimationCurve FrameOverTime = AnimationCurve.Linear(0, 1, 1, 1);

    private int count;
    private Renderer currentRenderer;
    private Projector projector;
    private Material instanceMaterial;
    private float animationStartTime;
    private bool canUpdate;
    private int previousIndex;
    private int totalFrames;
    private float currentInterpolatedTime;
    private int currentIndex;
    private Vector2 size;
    private bool isInitialized;

    private void OnEnable()
    {
        if (isInitialized) InitDefaultVariables();
    }

    private void Start()
    {
        InitDefaultVariables();
        isInitialized = true;
    }

    private void OnWillRenderObject()
    {
        if (!Application.isPlaying) ManualUpdate();
    }

    void Update()
    {
        if (Application.isPlaying) ManualUpdate();
    }

    private void InitDefaultVariables()
    {
        currentRenderer = GetComponent<Renderer>();
        UpdateMaterial();

        totalFrames = TilesX * TilesY;
        previousIndex = 0;
        canUpdate = true;
        count = TilesY * TilesX;
        var offset = Vector3.zero;
        StartFrameOffset = StartFrameOffset - (StartFrameOffset / count) * count;
        size = new Vector2(1f / TilesX, 1f / TilesY);
        animationStartTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup ;
        if (instanceMaterial != null)
        {
            foreach (var textureName in TextureNames) {
                instanceMaterial.SetTextureScale(textureName.ToString(), size);
                instanceMaterial.SetTextureOffset(textureName.ToString(), offset);
            }
        }
    }

    private void ManualUpdate()
    {
        if (!canUpdate) return;
        UpdateMaterial();
        SetSpriteAnimation();
        if (IsInterpolateFrames)
            SetSpriteAnimationIterpolated();
    }

    private void UpdateMaterial()
    {
        if (currentRenderer == null) return;
        if (Application.isPlaying) instanceMaterial = currentRenderer.material;
        instanceMaterial = currentRenderer.sharedMaterial;
        if (IsInterpolateFrames) instanceMaterial.EnableKeyword("USE_SCRIPT_FRAMEBLENDING");
        else instanceMaterial.DisableKeyword("USE_SCRIPT_FRAMEBLENDING");
    }

    void SetSpriteAnimation()
    {
        var time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        int index = (int)((time - animationStartTime) * FPS);
        index = index % totalFrames;

        if (!IsLoop && index < previousIndex)
        {
            canUpdate = false;
            return;
        }

        if (IsInterpolateFrames && index != previousIndex)
        {
            currentInterpolatedTime = 0;
        }
        previousIndex = index;

        if (IsReverse)
            index = totalFrames - index - 1;

        var uIndex = index % TilesX;
        var vIndex = index / TilesX;

        float offsetX = uIndex * size.x;
        float offsetY = (1.0f - size.y) - vIndex * size.y;
        var offset = new Vector2(offsetX, offsetY);

        if (instanceMaterial != null)
        {
            foreach (var textureName in TextureNames)
            {
                instanceMaterial.SetTextureScale(textureName.ToString(), size);
                instanceMaterial.SetTextureOffset(textureName.ToString(), offset);
            }
        }
    }

    float prevRealTime;
    public float DeltaTime()
    {
        if (Application.isPlaying)
        {
            return Time.deltaTime;
        }
        else
        {
            var delta = Time.realtimeSinceStartup - prevRealTime;
            prevRealTime = Time.realtimeSinceStartup;
            return delta;
        }
    }



    private void SetSpriteAnimationIterpolated()
    {
        currentInterpolatedTime += DeltaTime();

        var nextIndex = previousIndex + 1;
        if (nextIndex == totalFrames)
            nextIndex = previousIndex;
        if (IsReverse)
            nextIndex = totalFrames - nextIndex - 1;

        var uIndex = nextIndex%TilesX;
        var vIndex = nextIndex/TilesX;

        float offsetX = uIndex*size.x;
        float offsetY = (1.0f - size.y) - vIndex*size.y;
        var offset = new Vector2(offsetX, offsetY);
        if (instanceMaterial != null)
        {
            instanceMaterial.SetVector("_MainTex_NextFrame", new Vector4(size.x, size.y, offset.x, offset.y));
            instanceMaterial.SetFloat("InterpolationValue", Mathf.Clamp01(currentInterpolatedTime*FPS));
        }
    }
}