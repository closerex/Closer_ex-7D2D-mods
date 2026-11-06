using FullautoLauncher.Scripts.ProjectileManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class PIGSimpleMesh : ProjectileItemGroupAbs<PHSimpleMesh>
{
    private AssetManagement.InstanceHandle<GameObject> renderHandle;
    private Mesh mesh;
    private RenderParams renderParams;
    private SimpleMeshTransformData data;

    public PIGSimpleMesh(ItemClass item) : base(item)
    {
        renderHandle = item.CloneModel(new ItemValue(item.Id), null, BlockShape.MeshPurpose.World);
        if (!renderHandle.Instance)
        {
            throw new InvalidOperationException($"Cannot create projectile mesh model for {item.Name}.");
        }
        try
        {
            var renderTrans = renderHandle.Instance.transform;
            MeshFilter filter = renderTrans.GetComponentInChildren<MeshFilter>(true);
            mesh = filter.sharedMesh;
            MeshRenderer renderer = renderTrans.GetComponentInChildren<MeshRenderer>(true);
            renderParams = new RenderParams(renderer.material)
            {
                layer = renderer.gameObject.layer,
                lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                rendererPriority = renderer.rendererPriority,
                renderingLayerMask = renderer.renderingLayerMask,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };
            data = new SimpleMeshTransformData(renderer.localToWorldMatrix, mesh.bounds);
        }
        catch
        {
            renderHandle.Destroy();
            renderHandle = default;
            throw;
        }
    }

    public override void Cleanup()
    {
        base.Cleanup();
        queue_pool_projectile.Clear();
        dict_fired_projectiles.Clear();
        var handle = renderHandle;
        renderHandle = default;
        handle.Destroy();
    }

    public override void Update()
    {
        if (GameManager.IsDedicatedServer)
        {
            return;
        }

        foreach (var set in dict_fired_projectiles.Values)
        {
            foreach (var projectile in set)
            {
                Graphics.RenderMesh(in projectile.renderParams, mesh, 0, projectile.finalMat);
            }
        }
    }

    protected override PHSimpleMesh Create(ProjectileParams par)
    {
        return new PHSimpleMesh(par, data, in renderParams);
    }
}
