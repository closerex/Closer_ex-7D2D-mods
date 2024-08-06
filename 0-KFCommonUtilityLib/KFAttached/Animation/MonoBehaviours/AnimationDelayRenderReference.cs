using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[AddComponentMenu("")]
internal class AnimationDelayRenderReference : MonoBehaviour
{
#if NotEditor
    internal HashSet<AnimationDelayRender> targets = new HashSet<AnimationDelayRender>();
    private void OnPreCull()
    {
        if (targets.Count > 0)
        {
            foreach (var target in targets)
            {
                target.PreCullCallback();
            }
        }
    }
#endif
}
