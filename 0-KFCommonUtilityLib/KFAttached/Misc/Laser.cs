using KFCommonUtilityLib;
using UnityEngine;

public class LaserSight : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public Transform laserOrigin;
    public string laserOriginName = "laserOrigin";
    public bool useAkimboOrigin;
    public Transform defaultAkimboOrigin;
    public string akimboOriginName = "akimboLaserOrigin";

    [Header("Dot Settings")]
    [ColorUsage(false)]
    public Color dotBaseColor = new (.7f, .1f, .1f, 1f);
    public float dotIntensityBase = 7.5f;
    public float intensityFlickerSpeed = 5f;
    public float intensityFlickerMax = 0.25f;

    private float maxDistance = 100f;
    private float dotSizeFarDistance = 15f;
    private float dotSizeBase = 0.01f;
    [Range(0f, 1f)]
    private float dotSizeFar = .75f;
    private float dotProjectionRange = 0.25f;

    private static LayerMask collisionMask = -538750997;
    private static int intensityPropID = Shader.PropertyToID("_MaxIntensity");
    private Transform dotTrans;
    private Material dotMaterial;

#if NotEditor
    private EntityPlayerLocal player;
    private ScopeBase scopeBase;
#endif

    void Awake()
    {
        if (!lineRenderer)
        {
            Destroy(this);
            return;
        }

        lineRenderer.transform.AddMissingComponent<LaserReferenced>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = true;
    }

    // Create the dot GameObject
    private void CreateDot()
    {
        var dotObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        dotObject.name = "LaserDot";
        dotTrans = dotObject.transform;
        dotTrans.localScale = new Vector3(dotSizeBase, dotSizeBase, dotProjectionRange);
        Destroy(dotObject.GetComponent<Collider>());
        var dotRenderer = dotObject.GetComponent<MeshRenderer>();
#if NotEditor
        dotRenderer.material = SharedAssets.DefaultLaserDotMaterial;
#endif
        dotMaterial = dotRenderer.material;
        dotMaterial.color = dotBaseColor;
        dotRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dotRenderer.receiveShadows = false;
        dotRenderer.allowOcclusionWhenDynamic = false;
    }

    private void OnEnable()
    {
#if NotEditor
        player = transform.GetLocalPlayerInParent();
        if (player)
        {
            var targets = AnimationRiggingManager.GetActiveRigTargetsFromPlayer(player);
            if (player.bFirstPersonView && targets.ItemFpv)
            {
                scopeBase = targets.ItemFpv.GetComponentInChildren<ScopeBase>();
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
        CreateDot();
    }

    private void OnDisable()
    {
        if (dotTrans)
        { 
            Destroy(dotTrans.gameObject);
        }
    }

    void LateUpdate()
    {
        bool dynamicOriginSet = false;
        Vector3 origin = Vector3.zero;
        Vector3 direction = Vector3.forward;
#if NotEditor
        if (scopeBase && scopeBase.aimingModule != null && player.bFirstPersonView)
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

                if (dynamicOriginTrans == dynamicLaserOrigin.transform)
                {
                    aimingModule.CalcCurrentWorldPos(out Vector3 curAimRefPosWorld, out Quaternion curAimRefRotWorld);
                    origin = Vector3.Lerp(laserOrigin.position, curAimRefPosWorld, aimingModule.CurAimProcValue);
                    direction = Quaternion.Slerp(laserOrigin.rotation, curAimRefRotWorld, aimingModule.CurAimProcValue) * Vector3.forward;
                }
                else
                {
                    origin = Vector3.Lerp(laserOrigin.position, dynamicOriginTrans.position, aimingModule.CurAimProcValue);
                    direction = Vector3.Slerp(laserOrigin.forward, dynamicOriginTrans.forward, aimingModule.CurAimProcValue);
                }

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
        bool laserHit =
#if NotEditor
            Voxel.Raycast(GameManager.Instance.World, new(origin + Origin.position, direction), maxDistance, collisionMask, 8, 0f);
        if (laserHit)
        {
            //minDotVal = Mathf.Cos(Mathf.Deg2Rad * maxAdjustmentAngle);
            if (Vector3.Dot(transform.forward, Voxel.phyxRaycastHit.point - transform.position) > 0)
            {
                endPoint = Voxel.phyxRaycastHit.point;
            }
        }
#else
            Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, collisionMask);
        if (laserHit)
        {
            //minDotVal = Mathf.Cos(Mathf.Deg2Rad * maxAdjustmentAngle);
            if (Vector3.Dot(transform.forward, hit.point - transform.position) > 0)
            {
                endPoint = hit.point;
            }
        }
#endif

        bool checkHit = false;
        if (dynamicOriginSet)
        {
            checkHit =
#if NotEditor
                Voxel.Raycast(GameManager.Instance.World, new(transform.position + Origin.position, endPoint - transform.position), Vector3.Distance(transform.position, endPoint), collisionMask, 8, 0f);
            if (checkHit)
            {
                endPoint = Voxel.phyxRaycastHit.point;
            }
#else
                Physics.Raycast(transform.position, endPoint - transform.position, out RaycastHit hitCheck, Vector3.Distance(transform.position, endPoint), collisionMask);
            if (checkHit)
            {
                endPoint = hitCheck.point;
            }
#endif
        }

#if NotEditor
        if (player != null)
        {
            player.SetModelLayer(layer);
        }
#endif

        if (laserHit || checkHit)
        {
#if NotEditor
            // Flicker intensity
            dotMaterial.SetFloat(intensityPropID, dotIntensityBase + Mathf.PerlinNoise(Time.time * intensityFlickerSpeed, 0f) * intensityFlickerMax);
#endif
            if (Camera.main)
            {
                Transform cameraTrans = Camera.main.transform;
                float hitDistance = Vector3.Dot(endPoint - cameraTrans.position, cameraTrans.forward);
                float dotSizeReal = dotSizeBase * Mathf.Clamp(hitDistance, .75f, dotSizeFarDistance) * Mathf.Lerp(1, dotSizeFar, hitDistance / dotSizeFarDistance);
                dotTrans.localScale = new(dotSizeReal, dotSizeReal, dotProjectionRange);
                dotTrans.rotation = cameraTrans.rotation;
            }
            //dotTrans.forward = (endPoint - (Camera.main ? Camera.main.transform.position : transform.position)).normalized;
            dotTrans.position = endPoint;
            dotTrans.gameObject.SetActive(true);
        }
        else
        {
            dotTrans.gameObject.SetActive(false);
        }

        // Update beam
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, endPoint);
    }

    void OnValidate()
    {
        if (!lineRenderer)
            Debug.LogWarning("LaserSight: Missing LineRenderer!", this);
    }
}
