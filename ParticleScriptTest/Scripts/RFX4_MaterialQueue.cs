using UnityEngine;

[RequireComponent(typeof(Renderer))]
[ExecuteInEditMode]
public class RFX4_MaterialQueue : MonoBehaviour
{
  [Tooltip("Background=1000, Geometry=2000, AlphaTest=2450, Transparent=3000, Overlay=4000")]
  public int queue = 2000;

  public int[] queues;

  void Start()
  {
    Renderer renderer = GetComponent<Renderer>();
    if (!renderer || !renderer.sharedMaterial || queues == null)
      return;
    renderer.sharedMaterial.renderQueue = queue;
    for (int i = 0; i < queues.Length && i < renderer.sharedMaterials.Length; i++)
      renderer.sharedMaterials[i].renderQueue = queues[i];
  }

  void OnValidate()
  {
    Start();
  }

  void Update()
  {
    if (Application.isPlaying) return; 
    Start();
  }
}