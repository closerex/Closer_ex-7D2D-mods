using System.Collections.Generic;
using UnityEngine;

internal class ExplosionAreaBuffTick : ExplosionDamageArea
{
    private HashSet<EntityAlive> hash_entities = new HashSet<EntityAlive>();
    private EntityPlayer player = null;
    private ItemValue item_value = null;

    //ExplosionParams cur_params;
    //called immediately after particle initialized
    private new void Awake()
    {
        //this is a important condition for a script that deals with area effect!
        //make sure it's only executed on server side, so that you don't need to worry about state sync
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
        {
            enabled = false;
        }
        else
        {
            //CurrentExplosionParams stores all params that GameManager.explode() contains, except ItemValue because I'm storing it separately as CurrentItemValue
            //cur_params = CustomParticleEffectLoader.LastInitializedComponent.CurrentExplosionParams;
            //Log.Out("params:" + cur_params._clrIdx + cur_params._blockPos + cur_params._playerId + cur_params._rotation + cur_params._worldPos + cur_params._explosionData.ParticleIndex);
            //make sure you clone the ItemValue if you need it
            item_value = CustomExplosionManager.LastInitializedComponent.CurrentItemValue.Clone();
            //I'm not sure when Position and StartPosition is needed but filling more fields won't harm
            player = GameManager.Instance.World.GetEntity(InitiatorEntityId) as EntityPlayer;
            var value = CustomExplosionManager.LastInitializedComponent;
            value.Component.TryGetCustomProperty(ExplosionAreaBuffTickParser.name, out var interval);
            int repeatTimes = (int)(value.CurrentExplosionParams._explosionData.Duration / (float)interval);
            gameObject.AddComponent<Timer>().start((float)interval, repeatTimes, onTimerTick, null);
        }
    }

    private void onTimerTick(Timer timer)
    {
        //Log.Out("Timer tick, buff count: " + this.BuffActions.Count.ToString() + ", entity count: " + this.list_entities.Count.ToString());
        foreach (EntityAlive entityAlive in hash_entities)
        {
            if (!entityAlive || !entityAlive.IsAlive())
            {
                //list_entities.Remove(entityAlive);	//don't do this; it will interrupt iteration and throw error
                continue;
            }
            if (BuffActions != null)
            {
                for (int i = 0; i < this.BuffActions.Count; i++)
                {
                    entityAlive.Buffs.AddBuff(this.BuffActions[i], InitiatorEntityId, true, false, -1);
                }
            }
            if (player != null && entityAlive.entityId != InitiatorEntityId)
            {
                MinEventParams data = player.MinEventContext;
                data.Other = entityAlive;
                data.ItemValue = item_value;
                data.Position = entityAlive.GetPosition();
                //Log.Out("Fire attack event:" + data.ItemValue.ItemClass.GetItemName() + " on " + data.Other.EntityName);
                //this fires event on ItemClass
                item_value.FireEvent(MinEventTypes.onSelfAttackedOther, data);
                //do not use inventory since holding item may have changed
                player.FireEvent(MinEventTypes.onSelfAttackedOther, false);
            }
        }
    }

    //copied from vanilla; if you dont know how to get a entity in range, copy the same code.
    private new Entity getEntityFromCollider(Collider col)
    {
        Transform transform = col.transform;
        if (!transform.tag.StartsWith("E_"))
        {
            return null;
        }
        Transform transform2 = null;
        if (transform.tag.StartsWith("E_BP_"))
        {
            transform2 = GameUtils.GetHitRootTransform(transform.tag, transform);
        }
        EntityAlive entityAlive = (transform2 != null) ? transform2.GetComponent<EntityAlive>() : null;
        if (entityAlive == null || entityAlive.IsDead())
        {
            return null;
        }
        return entityAlive;
    }

    //you need a collider that works as a trigger to receive these two messages
    private new void OnTriggerEnter(Collider other)
    {
        if (!enabled)
            return;
        //Log.Out("Explosive Area trigger entered!");
        EntityAlive entityAlive = this.getEntityFromCollider(other) as EntityAlive;
        if (entityAlive == null)
        {
            return;
        }
        if (!hash_entities.Contains(entityAlive))
            hash_entities.Add(entityAlive);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enabled)
            return;
        //Log.Out("Explosive Area trigger exited!");
        EntityAlive entityAlive = this.getEntityFromCollider(other) as EntityAlive;
        if (entityAlive == null)
            return;
        hash_entities.Remove(entityAlive);
    }
}