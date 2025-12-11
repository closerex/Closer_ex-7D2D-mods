using KFCommonUtilityLib;
using UnityEngine;

public class LaserSight : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public Transform laserOrigin;
    public string laserOriginName = "laserOrigin";
    public float maxDistance = 100f;
    public float maxDotSizeDistance = 50f;
    public LayerMask collisionMask = 1353162769;
    public bool useAkimboOrigin;
    public Transform defaultAkimboOrigin;
    public string akimboOriginName = "akimboLaserOrigin";

    [Header("Dot Settings")]
    public float dotBaseSizeNear = 0.03f;
    public float dotBaseSizeFar = 0.3f;
    public float intensityFlickerSpeed = 5f;
    public float intensityFlickerMax = 0.1f;
    public float clippingOffset = 0.007f;
    [Header("Material Settings")]
    public Material dotMaterial;
    [ColorUsage(true)]
    public Color dotBaseColor = new (.7f, .1f, .1f, 1f);
    public float dotBaseIntensity = 0;
    public string dotColorPropertyName = "_EmissionColor";

    private static Mesh dotMesh;
    private Transform dotTrans;
    private MeshRenderer dotRenderer;
    private MeshFilter dotFilter;
    private MaterialPropertyBlock props;
#if NotEditor
    private EntityPlayerLocal player;
    private ScopeBase scopeBase;
#endif

    //handle external attached prefab origin search

    void Awake()
    {
        if (!lineRenderer || !dotMaterial)
        {
            Destroy(this);
            return;
        }
        // Create the quad mesh (XY plane)
        if (!dotMesh)
        {
            dotMesh = new Mesh();
            dotMesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0)
            };
            dotMesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            dotMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            dotMesh.RecalculateNormals();
        }

        // Create the dot GameObject
        var dotObject = new GameObject("LaserDot");
        dotObject.layer = 24;
        dotTrans = dotObject.transform;
        dotTrans.SetParent(transform, true);

        dotFilter = dotObject.AddComponent<MeshFilter>();
        dotFilter.mesh = dotMesh;

        dotRenderer = dotObject.AddComponent<MeshRenderer>();
        dotRenderer.material = dotMaterial;
        dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dotRenderer.receiveShadows = false;
        dotRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        dotRenderer.enabled = false;

        props = new MaterialPropertyBlock();

        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = true;
#if NotEditor
        player = transform.GetLocalPlayerInParent();
        if (player)
        {
            var targets = AnimationRiggingManager.GetActiveRigTargetsFromPlayer(player);
            if (targets && targets.IsAnimationSet)
            {
                if (targets.IsFpv)
                {
                    scopeBase = targets.ItemFpv.GetComponentInChildren<ScopeBase>();
                }
            }
            if (!laserOrigin)
            {
                laserOrigin = AnimationRiggingManager.GetTransformOverrideByName(player.inventory.holdingItemData.model, laserOriginName);
            }
            if (useAkimboOrigin && !defaultAkimboOrigin)
            {
                defaultAkimboOrigin = AnimationRiggingManager.GetTransformOverrideByName(player.inventory.holdingItemData.model, akimboOriginName);
            }
        }
        if (!laserOrigin)
        {
            laserOrigin = transform;
        }
#endif
    }

    void LateUpdate()
    {
        bool dynamicOriginSet = false;
        Vector3 origin = Vector3.zero;
        Vector3 direction = Vector3.forward;
#if NotEditor
        if (scopeBase && scopeBase.aimingModule != null)
        {
            var aimingModule = scopeBase.aimingModule;
            var dynamicLaserOrigin = aimingModule.CurAimRef;
            if (dynamicLaserOrigin)
            {
                Transform dynamicOriginTrans;
                if (useAkimboOrigin && (dynamicLaserOrigin.akimboLaserOriginOverride || defaultAkimboOrigin))
                {
                    dynamicOriginTrans = dynamicLaserOrigin.akimboLaserOriginOverride ?? defaultAkimboOrigin;
                }
                else
                {
                    dynamicOriginTrans = dynamicLaserOrigin.laserOriginOverride ?? dynamicLaserOrigin.transform;
                }
                origin = Vector3.Lerp(laserOrigin.position, dynamicOriginTrans.position, aimingModule.aimProcValue);
                direction = Vector3.Slerp(laserOrigin.forward, dynamicOriginTrans.forward, aimingModule.aimProcValue);
                dynamicOriginSet = true;
            }
        }
#endif
        if (!dynamicOriginSet)
        {
            if (!laserOrigin)
            {
                return;
            }
            origin = laserOrigin.position;
            direction = laserOrigin.forward;
        }

#if NotEditor
        int layer = 0;
        if (player != null)
        {
            layer = player.GetModelLayer();
            player.SetModelLayer(2);
        }
#endif
        Vector3 endPoint = origin + direction * maxDistance;
        bool laserHit = Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, collisionMask);
        if (laserHit)
        {
            //minDotVal = Mathf.Cos(Mathf.Deg2Rad * maxAdjustmentAngle);
            if (Vector3.Dot(transform.forward, hit.point - transform.position) > 0)
            {
                endPoint = hit.point + hit.normal * clippingOffset;
            }
        }

        bool checkHit = false;
        if (dynamicOriginSet)
        {
            checkHit = Physics.Raycast(transform.position, endPoint - transform.position, out RaycastHit hitCheck, Vector3.Distance(transform.position, endPoint), collisionMask);
            if (checkHit)
            {
                hit = hitCheck;
                endPoint = hit.point + hit.normal * clippingOffset;
            }
        }

#if NotEditor
        if (player != null)
        {
            player.SetModelLayer(layer);
        }
#endif

        if (laserHit || checkHit)
        {
            Vector3 localImpact = transform.InverseTransformPoint(endPoint);

            // Flicker intensity
            float flicker = Mathf.PerlinNoise(Time.time * intensityFlickerSpeed, 0f) * intensityFlickerMax;
            float intensity = Mathf.Pow(2, dotBaseIntensity + flicker);
            Color emissive = new Color(dotBaseColor.r * intensity,
                                       dotBaseColor.g * intensity,
                                       dotBaseColor.b * intensity,
                                       dotBaseColor.a);
            //if (string.IsNullOrEmpty(dotIntensityPropertyName))
            //{
            //    emissive = dotBaseColor * intensity;
            //}
            //else
            //{
            //    props.SetFloat(dotIntensityPropertyName, intensity);
            //}

            props.SetColor(dotColorPropertyName, emissive);
            dotRenderer.SetPropertyBlock(props);

            dotTrans.SetParent(null, true);
            dotTrans.position = endPoint;
            dotTrans.forward = -hit.normal;
            dotTrans.localScale = Vector3.one * Mathf.Lerp(dotBaseSizeNear, dotBaseSizeFar, hit.distance / maxDotSizeDistance);
            dotTrans.SetParent(transform, true);
            dotRenderer.enabled = true;
        }
        else
        {
            dotRenderer.enabled = false;
        }

        // Update beam
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, endPoint);
    }

    void OnValidate()
    {
        if (!lineRenderer)
            Debug.LogWarning("LaserSight: Missing LineRenderer!", this);
        if (!dotMaterial)
            Debug.LogWarning("LaserSight: Missing dotMaterial!", this);
    }
}
