using System;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public class AimingMaterialBlender : MonoBehaviour
    {
        public AimReference aimRef;
        public string materialPropertyName = "_MatBlendingScalar";
        public bool isColorAlpha = false;
        [Range(0, 1)]
        public float minThres = 0f;
        [Range(0, 1)]
        public float opaqueThres = 0.1f;
#if NotEditor
        private Material material;
        private float recordedValue = -1f;
        private float Value
        {
            set
            {
                if (recordedValue != value)
                {
                    if (isColorAlpha)
                    {
                        var color = material.GetColor(materialPropertyName);
                        color.a = Mathf.Lerp(1, minThres, value);
                        material.SetColor(materialPropertyName, color);
                        if (value <= opaqueThres)
                        {
                            material.renderQueue = 2000;
                        }
                        else
                        {
                            material.renderQueue = 3000;
                        }
                    }
                    else
                    {
                        material.SetFloat(materialPropertyName, Mathf.Lerp(minThres, 1, value));
                    }
                }
                recordedValue = value;
            }
        }

        public void OnEnable()
        {
            if (TryGetComponent<Renderer>(out var renderer))
            {
                material = renderer.material;
                Value = 0f;
            }
            else
            {
                Destroy(this);
                return;
            }
        }

        public void LateUpdate()
        {
            if (!aimRef || !aimRef.group)
            {
                Destroy(this);
                return;
            }
            var data = aimRef.group.data;
            if (data != null && aimRef.index >= 0)
            {
                Value = data.CurAimProcValue * data.targetSwitchBlender.GetTargetWeight(aimRef.index);
            }
            else
            {
                Value = 0f;
            }
        }
#endif
    }
}
