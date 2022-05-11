using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SubExplosionController : MonoBehaviour
{
    void Awake()
    {
        List<int> list_index = null;
        string[] arr_trans = null;
        CustomParticleComponents cur_component = CustomParticleEffectLoader.LastInitializedComponent;
        if (cur_component.TryGetCustomProperty(CustomParticleLoaderMultiExplosionPatches.str_sub_explosion, out object com))
            list_index = com as List<int>;
            
        if(cur_component.TryGetCustomProperty(CustomParticleLoaderMultiExplosionPatches.str_sub_explosion_transform, out object str))
            arr_trans = str as string[];

        if(list_index != null && arr_trans != null && list_index.Count <= arr_trans.Length)
        {
            EntityAlive entityAlive = GameManager.Instance.World.GetEntity(cur_component.CurrentExplosionParams._playerId) as EntityAlive;
            //if client is initiator, proc sub explosion on client
            //if initiator is not player, proc sub explosion on closest player client
            //if server is hosted, proc sub explosion on server
            World world = GameManager.Instance.World;
            EntityPlayer closestPlayer = null;
            bool procExplosion = (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient
                                  && ((entityAlive && entityAlive is EntityPlayer player && player.entityId == world.GetPrimaryPlayerId())
                                      || ((!entityAlive || !(entityAlive is EntityPlayer))
                                          && (closestPlayer = world.GetClosestPlayer(cur_component.CurrentExplosionParams._worldPos, -1, false)) != null
                                          && closestPlayer.entityId == world.GetPrimaryPlayerId())))
                              || (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && entityAlive is EntityPlayerLocal);

            //Log.Out("proc explosion: " + procExplosion + " is client: " + SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient + " primary player id: " + world.GetPrimaryPlayerId());
            for (int i = 0, j = i; i < arr_trans.Length; ++i, ++j)
            {
                bool sync = false;
                bool explode = false;
                bool explodeOnDeath = false;
                bool explodeOnBoth = false;
                //bool ballistic = false;
                string str_trans = arr_trans[i].Trim();
                Transform trans = null;
                if(str_trans != null)
                {
                    explode = str_trans.StartsWith("$");
                    if (explode)
                    {
                        str_trans = str_trans.Remove(0, 1);
                        sync = true;
                    }else
                    {
                        explode = !str_trans.StartsWith("#");
                        if (!explode)
                        {
                            str_trans = str_trans.Remove(0, 1);
                            sync = true;
                        }
                    }

                    explodeOnDeath = str_trans.EndsWith("$");
                    if (explodeOnDeath)
                        str_trans = str_trans.Remove(str_trans.Length - 1);
                    else
                    {
                        explodeOnBoth = str_trans.EndsWith("#");
                        if (explodeOnBoth)
                        {
                            explodeOnDeath = true;
                            str_trans = str_trans.Remove(str_trans.Length - 1);
                        }
                    }
                    /*
                    ballistic = str_trans.EndsWith("$");
                    if (ballistic)
                        str_trans = str_trans.TrimEnd('$');
                    */
                    if (!explode)
                        --j;
                    //Log.Out(str_trans + " sync: " + sync + " explode: " + explode);

                    trans = transform.FindInChildren(str_trans);
                }

                if (trans != null)
                {
                    //Log.Out("Subexplosion initialized!");
                    if (sync)
                    {
                        //Log.Out("Sync particle on transform: " + trans.name);
                        ParticleSyncController controller = trans.gameObject.AddComponent<ParticleSyncController>();
                    }
                    trans.gameObject.AddComponent<InitialCollisionHandler>();
                    if(procExplosion)
                    {
                        CustomParticleComponents component = null;
                        if(explode)
                        {
                            //Log.Out("Adding SubExplosionInitializer to transform: " + trans.name);
                            int index = list_index[j];
                            if (!CustomParticleEffectLoader.GetCustomParticleComponents(index, out component) || component == null)
                            {
                                Log.Error("SubExplosionController: refering to explosion index that does not exist!");
                                Destroy(gameObject);
                                return;
                            }
                            SubExplosionInitializer initializer = trans.gameObject.AddComponent<SubExplosionInitializer>();
                            int clrIdx = cur_component.CurrentExplosionParams._clrIdx;
                            //Log.Out(initializer.name);
                            initializer.entityAlive = entityAlive;
                            initializer.clrIdx = clrIdx;
                            initializer.data = component.BoundExplosionData;
                            if (component.BoundItemClass != null)
                                initializer.value = new ItemValue(component.BoundItemClass.Id);
                            if (explodeOnDeath)
                                initializer.SetExplodeOnDeath(explodeOnBoth);
                            /*
                            if(!ballistic)
                            {
                                Vector3i blockPos = component.CurrentExplosionParams._blockPos;
                                Chunk chunk = GameManager.Instance.World.ChunkClusters[clrIdx].GetChunkSync(World.toChunkXZ(blockPos.x), blockPos.y, World.toChunkXZ(blockPos.z)) as Chunk;
                                ballistic = chunk == null || !chunk.GetAvailable();
                            }
                            initializer.SetBallistic(ballistic);
                            */
                            
                            //Log.Out("Particle index: " + initializer.data.ParticleIndex.ToString() + " PlayerId: " + initializer.entityid.ToString() + " Item: " + (initializer.value != null ? initializer.value.ItemClass.Name : string.Empty));
                        }
                    }
                }
            }
        }
    }
}