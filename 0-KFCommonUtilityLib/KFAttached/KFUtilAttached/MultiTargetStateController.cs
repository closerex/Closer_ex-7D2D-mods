using UnityEngine;

namespace KFCommonUtilityLib
{
    public class MultiTargetStateController : MonoBehaviour, IActiveCountHandler
    {
        public GameObject[] trackedObjs;

        public void OnEnable()
        {
            SetActiveCount(0);
        }

        public void SetActiveCount(int count)
        {
            if (trackedObjs == null)
                return;

            for (int i = 0; i < trackedObjs.Length; i++)
            {
                if (trackedObjs[i])
                {
                    trackedObjs[i].SetActive(i < count);
                }
            }
        }
    }
}
