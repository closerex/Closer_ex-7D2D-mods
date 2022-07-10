using System.Collections.Generic;
using UnityEngine;

public class AimAssistHelper : MonoBehaviour
{
    public EntityAlive aimTarget { get; private set; } = null;
    public EntityPlayerLocal localPlayer;
    private HashSet<Transform> hash_intrans = new HashSet<Transform>();

    private void Update()
    {
        if (aimTarget != null && aimTarget.IsDead())
        {
            //Log.Out("target no longer valid: " + aimTarget.EntityName + " " + aimTarget.entityId);
            aimTarget = null;
            hash_intrans.Clear();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag.StartsWith("E_"))
        {
            Transform root = GameUtils.GetHitRootTransform(other.tag, other.transform);
            EntityAlive entity = root?.GetComponent<EntityAlive>();
            if (entity != null)
            {
                if (entity == aimTarget)
                {
                    //Log.Out("the same target: " + aimTarget.EntityName + " " + aimTarget.entityId);
                    if (!hash_intrans.Contains(other.transform))
                        hash_intrans.Add(other.transform);
                    return;
                }

                if (aimTarget != null)
                {
                    //Log.Out("target already set: " + aimTarget.EntityName + " " + aimTarget.entityId);
                    return;
                }

                if (entity != null && entity.IsAlive() && (entity is EntityEnemy || FactionManager.Instance.GetRelationshipTier(localPlayer, entity) < FactionManager.Relationship.Neutral))
                {
                    if(Physics.Raycast(transform.parent.position, entity.getHeadPosition() - Origin.position - transform.parent.position, out var hitInfo, transform.localScale.y, -538750997))
                    {
                        if(!hitInfo.transform.tag.StartsWith("E_"))
                        {
                            //Log.Out("target not visible: " + entity.EntityName + " " + entity.entityId);
                            return;
                        }

                        Transform hitRoot = GameUtils.GetHitRootTransform(hitInfo.transform.tag, hitInfo.transform);
                        EntityAlive hitEntity = hitRoot?.GetComponent<EntityAlive>();
                        if(hitEntity != null && hitEntity != entity)
                        {
                            //Log.Out("target " + entity.EntityName + " " + entity.entityId + " blocked by entity " + hitEntity.EntityName + " " + hitEntity.entityId);
                            return;
                        }    
                    }
                    aimTarget = entity;
                    hash_intrans.Add(other.transform);
                    //Log.Out("aim target is now: " + aimTarget.EntityName + " " + aimTarget.entityId);
                }
            }
            //else
                //Log.Out("entity not found on target: " + other.transform.name + " " + other.tag + " is trigger: " + other.isTrigger);
        }
        //else
            //Log.Out("entity tag not found: " + other.transform.name + " " + other.tag + " is trigger: " + other.isTrigger);
    }

    private void OnTriggerExit(Collider other)
    {
        if (aimTarget == null || !other.tag.StartsWith("E_"))
            return;

        Transform root = GameUtils.GetHitRootTransform(other.tag, other.transform);
        if(root != null)
        {
            EntityAlive entity = root.GetComponent<EntityAlive>();
            if (entity != null && entity == aimTarget)
            {
                hash_intrans.Remove(other.transform);
                if(hash_intrans.Count == 0)
                {
                    //Log.Out("target leave aim zone: " + aimTarget.EntityName + " " + aimTarget.entityId);
                    aimTarget = null;
                }
            }
        }
    }
}

