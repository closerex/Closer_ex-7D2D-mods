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
            Renderer[] renderers = forDropMesh ? dropMeshRenderers : holdingItemRenderers;
            int[] materialCount = forDropMesh ? dropMeshMaterialCount : holdingItemMaterialCount;

            if (renderers == null || renderers.Length == 0 || materialCount == null || materialCount.Length == 0)
            {
                return;
            }

            Material[] materials = forDropMesh ? replacer.dropMeshMaterials : replacer.holdingItemMaterials;
            int totalMaterialCount = materialCount.Sum();
            if (materials.Length != totalMaterialCount)
            {
                Log.Warning($"SkinMaterialReplacer material count does not match: expected {totalMaterialCount} actual {materials.Length}");
                return;
            }

            int materialOffset = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] replacedMaterials = renderers[i].materials;
                for (int j = 0; j < materialCount[i]; j++)
                {
                    int materialIndex = materialOffset + j;
                    if (materials[materialIndex])
                    {
                        replacedMaterials[j] = materials[materialIndex];
                    }
                }
                renderers[i].materials = replacedMaterials;
                materialOffset += materialCount[i];
            }
        }
#endif
    }
}
