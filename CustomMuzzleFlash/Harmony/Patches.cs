using HarmonyLib;

[HarmonyPatch]
class MuzzlePatch
{
    [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.ReadFrom))]
    [HarmonyPostfix]
    private static void Postfix_ReadFrom_ItemActionAttack(DynamicProperties _props)
    {
        LoadPEAsset(_props, "Particles_muzzle_fire");
        LoadPEAsset(_props, "Particles_muzzle_fire_fpv");
        LoadPEAsset(_props, "Particles_muzzle_smoke");
        LoadPEAsset(_props, "Particles_muzzle_smoke_fpv");
    }

    [HarmonyPatch(typeof(AutoTurretFireController), nameof(AutoTurretFireController.Init))]
    [HarmonyPostfix]
    private static void Postfix_Init_AutoTurretFireController(DynamicProperties _properties)
    {
        LoadPEAsset(_properties, "ParticlesMuzzleFire");
        LoadPEAsset(_properties, "ParticlesMuzzleSmoke");
	}

    private static void LoadPEAsset(DynamicProperties _props, string _key)
    {
        if (_props.Values.TryGetValue(_key, out string val) && !string.IsNullOrEmpty(val) && !ParticleEffect.IsAvailable(val))
        {
            ParticleEffect.LoadAsset(val);
        }
    }
}