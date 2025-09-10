#if NotEditor
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine.Rendering.PostProcessing;
#endif
using UnityEngine;

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
        [Header("Core")]
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
        [Header("Reticle Scaling")]
        [SerializeField]
        private bool scaleReticle = false;
        [SerializeField]
        private Vector2 reticleSizeRange = new Vector2(1, 1);
        //[SerializeField]
        //private bool scaleDownReticle = false;
        //[SerializeField]
        //private float reticleScaleRatio = 1.0f;
        [Header("Camera Texture Size And Procedural Aiming")]
        [SerializeField]
        private Transform aimRef;
        [SerializeField]
        private float lensSizeFull;
        [SerializeField]
        private float lensSizeValid;

        private float initialReticleScale = 1f;
        private float initialFov = 55f;
        private float textureHeight = Screen.height;

#if NotEditor
        private EntityPlayerLocal player;
        private int itemSlot = -1;
        private ItemActionZoom.ItemActionDataZoom zoomActionData;
        private bool IsVariableZoom => variableZoom && variableZoomData != null;
        private ActionModuleVariableZoom.VariableZoomData variableZoomData;
        private float targetStep = 0;
        private float currentStep = 0;
        private float stepVelocity = 0;
#else
        [Header("Editor Debug")]
        public Camera debugCamera;
        public float debugScale = 2f;
