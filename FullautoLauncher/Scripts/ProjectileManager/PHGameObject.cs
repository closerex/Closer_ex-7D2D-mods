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
        public Transform Transform { get; }
        public PHGameObject(Transform transform, ProjectileParams par) : base(par)
        {
            Transform = transform;
        }

        public override void Fire()
        {
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
            GameObject.Destroy(Transform.gameObject);
        }
    }
}
