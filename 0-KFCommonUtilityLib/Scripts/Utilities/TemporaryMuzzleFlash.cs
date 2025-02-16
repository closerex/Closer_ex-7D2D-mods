using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public class TemporaryMuzzleFlash : TemporaryObject
    {
        private void OnDisable()
        {
            StopAllCoroutines();
            if (destroyMaterials)
            {
                Utils.CleanupMaterialsOfRenderers<Renderer[]>(transform.GetComponentsInChildren<Renderer>());
            }
            Destroy(gameObject);
        }
    }
}
