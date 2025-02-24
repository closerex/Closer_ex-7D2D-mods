#if NotEditor
using HarmonyLib;
using System.Reflection;

#endif
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace KFCommonUtilityLib.KFAttached.Render
{
    [AddComponentMenu("KFAttachments/Render Utils/Magnify Scope")]
    [RequireComponent(typeof(Renderer))]
    public class MagnifyScope : MonoBehaviour
    {
#if NotEditor
        private static Shader newShader;
        private static FieldInfo fieldResources = AccessTools.Field(typeof(PostProcessLayer), "m_Resources");
#endif
        private RenderTexture targetTexture;
        private Renderer renderTarget;

        private Camera pipCamera;
        [SerializeField]
        private bool manualControl = false;
        [SerializeField]
        private Transform cameraJoint;
        [SerializeField]
        private float aspectRatio = 1.0f;
        [SerializeField]
        private bool hideFpvModelInScope = false;
        [SerializeField]
        private bool variableZoom = false;
        [SerializeField]
        private bool scaleReticle = false;
        [SerializeField]
        private bool scaleDownReticle = false;
        [SerializeField]
        private float reticleScaleRatio = 1.0f;

        private float initialReticleScale = 1f;

#if NotEditor
        private EntityPlayerLocal player;
        private int itemSlot = -1;
        private ItemActionZoom.ItemActionDataZoom zoomActionData;
        private bool IsVariableZoom => variableZoom && variableZoomData != null;
        private ActionModuleVariableZoom.VariableZoomData variableZoomData;
#else
        public Camera debugCamera;
        public float debugScale = 2f;
#endif
        private void Awake()
        {
            renderTarget = GetComponent<Renderer>();
            if (renderTarget == null)
            {
                Destroy(this);
                return;
            }
#if NotEditor

            if (newShader == null)
            {
                newShader = LoadManager.LoadAsset<Shader>("#@modfolder(CommonUtilityLib):Resources/PIPScope.unity3d?PIPScope.shadergraph", null, null, false, true).Asset;
            }
            if (renderTarget.material.shader.name == "Shader Graphs/MagnifyScope")
            {
                renderTarget.material.shader = newShader;
            }
            initialReticleScale = renderTarget.material.GetFloat("_ReticleScale");
#else
            if(debugCamera == null)
            {
                Destroy(this);
                return;
            }
#endif
            //if (!player.playerCamera.TryGetComponent<BokehBlurTargetRef>(out var bokeh))
            //{
            //    bokeh = player.playerCamera.gameObject.AddComponent<BokehBlurTargetRef>();
            //}
            //bokeh.target = this;
            // Precompute rotations
        }

#if NotEditor
        private void Start()
        {
            var entity = GetComponentInParent<EntityPlayerLocal>();
            if (!entity)
            {
                Destroy(this);
                return;
            }
            player = entity;
            itemSlot = player.inventory.holdingItemIdx;
            OnEnable();
        }
#endif

        private void OnEnable()
        {
            float targetFov;
#if NotEditor
            if (!player)
            {
                return;
            }
            //inventory holding item is not set when creating model, this might be an issue for items with base scope that has this script attached
            //workaround taken from alternative action module, which keeps a reference to the ItemValue being set until its custom data is created
            //afterwards it's set to null so we still need to access holding item when this method is triggered by mods
            if (itemSlot != player.inventory.holdingItemIdx)
            {
                Log.Out($"Scope shader script: Expecting holding item idx {itemSlot} but getting {player.inventory.holdingItemIdx}!");
                return;
            }
            var zoomAction = (ItemActionZoom)((ActionModuleAlternative.InventorySetItemTemp?.ItemClass ?? player.inventory.holdingItem).Actions[1]);
            zoomActionData = (ItemActionZoom.ItemActionDataZoom)player.inventory.holdingItemData.actionData[1];
            if (variableZoom && zoomActionData is IModuleContainerFor<ActionModuleVariableZoom.VariableZoomData> zoomDataModule)
            {
                variableZoomData = zoomDataModule.Instance;
                targetFov = variableZoomData.curFov;
                variableZoomData.shouldUpdate = false;
            }
            else
            {
                string originalRatio = zoomAction.Properties.GetString("ZoomRatio");
                if (string.IsNullOrEmpty(originalRatio))
                {
                    originalRatio = "0";
                }
                targetFov = StringParsers.ParseFloat(player.inventory.holdingItemItemValue.GetPropertyOverride("ZoomRatio", originalRatio));
                targetFov = Mathf.Rad2Deg * 2 * Mathf.Atan(Mathf.Tan(Mathf.Deg2Rad * 7.5f) / Mathf.Sqrt(targetFov));
            }

#else
            if(debugCamera == null)
            {
                Destroy(this);
            }
            targetFov = Mathf.Rad2Deg * 2 * Mathf.Atan(Mathf.Tan(Mathf.Deg2Rad * 27.5f) / Mathf.Sqrt(debugScale));
#endif
            CreateCamera();
            UpdateFOV(targetFov);
        }

#if NotEditor
        private void Update()
        {
            if (IsVariableZoom && variableZoomData.shouldUpdate)
            {
                UpdateFOV(variableZoomData.curFov);
                variableZoomData.shouldUpdate = false;
            }

            if (!manualControl && zoomActionData != null)
            {
                bool aimingGun = player.AimingGun;
                if (aimingGun && !pipCamera.gameObject.activeSelf)
                {
                    pipCamera.gameObject.SetActive(true);
                }
                else if (!aimingGun && !zoomActionData.bZoomInProgress && pipCamera.gameObject.activeSelf)
                {
                    pipCamera.gameObject.SetActive(false);
                }
            }
        }
#endif

        private void OnDisable()
        {
            DestroyCamera();
#if NotEditor
#else
            if(debugCamera == null)
            {
                Destroy(this);
            }
#endif
        }

        private void DestroyCamera()
        {
            if (targetTexture && targetTexture.IsCreated())
            {
                targetTexture.Release();
                Destroy(targetTexture);
            }
            if (pipCamera)
            {
                Destroy(pipCamera.gameObject);
            }
        }

        private void UpdateFOV(float targetFov)
        {
            if (targetFov > 0)
            {
//#if NotEditor
//                float targetFov = targetScale;
//#else
//                float targetFov = Mathf.Rad2Deg * 2 * Mathf.Atan(Mathf.Tan(Mathf.Deg2Rad * 27.5f) / Mathf.Sqrt(targetScale));
//#endif
                pipCamera.fieldOfView = targetFov;
#if NotEditor
                if (scaleReticle)
                {
                    if (variableZoomData.maxScale > variableZoomData.minScale)
                    {
                        float minScale;
                        if (reticleScaleRatio >= 1)
                        {
                            minScale = scaleDownReticle ? 1 - (variableZoomData.maxScale * reticleScaleRatio - variableZoomData.minScale) / (variableZoomData.maxScale * reticleScaleRatio) : 1;
                        }
                        else
                        {
                            minScale = scaleDownReticle ? 1 - reticleScaleRatio * (variableZoomData.maxScale - variableZoomData.minScale) / variableZoomData.maxScale : 1;
                        }
                        float maxScale;
                        if (reticleScaleRatio >= 1)
                        {
                            maxScale = scaleDownReticle ? 1 : variableZoomData.maxScale * reticleScaleRatio / variableZoomData.minScale;
                        }
                        else
                        {
                            maxScale = scaleDownReticle ? 1 : 1 + reticleScaleRatio * (variableZoomData.maxScale - variableZoomData.minScale) / variableZoomData.minScale;
                        }
                        //float reticleScale = Mathf.Lerp(minScale, maxScale, (variableZoomData.curScale - variableZoomData.minScale) / (variableZoomData.maxScale - variableZoomData.minScale));
                        float reticleScale = Mathf.Lerp(minScale, maxScale, variableZoomData.curStep);
                        renderTarget.material.SetFloat("_ReticleScale", initialReticleScale / reticleScale);
                    }
                    else
                    {
                        renderTarget.material.SetFloat("_ReticleScale", initialReticleScale);
                    }
                }
                //Log.Out($"target fov {targetFov} target scale {targetScale}");
#endif
            }
        }

        private void CreateCamera()
        {
            targetTexture = new RenderTexture((int)(Screen.height * 0.5f * aspectRatio), (int)(Screen.height * 0.5f), 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                filterMode = FilterMode.Bilinear
            };
            renderTarget.material.mainTexture = targetTexture;
            GameObject cameraGO = new GameObject("KFPiPCam");
            if (cameraJoint != null)
            {
                cameraGO.transform.parent = cameraJoint.transform;
            }
            else
            {
                cameraGO.transform.parent = transform;
            }

            pipCamera = cameraGO.AddComponent<Camera>();
#if NotEditor
            //pipCamera.CopyFrom(player.playerCamera);
            pipCamera.cullingMask = player.playerCamera.cullingMask;
#else
            pipCamera.CopyFrom(debugCamera);
#endif
            pipCamera.targetTexture = targetTexture;
            pipCamera.depth = -2;
            pipCamera.fieldOfView = 55;
            pipCamera.nearClipPlane = 0.05f;
            pipCamera.farClipPlane = 5000;
            pipCamera.aspect = aspectRatio;
            if (cameraJoint == null || hideFpvModelInScope)
            {
                pipCamera.cullingMask &= ~(1024);
            }
            else
            {
                pipCamera.cullingMask |= 1024;
            }
            cameraGO.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            cameraGO.transform.localScale = Vector3.one;

#if NotEditor
            WeaponCameraFollow weaponCameraFollow = cameraGO.AddComponent<WeaponCameraFollow>();
            weaponCameraFollow.targetTexture = targetTexture;
            weaponCameraFollow.dynamicSensitivityData = (zoomActionData as IModuleContainerFor<ActionModuleDynamicSensitivity.DynamicSensitivityData>)?.Instance;
            weaponCameraFollow.player = player;
            var old = player.playerCamera.GetComponent<PostProcessLayer>();
            var layer = pipCamera.gameObject.GetOrAddComponent<PostProcessLayer>();
            //layer.antialiasingMode = old.antialiasingMode;
            //layer.superResolution = (SuperResolution)old.superResolution.GetType().CreateInstance();
            layer.Init(fieldResources.GetValue(old) as PostProcessResources);
            //weaponCameraFollow.UpdateAntialiasing();
#endif
        }

        internal void RenderImageCallback(RenderTexture source, RenderTexture destination)
        {
        }
    }
}