#endif
        private void Awake()
        {
            renderTarget = GetComponent<Renderer>();
            if (!renderTarget)
            {
                Destroy(this);
                return;
            }
#if NotEditor

            if (newShader == null)
            {
                newShader = LoadManager.LoadAsset<Shader>("#@modfolder(CommonUtilityLib):Resources/PIPScope.unity3d?PIPScope.shadergraph", null, null, false, true).Asset;
            }
            if (renderTarget.material.shader.name == "Shader Graphs/MagnifyScope" || renderTarget.material.shader.name == "Shader Graphs/PIPScopeNew")
            {
                renderTarget.material.shader = newShader;
            }
            renderTarget.material.renderQueue = 3000;
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
            var entity = this.GetLocalPlayerInParent();
            if (!entity)
            {
                Destroy(gameObject);
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
            CalcInitialFov();
            //inventory holding item is not set when creating model, this might be an issue for items with base scope that has this script attached
            //workaround taken from alternative action module, which keeps a reference to the ItemValue being set until its custom data is created
            //afterwards it's set to null so we still need to access holding item when this method is triggered by mods
            if (itemSlot != player.inventory.holdingItemIdx)
            {
                Log.Out($"Scope shader script: Expecting holding item idx {itemSlot} but getting {player.inventory.holdingItemIdx}!");
                return;
            }
            var zoomAction = ((ActionModuleAlternative.InventorySetItemTemp?.ItemClass ?? player.inventory.holdingItem).Actions[1]) as ItemActionZoom;
            if (zoomAction == null)
            {
                Destroy(gameObject);
                return;
            }
            zoomActionData = (ItemActionZoom.ItemActionDataZoom)player.inventory.holdingItemData.actionData[1];
            variableZoomData = (zoomActionData as IModuleContainerFor<ActionModuleVariableZoom.VariableZoomData>)?.Instance;
            if (variableZoomData != null && (variableZoom || variableZoomData.forceFov))
            {
                if (variableZoom)
                {
                    variableZoomData.shouldUpdate = false;
                    targetStep = currentStep = variableZoomData.curStep;
                    stepVelocity = 0f;
                    targetFov = CalcCurrentFov();
                }
                else
                {
                    targetFov = variableZoomData.fovRange.min;
                }
            }
            else
            {
                string originalRatio = zoomAction.Properties.GetString("ZoomRatio");
                if (string.IsNullOrEmpty(originalRatio))
                {
                    originalRatio = "0";
                }
                targetFov = StringParsers.ParseFloat(player.inventory.holdingItemItemValue.GetPropertyOverride("ZoomRatio", originalRatio));
                targetFov = ScaleToFov(targetFov);
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
            if (IsVariableZoom)
            {
                if (variableZoomData.shouldUpdate)
                {
                    variableZoomData.shouldUpdate = false;
                    targetStep = variableZoomData.curStep;
                }
                if (currentStep != targetStep)
                {
                    if (variableZoomData.isToggleOnly)
                    {
                        currentStep = targetStep;
                    }
                    else
                    {
                        currentStep = Mathf.SmoothDamp(currentStep, targetStep, ref stepVelocity, 0.05f);
                    }
                    UpdateFOV(CalcCurrentFov());
                }
            }

            if (!manualControl && zoomActionData != null)
            {
                if (player.bFirstPersonView)
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
                else if (pipCamera.gameObject.activeSelf)
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
            currentStep = targetStep = stepVelocity = 0f;
#else
            if(debugCamera == null)
            {
                Destroy(this);
            }
#endif
        }

        private float ScaleToFov(float scale)
        {
            return Mathf.Rad2Deg * 2 * Mathf.Atan(Mathf.Tan(Mathf.Deg2Rad * initialFov * 0.5f) / scale);
        }

        private float FovToScale(float fov)
        {
            return Mathf.Tan(Mathf.Deg2Rad * initialFov * 0.5f) / Mathf.Tan(Mathf.Deg2Rad * fov * 0.5f);
        }

#if NotEditor
        private void CalcInitialFov()
        {
            if (aimRef)
            {
                var distance = Mathf.Abs(Vector3.Dot(renderTarget.bounds.center - aimRef.position, aimRef.forward));
                var scaleFov = lensSizeValid / (2 * distance * Mathf.Tan(Mathf.Deg2Rad * 27.5f));
                var scaleTexture = lensSizeFull / (2 * distance * Mathf.Tan(Mathf.Deg2Rad * 27.5f));
                textureHeight = scaleTexture * Screen.height;
                //textureHeight = Mathf.Abs(player.playerCamera.WorldToScreenPoint(player.playerCamera.transform.forward * distance + player.playerCamera.transform.up * height).y - 
                //                          player.playerCamera.WorldToScreenPoint(player.playerCamera.transform.forward * distance - player.playerCamera.transform.up * height).y);
                initialFov = Mathf.Rad2Deg * 2 * Mathf.Atan(Mathf.Tan(Mathf.Deg2Rad * 27.5f) * scaleFov);
                Log.Out($"distance {distance}, scale fov {scaleFov}, scale texture {scaleTexture} texture height {textureHeight} initial fov {initialFov}");
                return;
            }
            textureHeight = Screen.height * 0.5f;
            initialFov = 15;
        }

        private static float CalcFovStep(float t, float fovMin, float fovMax)
        {
            return 2f * Mathf.Rad2Deg * Mathf.Atan(Mathf.Lerp(Mathf.Tan(fovMax * 0.5f * Mathf.Deg2Rad), Mathf.Tan(fovMin * 0.5f * Mathf.Deg2Rad), t));
        }

        private float CalcCurrentFov()
        {
            if (!IsVariableZoom)
            {
                throw new Exception("Variable Zoom is not set!");
            }
            float targetFov;
            if (variableZoomData.forceFov)
            {
                targetFov = CalcFovStep(currentStep, variableZoomData.fovRange.min, variableZoomData.fovRange.max);
            }
            else
            {
                targetFov = ScaleToFov(Mathf.Lerp(variableZoomData.minScale, variableZoomData.maxScale, currentStep));
            }
            return targetFov;
        }
#endif

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
                pipCamera.fieldOfView = targetFov;
#if NotEditor
                if (scaleReticle && IsVariableZoom)
                {
                    renderTarget.material.SetFloat("_ReticleScale", Mathf.Lerp(reticleSizeRange.x, reticleSizeRange.y, currentStep));
                    //if (variableZoomData.maxScale > variableZoomData.minScale)
                    //{
                    //    float minScale;
                    //    if (reticleScaleRatio >= 1)
                    //    {
                    //        minScale = scaleDownReticle ? 1 - (variableZoomData.maxScale * reticleScaleRatio - variableZoomData.minScale) / (variableZoomData.maxScale * reticleScaleRatio) : 1;
                    //    }
                    //    else
                    //    {
                    //        minScale = scaleDownReticle ? 1 - reticleScaleRatio * (variableZoomData.maxScale - variableZoomData.minScale) / variableZoomData.maxScale : 1;
                    //    }
                    //    float maxScale;
                    //    if (reticleScaleRatio >= 1)
                    //    {
                    //        maxScale = scaleDownReticle ? 1 : variableZoomData.maxScale * reticleScaleRatio / variableZoomData.minScale;
                    //    }
                    //    else
                    //    {
                    //        maxScale = scaleDownReticle ? 1 : 1 + reticleScaleRatio * (variableZoomData.maxScale - variableZoomData.minScale) / variableZoomData.minScale;
                    //    }
                    //    float reticleScale = Mathf.Lerp(minScale, maxScale, variableZoomData.curStep);
                    //    renderTarget.material.SetFloat("_ReticleScale", initialReticleScale / reticleScale);
                    //}
                    //else
                    //{
                    //    renderTarget.material.SetFloat("_ReticleScale", initialReticleScale);
                    //}
                }
                //Log.Out($"target fov {targetFov} target scale {targetScale}");
#endif
            }
        }

        private void CreateCamera()
        {
            const float texScale = 1f;
            targetTexture = new RenderTexture((int)(textureHeight * aspectRatio), (int)(textureHeight), 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
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
            pipCamera.targetTexture = targetTexture;
            pipCamera.depth = -2;
            pipCamera.fieldOfView = 55;
            pipCamera.nearClipPlane = 0.05f;
            pipCamera.farClipPlane = 5000;
            pipCamera.aspect = aspectRatio;
            pipCamera.rect = new Rect(0, 0, texScale, texScale);
#if NotEditor
            //pipCamera.CopyFrom(player.playerCamera);
            pipCamera.cullingMask = player.playerCamera.cullingMask;
            //renderTarget.material.SetFloat("_AspectMain", player.playerCamera.aspect);
            //renderTarget.material.SetFloat("_AspectScope", pipCamera.aspect);
#else
            pipCamera.CopyFrom(debugCamera);
#endif
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
