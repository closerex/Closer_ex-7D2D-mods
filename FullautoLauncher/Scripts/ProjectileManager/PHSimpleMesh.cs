using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static AstarManager;

namespace FullautoLauncher.Scripts.ProjectileManager
{
    public class SimpleMeshTransformData
    {
        public readonly Matrix4x4 TRS;
        public readonly Bounds bounds;

        public SimpleMeshTransformData(Matrix4x4 TRS, Bounds bounds)
        {
            this.TRS = TRS;
            this.bounds = bounds;
        }
    }

    public class PHSimpleMesh : ParameterHolderAbs
    {
        public Matrix4x4 finalMat;
        public RenderParams renderParams;
        private SimpleMeshTransformData data;

        public PHSimpleMesh(ProjectileParams par, SimpleMeshTransformData data, in RenderParams renderPar) : base(par)
        {
            this.data = data;
            this.renderParams = renderPar;
        }

        public override void Fire()
        {
            UpdatePosition();
        }

        public override void UpdatePosition()
        {
            finalMat = Matrix4x4.Translate(par.renderPosition - Origin.position) * Matrix4x4.Rotate(Quaternion.LookRotation(par.moveDir)) * data.TRS;
            Bounds bounds = data.bounds;
            bounds.center = finalMat.MultiplyPoint3x4(bounds.center);
            renderParams.worldBounds = bounds;
        }
    }
}
