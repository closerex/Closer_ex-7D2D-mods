using System.Collections.Generic;
using UnityEngine;

namespace KFCommonUtilityLib.KFAttached.Render
{
    [AddComponentMenu("")]
    internal class MagnifyScopeTargetRef : MonoBehaviour
    {
        private HashSet<MagnifyScope> targets = new HashSet<MagnifyScope>();

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            foreach (var target in targets)
            {
                target.RenderImageCallback(source, destination);
            }
            Graphics.Blit(source, destination);
        }

        internal void AddTarget(MagnifyScope target)
        {
            if (targets.Count == 0)
            {
                enabled = true;
            }
            targets.Add(target);
        }

        internal void RemoveTarget(MagnifyScope target)
        {
            targets.Remove(target);
            if(targets.Count == 0 )
            {
                enabled = false;
            }
        }
    }
}
