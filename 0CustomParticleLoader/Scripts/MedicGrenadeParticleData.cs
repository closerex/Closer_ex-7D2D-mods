using System;
using System.Collections.Generic;
using UnityEngine;

class MedicGrenadeExplosionDamageArea : ExplosionDamageArea
{
	//called immediately after particle initialized
	private void Awake()
	{
		//this is a important condition for a script that deals with area effect!
		//make sure it's only executed on server side, so that you don't need to worry about state sync
		if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			base.enabled = false;
		}else
        {
			//CurrentExplosionParams stores all params that GameManager.explode() contains, except ItemValue because I'm storing it separately as CurrentItemValue
			cur_params = CustomParticleEffectLoader.LastInitializedComponent.CurrentExplosionParams;
			//Log.Out("params:" + cur_params._clrIdx + cur_params._blockPos + cur_params._playerId + cur_params._rotation + cur_params._worldPos + cur_params._explosionData.ParticleIndex);
			data = new MinEventParams();
			//make sure you clone the ItemValue if you need it
			//and DO NOT store it in MinEventParams
			//because when you set an EntityAlive's MinEventParams object to your MinEventParams,
			//it will be updated every second and set to the holding ItemValue
			item_value = CustomParticleEffectLoader.LastInitializedComponent.CurrentItemValue.Clone();
			//I'm not sure when Position and StartPosition is needed but filling more fields won't harm
			data.Position = cur_params._worldPos;
        }
	}

	private void Start()
    {
		//Log.Out("Explosive Area initialized!");
		//Log.Out("Initiator entity id:" + InitiatorEntityId.ToString());
		player = GameManager.Instance.World.GetEntity(InitiatorEntityId) as EntityPlayer;
		data.Self = player;
		data.IsLocal = player is EntityPlayerLocal;
		gameObject.AddComponent<Timer>().start(1, 15, onTimerTick, null);
    }

	private void onTimerTick(Timer timer)
    {
		if (this.BuffActions != null)
		{
			//Log.Out("Timer tick, buff count: " + this.BuffActions.Count.ToString() + ", entity count: " + this.list_entities.Count.ToString());

			if (player != null)
			{
				data.StartPosition = player.GetPosition();
				data.ItemValue = item_value;
			}
			
			foreach (EntityAlive entityAlive in list_entities)
			{
				if (!entityAlive.IsAlive())
				{
					//list_entities.Remove(entityAlive);	//don't do this; it will interrupt iteration and throw error
					continue;
				}
				for (int i = 0; i < this.BuffActions.Count; i++)
				{
					entityAlive.Buffs.AddBuff(this.BuffActions[i], -1, true, false, false);
					if(player != null && entityAlive.entityId != InitiatorEntityId)
                    {
						data.Other = entityAlive;
						player.MinEventContext = data;
						//Log.Out("Fire attack event:" + data.ItemValue.ItemClass.GetItemName() + " on " + data.Other.EntityName);
						//do not use inventory since holding item may have changed
						player.FireEvent(MinEventTypes.onSelfAttackedOther, false);
                    }
				}
			}
		}
	}

	//copied from vanilla; if you dont know how to get a entity in range, copy the same code.
	private Entity getEntityFromCollider(Collider col)
	{
		Transform transform = col.transform;
		if (!transform.tag.StartsWith("E_") && !transform.CompareTag("Item"))
		{
			return null;
		}
		if (transform.CompareTag("Item"))
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
	private void OnTriggerEnter(Collider other)
	{
		if (!enabled)
			return;
		//Log.Out("Explosive Area trigger entered!");
		EntityAlive entityAlive = this.getEntityFromCollider(other) as EntityAlive;
		if (entityAlive == null)
		{
			return;
		}
		if (!list_entities.Contains(entityAlive))
			list_entities.Add(entityAlive);
	}

	private void OnTriggerExit(Collider other)
    {
		if (!enabled)
			return;
		//Log.Out("Explosive Area trigger exited!");
		EntityAlive entityAlive = this.getEntityFromCollider(other) as EntityAlive;
		if (entityAlive == null)
			return;
		list_entities.Remove(entityAlive);
    }

	private HashSet<EntityAlive> list_entities = new HashSet<EntityAlive>();
	private EntityPlayer player = null;
	private MinEventParams data = null;
	private ItemValue item_value = null;
	ExplosionParams cur_params;
}