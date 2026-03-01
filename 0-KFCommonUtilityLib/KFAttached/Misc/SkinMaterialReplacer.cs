using UnityEngine;

namespace KFCommonUtilityLib
{
    [CreateAssetMenu(fileName = "MaterialReplacer", menuName = "KFLibData/SkinMaterialReplacer", order = 100)]
    public class SkinMaterialReplacer : ScriptableObject
    {
        public Material[] holdingItemMaterials;
        public Material[] dropMeshMaterials;
    }
}
