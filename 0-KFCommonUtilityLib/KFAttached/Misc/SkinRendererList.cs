#if NotEditor
using UniLinq;
#endif
#if UNITY_EDITOR
using System.Collections.Generic;
#endif
using UnityEngine;

namespace KFCommonUtilityLib
{
    public class SkinRendererList : MonoBehaviour
#if UNITY_EDITOR
        , ISerializationCallbackReceiver
#endif
    {
        public Renderer[] holdingItemRenderers;
        public Renderer[] dropMeshRenderers;
        [HideInInspector]
        public int[] holdingItemMaterialCount;
        [HideInInspector]
        public int[] dropMeshMaterialCount;

#if UNITY_EDITOR
        public List<Material> holdingItemMaterialsReference;
        public List<Material> dropMeshMaterialsReference;

        public void OnAfterDeserialize()
        {
        }

        public void OnBeforeSerialize()
        {
            if (holdingItemMaterialsReference == null)
            {
                holdingItemMaterialsReference = new List<Material>();
            }
            else
            {
                holdingItemMaterialsReference.Clear();
            }
            if (holdingItemRenderers != null && holdingItemRenderers.Length > 0)
            {
                holdingItemMaterialCount = new int[holdingItemRenderers.Length];
                for (int i = 0; i < holdingItemMaterialCount.Length; i++)
                {
                    holdingItemMaterialCount[i] = holdingItemRenderers[i] ? holdingItemRenderers[i].sharedMaterials.Length : 0;
                    if (holdingItemMaterialCount[i] > 0)
                    {
                        holdingItemMaterialsReference.AddRange(holdingItemRenderers[i].sharedMaterials);
                    }
                }
            }

            if (dropMeshMaterialsReference == null)
            {
                dropMeshMaterialsReference = new List<Material>();
            }
            else
            {
                dropMeshMaterialsReference.Clear();
            }
            if (dropMeshRenderers != null && dropMeshRenderers.Length > 0)
            {
                dropMeshMaterialCount = new int[dropMeshRenderers.Length];
                for (int i = 0; i < dropMeshMaterialCount.Length; i++)
                {
                    dropMeshMaterialCount[i] = dropMeshRenderers[i] ? dropMeshRenderers[i].sharedMaterials.Length : 0;
                    if (dropMeshMaterialCount[i] > 0)
                    {
                        dropMeshMaterialsReference.AddRange(dropMeshRenderers[i].sharedMaterials);
                    }
                }
            }
        }
#endif

#if NotEditor
        public void ApplySkinMaterials(SkinMaterialReplacer replacer, bool forDropMesh)
        {
            if (!replacer)
            {
                Log.Error("SkinMaterialReplacer invalid!");
                return;
            }
            var renderers = forDropMesh ? dropMeshRenderers : holdingItemRenderers;
            var materialCount = forDropMesh ? dropMeshMaterialCount : holdingItemMaterialCount;

            if (renderers is not { Length: not 0 } || materialCount is not { Length: not 0 })
            {
                return;
            }

            var materials = forDropMesh ? replacer.dropMeshMaterials : replacer.holdingItemMaterials;
            var totalMaterialCount = materialCount.Sum();
            if (materials.Length != totalMaterialCount)
            {
                Log.Warning($"SkinMaterialReplacer material count does not match: expected {totalMaterialCount} actual {materials.Length}");
                return;
            }

            var materialOffset = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                var replacedMaterials = renderers[i].materials;
                for (var j = 0; j < materialCount[i]; j++)
                {
                    var materialIndex = materialOffset + j;
                    if (materials[materialIndex])
                    {
                        replacedMaterials[j] = materials[materialIndex];
                    }
                }
                renderers[i].materials = replacedMaterials;
                materialOffset += materialCount[i];
            }
        }

        public void ApplyTintColor(Vector3 colorVec, bool forDropMesh)
        {
            var renderers = forDropMesh ? dropMeshRenderers : holdingItemRenderers;
            
            if (renderers is not { Length: not 0 })
            {
                return;
            }
            
            var color = new Color(colorVec.x, colorVec.y, colorVec.z, 1);
            foreach (var renderer in renderers)
            {
                if (!renderer)
                    continue;
                
                UpdateLight.SetTintColor(renderer, color);
            }
        }

        public Material GetMaterial(int index, bool forDropMesh)
        {
            var renderers = forDropMesh ? dropMeshRenderers : holdingItemRenderers;
            var materialCount = forDropMesh ? dropMeshMaterialCount : holdingItemMaterialCount;
            
            var materialOffset = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (materialCount[i] + materialOffset <= index)
                {
                    materialOffset += materialCount[i];
                    continue;
                }

                var matIndex = index - materialOffset;
                return renderers[i].materials[matIndex];
            }
            return null;
        }
#endif
    }
}
