using UnityEngine;

public class LaserSight : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public Transform laserOrigin;
    public float maxDistance = 100f;
    public LayerMask collisionMask;

    [Header("Dot Settings")]
    public Material dotMaterial;
    public float dotSizeNear = 0.01f;
    public float dotSizeFar = 0.45f;
    public float dotBaseIntensity = 0.75f;
    public float flickerSpeed = 5f;
    public float flickerAmount = 1f;
    public float clippingOffset = 0.007f;
    [Range(0f, 90f)]
    public float maxAdjustmentAngle = 30f;

    private static Mesh dotMesh;
    private GameObject dotObject;
    private MeshRenderer dotRenderer;
    private MeshFilter dotFilter;
    private MaterialPropertyBlock props;
    private float minDotVal;

    void Awake()
    {
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

        // Create the dot GameObject under the laserOrigin
        dotObject = new GameObject("LaserDot");
        dotObject.transform.SetParent(laserOrigin, false);

        dotFilter = dotObject.AddComponent<MeshFilter>();
        dotFilter.mesh = dotMesh;

        dotRenderer = dotObject.AddComponent<MeshRenderer>();
        dotRenderer.material = dotMaterial;

        props = new MaterialPropertyBlock();

        lineRenderer.useWorldSpace = true;
        minDotVal = Mathf.Cos(Mathf.Deg2Rad * maxAdjustmentAngle);

        dotRenderer.enabled = false;
        lineRenderer.enabled = false;
    }

    void LateUpdate()
    {
        if (!lineRenderer || !dotMaterial || laserOrigin == null)
            return;

        Vector3 origin = laserOrigin.position;
        Vector3 direction = laserOrigin.forward;
        Vector3 endPoint = origin + direction * maxDistance;
        bool laserHit = Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, collisionMask);

        if (laserHit)
        {
            Vector3 laserDirection = hit.point - transform.position;
            if (Vector3.Dot(transform.forward, laserDirection) <= minDotVal)
            {
                laserHit = false;
            }
        }

        if (laserHit)
        {
            Vector3 impactWorld = hit.point + hit.normal * clippingOffset;
            Vector3 localImpact = laserOrigin.InverseTransformPoint(impactWorld);
            endPoint = impactWorld;

            float distance = Vector3.Distance(laserOrigin.position, impactWorld);
            float dotSize = Mathf.Lerp(dotSizeNear, dotSizeFar, distance / maxDistance);

            // Flicker intensity
            float flicker = 1f + Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * flickerAmount;
            float intensity = dotBaseIntensity * flicker;
            Color emissive = Color.red * intensity;

            props.SetColor("_Color", emissive);
            props.SetFloat("_Emissive", intensity);
            dotRenderer.SetPropertyBlock(props);

            dotObject.transform.localPosition = localImpact;
            dotObject.transform.forward = -hit.normal;
            dotObject.transform.localScale = Vector3.one * dotSize;
            dotRenderer.enabled = true;
        }
        else
        {
            dotRenderer.enabled = false;
        }

        // Update beam
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, endPoint);
        lineRenderer.enabled = true;
    }

    void OnValidate()
    {
        if (!lineRenderer)
            Debug.LogWarning("LaserSight: Missing LineRenderer!", this);
        if (!dotMaterial)
            Debug.LogWarning("LaserSight: Missing dotMaterial!", this);
    }
}
