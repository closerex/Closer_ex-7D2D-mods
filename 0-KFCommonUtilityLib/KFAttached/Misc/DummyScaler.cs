#if UNITY_EDITOR
using UnityEngine;
#endif

namespace KFCommonUtilityLib
{
    internal class DummyScaler :
#if NotEditor
        ScreenSpaceParticleAspectScaler
#elif UNITY_EDITOR
        MonoBehaviour
#endif
    {
        private new void Update()
        {

        }
    }
}
