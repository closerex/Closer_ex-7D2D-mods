using System;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionSpawnEntity : MonoBehaviour
{
    public Entity entity = null;
    public bool canSpawn = false;
    void Awake()
    {
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
        {
            enabled = false;
            return;
        }

        CustomParticleComponents component = CustomParticleEffectLoader.LastInitializedComponent;
        EntityCreationData data = new EntityCreationData();
        int entityId = component.CurrentExplosionParams._playerId;
        Entity initiator = GameManager.Instance.World.GetEntity(entityId);
        if (initiator && !initiator.IsDead())
        {
            if (initiator.belongsPlayerId != -1)
                data.belongsPlayerId = initiator.belongsPlayerId;
            else
                data.belongsPlayerId = initiator.entityId;
        }else
        {
            data.belongsPlayerId = -1;
            entityId = -1;
        }
        Vector3 position = component.CurrentExplosionParams._worldPos;
        Vector3 rotation = transform.forward;
        int classId = 0;
        if (component.TryGetCustomProperty(CustomParticleLoaderSpawnEntityPatches.str_spawn_entity_lifetime, out object lifetime))
            data.lifetime = (float)lifetime;
        else
            data.lifetime = float.MaxValue;

        if (component.TryGetCustomProperty(CustomParticleLoaderSpawnEntityPatches.str_spawn_entity_group, out object entityGroup))
            classId = EntityGroups.GetRandomFromGroup(entityGroup as string, ref classId);
        else if (component.TryGetCustomProperty(CustomParticleLoaderSpawnEntityPatches.str_spawn_entity_class, out object entityClass))
            classId = EntityClass.FromString(entityClass as string);
        else if (component.TryGetCustomProperty(CustomParticleLoaderSpawnEntityPatches.str_spawn_entity_item, out object entityItem))
        {
            classId = EntityClass.itemClass;
            string itemName = entityItem as string;
            string[] itemStat = itemName.Split('$');
            int spawnCount = 1;
            if (itemStat.Length == 2)
            {
                itemName = itemStat[0];
                string[] itemCount = itemStat[1].Split(',');
                if (itemCount.Length == 2)
                    spawnCount = UnityEngine.Random.Range(int.Parse(itemCount[0]), int.Parse(itemCount[1]) + 1);
                else
                    spawnCount = int.Parse(itemCount[0]);

                if (spawnCount < 1)
                    spawnCount = 1;
            }
            ItemClass itemClass = ItemClass.GetItemClass(entityItem as string);
            if(itemClass == null)
            {
                Log.Error("ExplosionSpawnEntity: item class with name " + entityItem as string + "not found!");
                return;
            }
            if (spawnCount > itemClass.Stacknumber.Value)
                spawnCount = itemClass.Stacknumber.Value;
            ItemStack stack = new ItemStack(ItemClass.GetItem(entityItem as string), spawnCount);
            if (stack != null)
                data.itemStack = stack;
            else
                return;
                //GameManager.Instance.ItemDropServer(stack, position, Vector3.zero, entityId, data.lifetime < float.MaxValue ? data.lifetime : 60f);
            //return;
        }else if(component.TryGetCustomProperty(CustomParticleLoaderSpawnEntityPatches.str_spawn_entity_loot_group, out object lootGroup))
        {
            if(LootContainer.lootGroups.TryGetValue(lootGroup as string, out var entry))
            {
                EntityPlayer player = null;
                if (initiator)
                {
                    if (initiator is EntityPlayer)
                        player = initiator as EntityPlayer;
                    else
                        player = GameManager.Instance.World.GetClosestPlayer(initiator, -1, false);
                } else
                    player = GameManager.Instance.World.Players.Count > 0 ? GameManager.Instance.World.GetPlayers()[0] : null;

                if(player == null)
                {
                    Log.Error("ExplosionSpawnEntity: no valid player around, abort spawning loot group!");
                    return;
                }
                float gameStage = player.GetHighestPartyLootStage(0, 0);

                classId = EntityClass.itemClass;
                ItemStack stack = SpawnOneLootItemsFromList(entry.items, gameStage, entry.lootQualityTemplate, player);
                if (stack != null)
                    data.itemStack = stack;
                else
                    return;
                    //GameManager.Instance.ItemDropServer(stack, position, Vector3.zero, entityId, data.lifetime < float.MaxValue ? data.lifetime : 60f);
                //return;
            }else
            {
                Log.Error("ExplosionSpawnEntity: no loot group with name " + lootGroup as string + " found!");
                return;
            }
        }else
        {
            Log.Error("ExplosionSpawnEntity: Entity to spawn is not defined!");
            return;
        }

        data.entityClass = classId;
        data.id = -1;
        data.pos = position;
        data.rot = rotation;
        data.spawnById = -1;
        data.spawnByName = string.Empty;

        //GameManager.Instance.RequestToSpawnEntityServer(data);

        
        entity = EntityFactory.CreateEntity(data);
        if(entity)
        {
            canSpawn = true;
            if(entity is EntityPlayer)
            {
                Log.Error("ExplosionSpawnEntity: WHY THE HELL ARE YOU SPAWNING A PLAYER?");
                canSpawn = false;
            }else if(entity is EntityTurret || entity is EntityDrone)
            {
                Log.Error("ExplosionSpawnEntity: can not spawn turret or drone because they need an original item value");
                canSpawn = false;
            }else if(entity is EntityItem)
            {
                entity.isPhysicsMaster = true;
            }else if(entity is EntityVehicle)
            {
                Log.Error("ExplosionSpawnEntity: WHY THE HELL ARE YOU SPAWNING A VEHICLE?");
                canSpawn = false;
            }
            if (canSpawn)
            {
                //Log.Out("can spawn entity!");
                Chunk chunk = (Chunk)GameManager.Instance.World.GetChunkSync(World.toChunkXZ((int)entity.position.x), World.toChunkXZ((int)entity.position.z));
                entity.SetSpawnerSource(EnumSpawnerSource.Unknown, chunk.Key, entityGroup as string);
                GameManager.Instance.World.SpawnEntityInWorld(entity);
                //Log.Out("entity added to world!");
                if (entity is EntityItem)
                {
                    if (chunk != null)
                    {
                        List<EntityItem> list = new List<EntityItem>();
                        for (int i = 0; i < chunk.entityLists.Length; i++)
                        {
                            if (chunk.entityLists[i] != null)
                            {
                                for (int j = 0; j < chunk.entityLists[i].Count; j++)
                                {
                                    if (chunk.entityLists[i][j] is EntityItem)
                                    {
                                        list.Add(chunk.entityLists[i][j] as EntityItem);
                                    }
                                }
                            }
                        }
                        int exceed = list.Count - 50;
                        if (exceed > 0)
                        {
                            list.Sort(new GameManager.EntityItemLifetimeComparer());
                            int lastIndex = list.Count - 1;
                            while (lastIndex >= 0 && exceed > 0)
                            {
                                list[lastIndex].MarkToUnload();
                                exceed--;
                                lastIndex--;
                            }
                        }
                    }
                }
            }else
                entity.OnEntityUnload();
        }
    }

    public static float getProbability(LootContainer.LootEntry _item, float gamestage)
    {
        if (_item.lootProbTemplate != string.Empty && LootContainer.lootProbTemplates.ContainsKey(_item.lootProbTemplate))
        {
            LootContainer.LootProbabilityTemplate lootProbabilityTemplate = LootContainer.lootProbTemplates[_item.lootProbTemplate];
            for (int i = 0; i < lootProbabilityTemplate.templates.Count; i++)
            {
                LootContainer.LootEntry lootEntry = lootProbabilityTemplate.templates[i];
                if (lootEntry.minLevel <= gamestage && lootEntry.maxLevel >= gamestage)
                {
                    return lootEntry.prob;
                }
            }
        }
        return _item.prob;
    }

    public static ItemStack SpawnOneLootItemsFromList(List<LootContainer.LootEntry> itemSet, float gameStage, string lootQualityTemplate, EntityPlayer player)
    {
        float totalProb = 0f;
        for (int i = 0; i < itemSet.Count; i++)
        {
            LootContainer.LootEntry lootEntry = itemSet[i];
            if (!lootEntry.forceProb)
            {
                totalProb += getProbability(lootEntry, gameStage);
            }
        }
        if (totalProb == 0f)
        {
            return null;
        }
        float thresProb = 0f;
        float randomFloat = UnityEngine.Random.Range(0f, 1f);
        ItemStack stack = null;
        for (int k = 0; k < itemSet.Count; k++)
        {
            LootContainer.LootEntry lootEntry2 = itemSet[k];
            bool flag;
            float probability = getProbability(lootEntry2, gameStage);
            if (!lootEntry2.forceProb)
            {
                thresProb += probability / totalProb;
                flag = (randomFloat <= thresProb);
            }else
                flag = (UnityEngine.Random.Range(0f, 1f) <= probability);

            if (flag)
            {
                if (lootEntry2.group == null)
                {
                    int count = UnityEngine.Random.Range(lootEntry2.minCount, lootEntry2.maxCount + 1);
                    count += Mathf.RoundToInt((float)count * (lootEntry2.lootstageCountMod * gameStage));
                    stack = SpawnItem(lootEntry2, lootEntry2.item.itemValue, count, gameStage, lootQualityTemplate, player);
                    break;
                }
                if (lootEntry2.group.minLevel <= gameStage && lootEntry2.group.maxLevel >= gameStage)
                {
                    stack = SpawnOneLootItemsFromList(lootEntry2.group.items, gameStage, lootQualityTemplate, player);
                    break;
                }
                break;
            }
        }
        return stack;
    }

    public static ItemStack SpawnItem(LootContainer.LootEntry template, ItemValue lootItemValue, int countToSpawn, float gameStage, string lootQualityTemplate, EntityPlayer player)
    {
        if (lootItemValue.ItemClass == null)
        {
            return null;
        }
        if (player != null)
        {
            countToSpawn = Math.Min((int)EffectManager.GetValue(PassiveEffects.LootQuantity, player.inventory.holdingItemItemValue, (float)countToSpawn, player, null, lootItemValue.ItemClass.ItemTags, true, true, true, true, 1, true), lootItemValue.ItemClass.Stacknumber.Value);
        }
        if (countToSpawn < 1)
        {
            return null;
        }
        int minQuality = template.minQuality;
        int maxQuality = template.maxQuality;
        string qualityTemp = lootQualityTemplate;
        if (!string.IsNullOrEmpty(qualityTemp))
        {
            LootContainer.LootGroup parentGroup = template.parentGroup;
            if (((parentGroup != null) ? parentGroup.lootQualityTemplate : null) != null)
                qualityTemp = template.parentGroup.lootQualityTemplate;
        }
        if (!string.IsNullOrEmpty(qualityTemp))
        {
            bool flag = false;
            for (int j = 0; j < LootContainer.lootQualityTemplates[qualityTemp].templates.Count; j++)
            {
                float randomFloat = UnityEngine.Random.Range(0f, 1f);
                LootContainer.LootGroup lootGroup = LootContainer.lootQualityTemplates[qualityTemp].templates[j];
                minQuality = lootGroup.minQuality;
                maxQuality = lootGroup.maxQuality;
                if (lootGroup.minLevel <= gameStage && lootGroup.maxLevel >= gameStage)
                {
                    for (int k = 0; k < lootGroup.items.Count; k++)
                    {
                        LootContainer.LootEntry lootEntry = lootGroup.items[k];
                        if (randomFloat <= lootEntry.prob)
                        {
                            minQuality = lootEntry.minQuality;
                            maxQuality = lootEntry.maxQuality;
                            flag = true;
                            break;
                        }
                    }
                    if (flag)
                    {
                        break;
                    }
                }
            }
        }
        ItemValue itemValue;
        if (lootItemValue.HasQuality)
        {
            if (minQuality <= -1)
            {
                minQuality = 1;
                maxQuality = 6;
            }
            if (template.parentGroup != null && template.parentGroup.modsToInstall.Length != 0)
            {
                itemValue = new ItemValue(lootItemValue.type, minQuality, maxQuality, true, template.parentGroup.modsToInstall, template.parentGroup.modChance);
            }else
            {
                itemValue = new ItemValue(lootItemValue.type, minQuality, maxQuality, true, template.modsToInstall, template.modChance);
            }
        }else
        {
            itemValue = new ItemValue(lootItemValue.type, true);
        }

        if (itemValue.ItemClass != null)
        {
            if (itemValue.ItemClass.Actions != null && itemValue.ItemClass.Actions.Length != 0 && itemValue.ItemClass.Actions[0] != null)
            {
                itemValue.Meta = 0;
            }
            if (itemValue.MaxUseTimes > 0)
            {
                itemValue.UseTimes = (float)((int)((float)itemValue.MaxUseTimes * UnityEngine.Random.Range(0.2f, 0.8f)));
            }
            int stackNumber = itemValue.ItemClass.Stacknumber.Value;
            if (stackNumber <= 1)
                countToSpawn = 1;
            else if (countToSpawn > stackNumber)
                countToSpawn = stackNumber;
        }
        ItemStack stack = new ItemStack(itemValue, countToSpawn);
        return stack;
    }
}

