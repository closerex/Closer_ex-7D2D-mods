using System.Collections.Generic;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public class ProximityBeep : MonoBehaviour
    {
        [SerializeField]
        private LightController lightController;
        [SerializeField]
        private string beepSound;
        [SerializeField]
        private float beepIntervalMin = 1f;
        [SerializeField]
        private float beepIntervalMax = 3f;
        [SerializeField]
        private float detectionRadiusMin = 5f;
        [SerializeField]
        private float detectionRadiusMax = 20f;
        [SerializeField]
        private float checkInRangeInterval = 0.5f;

#if NotEditor
        private float nextCheckTime = 0f;
        private readonly List<Entity> entityCache = new List<Entity>();
        private float curBeepInterval;
        private float beepProgress = 0f;

        private void OnEnable()
        {
            nextCheckTime = 0f;
            curBeepInterval = beepIntervalMax;
            beepProgress = 0f;
            if (lightController)
            {
                lightController.enabled = false; ;
            }
        }

        private void Update()
        {
            if (Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + checkInRangeInterval;
                entityCache.Clear();
                GameManager.Instance.World.GetEntitiesAround(EntityFlags.All, Origin.position + transform.position, detectionRadiusMax, entityCache);
                float closestDistanceSqr = detectionRadiusMax * detectionRadiusMax;
                foreach (Entity ent in entityCache)
                {
                    if (ent != null)
                    {
                        float distSqr = (ent.position - (Origin.position + transform.position)).sqrMagnitude;
                        if (distSqr < closestDistanceSqr)
                        {
                            closestDistanceSqr = distSqr;
                        }
                    }
                }

                float beepSpeedMultiplier = Mathf.InverseLerp(detectionRadiusMin * detectionRadiusMin, detectionRadiusMax * detectionRadiusMax, closestDistanceSqr);
                curBeepInterval = Mathf.Lerp(beepIntervalMin, beepIntervalMax, beepSpeedMultiplier);
            }

            if (curBeepInterval > 0)
            {
                beepProgress += Time.deltaTime / curBeepInterval;
            }
            if (beepProgress >= 1f)
            {
                beepProgress = 0f;
                if (!string.IsNullOrEmpty(beepSound))
                {
                    Audio.Manager.Play(transform.position + Origin.position, beepSound, -1, false);
                }
                if (lightController)
                {
                    lightController.enabled = true;
                }
            }
        }
#endif
    }
}
