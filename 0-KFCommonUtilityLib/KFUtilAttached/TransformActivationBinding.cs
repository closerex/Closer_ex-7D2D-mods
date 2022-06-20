using UnityEngine;

public class TransformActivationBinding : MonoBehaviour
{
    [SerializeField]
    private GameObject[] bindings;

    private void OnEnable()
    {
        foreach(GameObject t in bindings)
            t.SetActive(true);
    }

    private void OnDisable()
    {
        foreach (GameObject t in bindings)
            t.SetActive(false);
    }
}
