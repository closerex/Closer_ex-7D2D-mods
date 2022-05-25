using System;

public class SpawnEntityParser : IExplosionPropertyParser
{
    public static readonly string name = "SpawnEntity";
    public static readonly string str_spawn_entity_class = "Explosion.SpawnEntityClass";
    public static readonly string str_spawn_entity_group = "Explosion.SpawnEntityGroup";
    public static readonly string str_spawn_entity_item = "Explosion.SpawnEntityItem";
    public static readonly string str_spawn_entity_loot_group = "Explosion.SpawnLootGroup";
    public static readonly string str_spawn_entity_lifetime = "Explosion.SpawnEntityLifetime";
    public static readonly string str_spawn_entity_spawn_chance = "Explosion.SpawnChance";
    public Type MatchScriptType()
    {
        return typeof(ExplosionSpawnEntity);
    }

    public string Name()
    {
        return name;
    }

    public bool ParseProperty(DynamicProperties _props, ExplosionComponent component, out object property)
    {
        property = new SpawnEntityProperty();
        var _prop = property as SpawnEntityProperty;

        float lifetime = float.MaxValue;
        _props.ParseFloat(str_spawn_entity_lifetime, ref lifetime);
        _prop.lifetime = lifetime;

        float chance = 1f;
        _props.ParseFloat(str_spawn_entity_spawn_chance, ref chance);
        _prop.chance = chance;

        string entity = null;
        _props.ParseString(str_spawn_entity_group, ref entity);
        if (!string.IsNullOrEmpty(entity))
        {
            _prop.spawn = entity;
            _prop.spawnType = SpawnEntityProperty.SpawnType.EntityGroup;
            Log.Out("Adding entity group to spawn: " + entity);
            return true;
        }

        entity = null;
        _props.ParseString(str_spawn_entity_class, ref entity);
        if (!string.IsNullOrEmpty(entity))
        {
            _prop.spawn = entity;
            _prop.spawnType = SpawnEntityProperty.SpawnType.EntityClass;
            Log.Out("Adding entity class to spawn: " + entity);
            return true;
        }

        entity = null;
        _props.ParseString(str_spawn_entity_item, ref entity);
        if (!string.IsNullOrEmpty(entity))
        {
            _prop.spawn = entity;
            _prop.spawnType = SpawnEntityProperty.SpawnType.EntityItem;
            if (_prop.lifetime == float.MaxValue)
                _prop.lifetime = 60f;
            Log.Out("Adding entity item to spawn: " + entity);
            return true;
        }

        entity = null;
        _props.ParseString(str_spawn_entity_loot_group, ref entity);
        if (!string.IsNullOrEmpty(entity))
        {
            _prop.spawn = entity;
            _prop.spawnType = SpawnEntityProperty.SpawnType.LootGroup;
            Log.Out("Adding loot group to spawn: " + entity);
            return true;
        }
        return true;
    }
}

public class SpawnEntityProperty
{
    public enum SpawnType
    {
        EntityClass,
        EntityGroup,
        EntityItem,
        LootGroup
    }
    public SpawnType spawnType;
    public string spawn;
    public float lifetime;
    public float chance;
}
