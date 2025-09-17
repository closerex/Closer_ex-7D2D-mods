using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;

[TypeTarget(typeof(ItemActionAttack)), TypeDataTarget(typeof(DynamicMuzzleFlashData))]
public class ActionModuleDynamicMuzzleFlash
{
    private struct State
    {
        public bool executed;
        public string particlesMuzzleFire;
        public string particlesMuzzleSmoke;
        public string particlesMuzzleFireFpv;
        public string particlesMuzzleSmokeFpv;
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    private void Postfix_OnModificationsChanged(ItemActionAttack __instance, ItemActionAttackData _data, DynamicMuzzleFlashData __customData)
    {
        __customData.particlesMuzzleFire = _data.invData.itemValue.GetPropertyOverrideForAction("Particles_muzzle_fire", __instance.particlesMuzzleFire, __instance.ActionIndex);
        __customData.particlesMuzzleFireFpv = _data.invData.itemValue.GetPropertyOverrideForAction("Particles_muzzle_fire_fpv", __instance.particlesMuzzleFireFpv, __instance.ActionIndex);
        __customData.particlesMuzzleSmoke = _data.invData.itemValue.GetPropertyOverrideForAction("Particles_muzzle_smoke", __instance.particlesMuzzleSmoke, __instance.ActionIndex);
        __customData.particlesMuzzleSmokeFpv = _data.invData.itemValue.GetPropertyOverrideForAction("Particles_muzzle_smoke_fpv", __instance.particlesMuzzleSmokeFpv, __instance.ActionIndex);
        if (!string.IsNullOrEmpty(__customData.particlesMuzzleFire) && !ParticleEffect.IsAvailable(__customData.particlesMuzzleFire))
        {
            ParticleEffect.LoadAsset(__customData.particlesMuzzleFire);
        }
        if (!string.IsNullOrEmpty(__customData.particlesMuzzleFireFpv) && !ParticleEffect.IsAvailable(__customData.particlesMuzzleFireFpv))
        {
            ParticleEffect.LoadAsset(__customData.particlesMuzzleFireFpv);
        }
        if (!string.IsNullOrEmpty(__customData.particlesMuzzleSmoke) && !ParticleEffect.IsAvailable(__customData.particlesMuzzleSmoke))
        {
            ParticleEffect.LoadAsset(__customData.particlesMuzzleSmoke);
        }
        if (!string.IsNullOrEmpty(__customData.particlesMuzzleSmokeFpv) && !ParticleEffect.IsAvailable(__customData.particlesMuzzleSmokeFpv))
        {
            ParticleEffect.LoadAsset(__customData.particlesMuzzleSmokeFpv);
        }
    }

    [HarmonyPatch(nameof(ItemAction.ItemActionEffects)), MethodTargetPrefix]
    private bool Prefix_ItemActionEffects(ItemActionAttack __instance, DynamicMuzzleFlashData __customData, out State __state)
    {
        __state = new State()
        {
            executed = true,
            particlesMuzzleFire = __instance.particlesMuzzleFire,
            particlesMuzzleFireFpv = __instance.particlesMuzzleFireFpv,
            particlesMuzzleSmoke = __instance.particlesMuzzleSmoke,
            particlesMuzzleSmokeFpv = __instance.particlesMuzzleSmokeFpv
        };
        __instance.particlesMuzzleFire = __customData.particlesMuzzleFire;
        __instance.particlesMuzzleFireFpv = __customData .particlesMuzzleFireFpv;
        __instance.particlesMuzzleSmoke = __customData.particlesMuzzleSmoke;
        __instance.particlesMuzzleSmokeFpv = __customData.particlesMuzzleSmokeFpv;
        return true;
    }

    [HarmonyPatch(nameof(ItemAction.ItemActionEffects)), MethodTargetPostfix]
    private void Postfix_ItemActionEffects(ItemActionAttack __instance, State __state)
    {
        if (__state.executed)
        {
            __instance.particlesMuzzleFire = __state.particlesMuzzleFire;
            __instance.particlesMuzzleFireFpv = __state.particlesMuzzleFireFpv;
            __instance.particlesMuzzleSmoke = __state.particlesMuzzleSmoke;
            __instance.particlesMuzzleSmokeFpv = __state.particlesMuzzleSmokeFpv;
        }
    }

    public class DynamicMuzzleFlashData
    {
        public string particlesMuzzleFire;
        public string particlesMuzzleFireFpv;
        public string particlesMuzzleSmoke;
        public string particlesMuzzleSmokeFpv;
    }
}
