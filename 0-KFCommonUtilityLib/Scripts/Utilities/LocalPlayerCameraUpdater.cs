using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public class LocalPlayerCameraUpdater : MonoBehaviour
    {
        public Camera currentCamera;
        public CameraMatrixOverride cameraMatrixOverride;

        private float finalYaw, finalPitch, inputYaw, inputPitch;

        private void Awake()
        {
            currentCamera = GetComponent<Camera>();
        }

        public static LocalPlayerCameraUpdater FindUpdater(Transform root)
        {
            var newCameraTransform = root.Find("NewCameraTransform");
            if (newCameraTransform)
            {
                return newCameraTransform.GetComponent<LocalPlayerCameraUpdater>();
            }
            return null;
        }

        public void SaveAndResetRotation(vp_FPCamera fpCamera)
        {
            UpdatePosition(fpCamera);
            currentCamera.transform.rotation = fpCamera.Transform.rotation;

            finalYaw = fpCamera.Yaw;
            finalPitch = fpCamera.Pitch;
            //reset original camera's position and rotation
            //todo: record player movement and target rotation
            fpCamera.Yaw = inputYaw;
            fpCamera.Pitch = inputPitch;

            Quaternion yaw = Quaternion.AngleAxis(inputYaw, Vector3.up);
            Quaternion pitch = Quaternion.AngleAxis(-inputPitch, Vector3.left);
            fpCamera.Transform.rotation = yaw * pitch;
        }

        public void RestoreRotation(vp_FPCamera fpCamera)
        {
            fpCamera.Transform.rotation = currentCamera.transform.rotation;
            fpCamera.m_Yaw = finalYaw;
            fpCamera.m_Pitch = finalPitch;
        }

        public void ApplyDiffToInput(vp_FPCamera fpCamera)
        {
            float yawDiff = fpCamera.Yaw - finalYaw;
            float pitchDiff = fpCamera.Pitch - finalPitch;
            inputYaw += yawDiff;
            inputPitch += pitchDiff;
        }

        public void ApplyFinal(vp_FPCamera fpCamera)
        {
            fpCamera.Yaw = finalYaw;
            fpCamera.Pitch = finalPitch;
        }

        public void UpdateFinal(vp_FPCamera fpCamera)
        {
            finalYaw = fpCamera.Yaw;
            finalPitch = fpCamera.Pitch;
        }

        public void ApplyInput(vp_FPCamera fpCamera)
        {
            fpCamera.Yaw = inputYaw;
            fpCamera.Pitch = inputPitch;
        }

        public void UpdateInput(vp_FPCamera fpCamera)
        {
            inputYaw = fpCamera.Yaw;
            inputPitch = fpCamera.Pitch;
        }

        public void UpdatePosition(vp_FPCamera fpCamera)
        {
            currentCamera.transform.position = fpCamera.Transform.position;
        }

        public void UpdateRotation(vp_FPCamera fpCamera)
        {
            currentCamera.transform.rotation = fpCamera.Transform.rotation;
            finalYaw = fpCamera.Yaw;
            finalPitch = fpCamera.Pitch;
            inputYaw = fpCamera.Yaw;
            inputPitch = fpCamera.Pitch;
        }

        private void OnPreCull()
        {
            cameraMatrixOverride.OnPreCull();
        }

        private void OnPreRender()
        {
            cameraMatrixOverride.OnPreRender();
        }

        private void OnPostRender()
        {
            cameraMatrixOverride.OnPostRender();
        }
    }
}
