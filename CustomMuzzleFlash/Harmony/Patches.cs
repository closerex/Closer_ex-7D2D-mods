using HarmonyLib;

[HarmonyPatch]
class MuzzlePatch
{
    [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.ReadFrom))]
    [HarmonyPostfix]
    private static void Postfix_ReadFrom_ItemActionAttack(DynamicProperties _props)
    {
		if (_props.Values.ContainsKey("Particles_muzzle_fire") && !ParticleEffect.IsAvailable(_props.Values["Particles_muzzle_fire"]))
		{
			ParticleEffect.LoadAsset(_props.Values["Particles_muzzle_fire"]);
		}
		if (_props.Values.ContainsKey("Particles_muzzle_fire_fpv") && !ParticleEffect.IsAvailable(_props.Values["Particles_muzzle_fire_fpv"]))
		{
			ParticleEffect.LoadAsset(_props.Values["Particles_muzzle_fire_fpv"]);
		}
		if (_props.Values.ContainsKey("Particles_muzzle_smoke") && !ParticleEffect.IsAvailable(_props.Values["Particles_muzzle_smoke"]))
		{
			ParticleEffect.LoadAsset(_props.Values["Particles_muzzle_smoke"]);
		}
		if (_props.Values.ContainsKey("Particles_muzzle_smoke_fpv") && !ParticleEffect.IsAvailable(_props.Values["Particles_muzzle_smoke_fpv"]))
		{
			ParticleEffect.LoadAsset(_props.Values["Particles_muzzle_smoke_fpv"]);
		}
	}
}