using FullautoLauncher.Scripts.ProjectileManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class PIGSimpleMesh : ProjectileItemGroupAbs<PHSimpleMesh>
{
    private Transform renderTrans;
    private Mesh mesh;
    private RenderParams renderParams;
    private SimpleMeshTransformData data;

    public PIGSimpleMesh(ItemClass item) : base(item)
    {
        renderTrans = item.CloneModel(GameManager.Instance.World, new ItemValue(item.Id), Vector3.zero, null);
        MeshFilter filter = renderTrans.GetComponentInChildren<MeshFilter>();
        mesh = filter.sharedMesh;
        MeshRenderer renderer = renderTrans.GetComponentInChildren<MeshRenderer>();
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
