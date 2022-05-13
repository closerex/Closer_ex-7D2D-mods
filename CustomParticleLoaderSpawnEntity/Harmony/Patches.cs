using HarmonyLib;

[HarmonyPatch]
public class CustomParticleLoaderSpawnEntityPatches
{
    public static readonly string str_spawn_entity_class = "Explosion.SpawnEntityClass";
    public static readonly string str_spawn_entity_group = "Explosion.SpawnEntityGroup";
    public static readonly string str_spawn_entity_item = "Explosion.SpawnEntityItem";
    public static readonly string str_spawn_entity_loot_group = "Explosion.SpawnLootGroup";
    public static readonly string str_spawn_entity_lifetime = "Explosion.SpawnEntityLifetime";
    public static readonly string str_spawn_entity_spawn_chance = "Explosion.SpawnChance";

    [HarmonyPatch(typeof(CustomParticleEffectLoader), nameof(CustomParticleEffectLoader.parseParticleData))]
    [HarmonyPostfix]
    public static void Postfix_parseParticleData_CustomParticleEffectLoader(ref DynamicProperties _props, bool __result)
    {
        if (!__result)
            return;

        var cur_component = CustomParticleEffectLoader.LastInitializedComponent;
        float lifetime = float.MaxValue;
        _props.ParseFloat(str_spawn_entity_lifetime, ref lifetime);
        if (lifetime < float.MaxValue)
            cur_component.AddCustomProperty(str_spawn_entity_lifetime, lifetime);

        float chance = 1f;
        _props.ParseFloat(str_spawn_entity_spawn_chance, ref chance);
        if (chance < 1f)
            cur_component.AddCustomProperty(str_spawn_entity_spawn_chance, chance);

        string entity = null;
        _props.ParseString(str_spawn_entity_group, ref entity);
        if(!string.IsNullOrEmpty(entity))
        {
            cur_component.AddCustomProperty(str_spawn_entity_group, entity);
            Log.Out("Adding entity group to spawn: " + entity);
            return;
        }

        entity = null;
        _props.ParseString(str_spawn_entity_class, ref entity);
        if(!string.IsNullOrEmpty(entity))
        {
            cur_component.AddCustomProperty(str_spawn_entity_class, entity);
            Log.Out("Adding entity class to spawn: " + entity);
            return;
        }

        entity = null;
        _props.ParseString(str_spawn_entity_item, ref entity);
        if(!string.IsNullOrEmpty(entity))
        {
            cur_component.AddCustomProperty(str_spawn_entity_item, entity);
            Log.Out("Adding entity item to spawn: " + entity);
            return;
        }

        entity = null;
        _props.ParseString(str_spawn_entity_loot_group, ref entity);
        if(!string.IsNullOrEmpty(entity))
        {
            cur_component.AddCustomProperty(str_spawn_entity_loot_group, entity);
            Log.Out("Adding loot group to spawn: " + entity);
            return;
        }
    }

    /*
    [HarmonyPatch(typeof(Explosion), nameof(Explosion.AttackEntites))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> AttackEntites_Explosion_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        MethodInfo mtd_ctsw = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.ConditionalTriggerSleeperWakeUp));
        MethodInfo mtd_isdead = AccessTools.Method(typeof(Entity), nameof(Entity.IsDead));

        for (int i = 0, totali = codes.Count; i < totali; i++)
        {
            if(codes[i].opcode == OpCodes.Ldloc_S && codes[i].OperandIs(14) && codes[i + 1].opcode == OpCodes.Callvirt && codes[i + 1].Calls(mtd_isdead))
            {
                var operand = codes[i + 2].operand;
                codes.InsertRange(i, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc, 14),
                    CodeInstruction.Call(typeof(ExplosionSpawnEntity), nameof(ExplosionSpawnEntity.ShouldExplosionIgnoreEntity)),
                    new CodeInstruction(OpCodes.Brtrue, operand)
                });
            }else if (codes[i].opcode == OpCodes.Callvirt && codes[i].Calls(mtd_ctsw))
            {
                var operand = codes[i - 2].operand;
                codes.InsertRange(i - 1, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc, 11),
                    CodeInstruction.Call(typeof(ExplosionSpawnEntity), nameof(ExplosionSpawnEntity.ShouldExplosionIgnoreEntity)),
                    new CodeInstruction(OpCodes.Brtrue, operand)
                });
                break;
            }
        }

        return codes;
    }
    */

    /*
    [HarmonyPatch(typeof(World), nameof(World.SpawnEntityInWorld))]
    [HarmonyPrefix]
    private static bool SpawnEntityInWorld_World_Prefix(Entity _entity, World __instance, out bool __state)
    {
        __state = false;
        if (_entity && _entity is EntityItem && __instance != null && !__instance.IsRemote())
            __state = _entity.bDead = true;

        return true;
    }

    [HarmonyPatch(typeof(World), nameof(World.SpawnEntityInWorld))]
    [HarmonyPostfix]
    private static void SpawnEntityInWorld_World_Postfix(Entity _entity, bool __state)
    {
        if (__state)
            _entity.bDead = false;
    }
    */
}
