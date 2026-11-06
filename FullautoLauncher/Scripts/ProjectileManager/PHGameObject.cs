using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FullautoLauncher.Scripts.ProjectileManager
{
    public class PHGameObject : ParameterHolderAbs, IDisposable
    {
        private AssetManagement.InstanceHandle<GameObject> modelHandle;
        private bool disposed;

        public Transform Transform { get; }

        public PHGameObject(Transform transform, ProjectileParams par)
            : this(new AssetManagement.InstanceHandle<GameObject>(transform.gameObject), par)
        {
        }

        public PHGameObject(AssetManagement.InstanceHandle<GameObject> modelHandle, ProjectileParams par) : base(par)
        {
            this.modelHandle = modelHandle;
            Transform = modelHandle.Instance.transform;
        }

        public override void Fire()
        {
            Transform.parent = null;
            Transform.gameObject.SetActive(true);
            UpdatePosition();
        }

        public override void UpdatePosition()
        {
            Vector3 realPos = par.renderPosition - Origin.position;
            Transform.position = realPos;
            Transform.LookAt(realPos + par.moveDir);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            var handle = modelHandle;
            modelHandle = default;
            // Destroy also releases the asset lease; never call Release again.
            handle.Destroy();
        }
    }
}
