using UnityEngine;

public class RFX4_StartDelay : MonoBehaviour
{

    public GameObject ActivatedGameObject;
    public float Delay = 1;

    private float currentTime = 0;
    private bool isEnabled;

    // Use this for initialization
    void OnEnable()
    {
        ActivatedGameObject.SetActive(false);
        isEnabled = false;
        // Invoke("ActivateGO", Delay);
        currentTime = 0;
    }

    void Update()
    {
        currentTime += Time.deltaTime;
        if (!isEnabled && currentTime >= Delay)
        {
            isEnabled = true;
            ActivatedGameObject.SetActive(true);
          
        }
    }
}
