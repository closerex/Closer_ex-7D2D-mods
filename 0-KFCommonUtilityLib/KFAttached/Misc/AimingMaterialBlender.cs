using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public class AimingMaterialBlender : MonoBehaviour
    {
        public AimReference aimRef;
        public string materialPropertyName = "_MatBlendingScalar";
#if NotEditor
        private Material material;
        private float recordedValue = 0f;
        private float Value
        {
            set
            {
                if (recordedValue != value)
                {
                    material.SetFloat(materialPropertyName, value);
                }
                recordedValue = value;
            }
        }
        public void Awake()
        {
            if (TryGetComponent<Renderer>(out var renderer) && aimRef)
            {
                material = renderer.material;
                material.SetFloat(materialPropertyName, 0f);
                recordedValue = 0f;
            }
            else
            {
                Destroy(this);
                return;
            }
        }

        public void OnEnable()
        {
            material.SetFloat(materialPropertyName, 0f);
            recordedValue = 0f;
        }

        public void LateUpdate()
        {
            var data = aimRef.group.data;
            if (data != null)
            {
                if (aimRef.index == data.curAimRefIndex)
                {
                    Value = data.aimProcValue;
                }
                else
                {
                    Value = 0f;
                }
            }
        }
#endif
    }
}
