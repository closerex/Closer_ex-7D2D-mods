using System;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public class AimingMaterialBlender : MonoBehaviour
    {
        public AimReference aimRef;
        public string materialPropertyName = "_MatBlendingScalar";
        public string depthPropertyName = "";
        public bool isColorAlpha = false;
        [Range(0, 1)]
        public float minThres = 0f;
        [Range(0, 1)]
        public float opaqueThres = 0.1f;
#if NotEditor
        private Material material;
        private int materialPropertyID = -1;
        private float recordedValue = -1f;
        private float originalDepth = -1f;
        private float Value
        {
            set
            {
                if (recordedValue != value && material.HasProperty(materialPropertyID))
                {
                    if (isColorAlpha)
                    {
                        var color = material.GetColor(materialPropertyID);
                        color.a = Mathf.Lerp(1, minThres, value);
                        material.SetColor(materialPropertyID, color);
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
                        material.SetFloat(materialPropertyID, Mathf.Lerp(minThres, 1, value));
                    }
                }
                recordedValue = value;
            }
        }

        public void Awake()
        {
            materialPropertyID = Shader.PropertyToID(materialPropertyName);
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

        public void OnDisable()
        {
            if (material)
            {
                if (!string.IsNullOrEmpty(depthPropertyName) && originalDepth >= 0 && material.HasProperty(depthPropertyName))
                {
                    material.SetFloat(depthPropertyName, originalDepth);
                }
                originalDepth = -1;
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
                if (aimRef.designedAimDistance > 0 && !string.IsNullOrEmpty(depthPropertyName) && originalDepth < 0 && material.HasProperty(depthPropertyName))
                {
                    originalDepth = material.GetFloat(depthPropertyName);
                    material.SetFloat(depthPropertyName, originalDepth * (data.targetSwitchBlender[aimRef.index].targetAimRefOffset + aimRef.designedAimDistance) / aimRef.designedAimDistance);
                }
            }
            else
            {
                Value = 0f;
                originalDepth = -1;
            }
        }
#endif
    }
}
