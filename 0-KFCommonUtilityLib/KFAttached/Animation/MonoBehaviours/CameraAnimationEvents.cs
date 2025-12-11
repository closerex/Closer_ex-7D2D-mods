#if NotEditor
using GearsAPI.Settings.Global;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Gears;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Animations;
using System.Linq;
using UnityEditor;
#endif

[AddComponentMenu("KFAttachments/Utils/Camera Animation Events")]
[DefaultExecutionOrder(0)]
[DisallowMultipleComponent]
public class CameraAnimationEvents : MonoBehaviour, IPlayableGraphRelated
#if UNITY_EDITOR
    , ISerializationCallbackReceiver
#endif
{
    [Serializable]
    public enum CurveType
    {
        [InspectorName(null)]
        Position,
        EularAngleRaw,
        EularAngleBaked,
        Quaternion
    }

    public class CameraCurveData
    {
        AnimationCurve[] curves;
        float[] values, initialValues;
        float clipLength, delay, curTime, blendInTime, curBlendInTime, blendOutTime, curBlendOutTime, curInterruptTime, speed, weight;
        CurveType curveType;
        int speedParamHash;
        bool relative;
        bool loop;
        bool alwaysBlendOut;
        bool interrupted;
        public int weightTagHash;
        public int stateHash;

        public CameraCurveData(int weightTagHash, int stateHash, AnimationCurve[] curves, float clipLength, float delay, float blendInTime, float blendOutTime, float speed, float weight, CurveType curveType, bool relative, bool loop, bool alwaysBlendOut, int speedParamHash = 0)
        {
            this.curves = curves;
            this.clipLength = clipLength;
            this.delay = loop ? 0f : delay;
            this.blendInTime = blendInTime;
            this.blendOutTime = blendOutTime;
            this.speed = speed;
            this.speedParamHash = speedParamHash;
            this.curveType = curveType;
            this.relative = relative;
            this.loop = loop;
            this.alwaysBlendOut = alwaysBlendOut;
            this.weight = weight;
            this.weightTagHash = weightTagHash;
            this.stateHash = stateHash;
            values = new float[curves.Length];
            initialValues = new float[curves.Length];
            if (relative)
            {
                for (int i = 0; i < curves.Length; i++)
                {
                    initialValues[i] = curves[i]?.Evaluate(0) ?? 0;
                }
            }
        }

        public bool Finished => curTime >= clipLength || (interrupted && (curBlendOutTime >= blendOutTime || curTime < delay));

        public void Update(Animator animator, float dt)
        {
            float dynamicSpeed = this.speed;
            if (speedParamHash != 0)
            {
                dynamicSpeed *= animator.GetWrappedFloat(speedParamHash);
            }

            dt *= dynamicSpeed;
            curBlendInTime += dt;
            curTime += dt;
            if (curTime < delay)
            {
                return;
            }
            if (loop)
            {
                curTime %= clipLength;
            }
            if (interrupted)
            {
                curInterruptTime += dt;
            }
            curBlendOutTime = Mathf.Max(curInterruptTime, loop || !alwaysBlendOut ? 0 : curTime + blendOutTime - clipLength);
            for (int i = 0; i < curves.Length; i++)
            {
                if (curves[i] == null)
                {
                    continue;
                }

                values[i] = curves[i].Evaluate(curTime - delay);
            }
        }

        public void Modify(ref Vector3 position, ref Quaternion rotation, Quaternion axisCorrection, float weightMultiplier = 1f)
        {
            float dynamicWeight = weight * weightMultiplier;
            if (blendInTime > 0)
            {
                dynamicWeight = Mathf.Lerp(0, dynamicWeight, curBlendInTime / blendInTime);
            }
            if (blendOutTime > 0)
            {
                dynamicWeight = Mathf.Lerp(dynamicWeight, 0, curBlendOutTime / blendOutTime);
            }

            if (dynamicWeight <= 0)
            {
                return;
            }

            switch (curveType)
            {
                case CurveType.Position:
                {
                    Vector3 positionValue = new Vector3(values[0], values[1], values[2]);
                    if (relative)
                    {
                        positionValue -= new Vector3(initialValues[0], initialValues[1], initialValues[2]);
                    }
                    position += axisCorrection * Vector3.Lerp(Vector3.zero, positionValue, dynamicWeight);
                    break;
                }
                case CurveType.EularAngleRaw:
                //{
                //    Vector3 eularRawValue = new Vector3(values[0], values[1], values[2]);
                //    if (relative)
                //    {
                //        eularRawValue -= new Vector3(initialValues[0], initialValues[1], initialValues[2]);
                //    }
                //    rotation *= Quaternion.Slerp(Quaternion.identity, axisCorrection * Quaternion.Euler(eularRawValue), dynamicWeight);
                //    break;
                //}
                case CurveType.EularAngleBaked:
                {
                    Quaternion eularBakedValue = axisCorrection * Quaternion.Euler(values[0], values[1], values[2]);
                    if (relative)
                    {
                        eularBakedValue = eularBakedValue * Quaternion.Inverse(axisCorrection * Quaternion.Euler(initialValues[0], initialValues[1], initialValues[2]));
                    }
                    rotation *= Quaternion.Slerp(Quaternion.identity, eularBakedValue, dynamicWeight);
                    break;
                }
                case CurveType.Quaternion:
                {
                    Quaternion rotationValue = axisCorrection * new Quaternion(values[0], values[1], values[2], values[3]);
                    if (relative)
                    {
                        rotationValue = rotationValue * Quaternion.Inverse(axisCorrection * new Quaternion(initialValues[0], initialValues[1], initialValues[2], initialValues[3]));
                    }
                    rotation *= Quaternion.Slerp(Quaternion.identity, rotationValue, dynamicWeight);
                    break;
                }
            }
        }

        public void Interrupt()
        {
            interrupted = true;
        }
    }

    [SerializeField]
    private Transform cameraOffsetTrans;
    [SerializeField]
    private float cameraAnimWeight = 1f;
    [SerializeField]
    public Quaternion axisCorrection = Quaternion.identity;
    [SerializeField]
    private string[] tags;

    private Animator animator;
    private List<CameraCurveData> list_curves = new List<CameraCurveData>();
    private static float defaultWeight = 1f;
#if NotEditor
    private class WeightHolder
    {
        public bool enabled = false;
        public float weight = 1f;
        public Dictionary<int, float> dict = new Dictionary<int, float>();

        public float GetWeightRaw(int weightTagHash)
        {
            if (dict.TryGetValue(weightTagHash, out var weight))
            {
                return weight;
            }
            return 1f;
        }

        public float GetWeight(int weightTagHash)
        {
            if (dict.TryGetValue(weightTagHash, out var weight))
            {
                return weight * this.weight;
            }
            return this.weight;
        }

        public void SetWeight(int weightTagHash, float weight)
        {
            dict[weightTagHash] = weight;
        }
    }

    private EntityPlayerLocal player;
    private WeightHolder weightHolder;
    private bool weightHolderChecked;
    private static readonly Dictionary<string, WeightHolder> dict_user_weights = new Dictionary<string, WeightHolder>();
    private static readonly string SavePath = Path.Combine(GameIO.GetUserGameDataDir(), "KFLibSettings", "CameraAnimationIntensitySettings.json");
    private static readonly string SavePathDir = Path.GetDirectoryName(SavePath);
#else
    [SerializeField]
    public Camera debugCamera;

    private Vector3 initialDebugCameraLocalPos;
    private Quaternion initialDebugCameraLocalRot;
#endif

#if NotEditor
    public static float GetUserWeight(string itemName, int weightTagHash)
    {
        if (weightTagHash != 0 && dict_user_weights.TryGetValue(itemName, out var holder) && holder != null)
        {
            return holder.GetWeight(weightTagHash);
        }

        return 1f;
    }

    public static void SetWeaponWeight(string itemName, float weight)
    {
        if (!dict_user_weights.TryGetValue(itemName, out var holder))
        {
            holder = new WeightHolder();
            dict_user_weights[itemName] = holder;
        }
        holder.weight = weight;
    }

    public static void SetUserWeight(string itemName, string weightTag, float weight)
    {
        SetUserWeight(itemName, Animator.StringToHash(weightTag), weight);
    }

    public static void SetUserWeight(string itemName, int weightTagHash, float weight)
    {
        if (!dict_user_weights.TryGetValue(itemName, out var holder))
        {
            holder = new WeightHolder();
            dict_user_weights[itemName] = holder;
        }
        holder.SetWeight(weightTagHash, weight);
    }

    public static void SetEnableUserWeight(string itemName, bool enabled)
    {
        if (!dict_user_weights.TryGetValue(itemName, out var holder))
        {
            holder = new WeightHolder();
            dict_user_weights[itemName] = holder;
        }
        holder.enabled = enabled;
    }

    public static void InitModSettings(IModGlobalSettings modSettings)
    {
        var tab = modSettings.GetTab("CameraAnimationSettings");
        var defaultWeightSetting = tab.GetCategory("Default").GetSetting("CameraAnimationIntensityMultiplier") as ISliderGlobalSetting;
        if (float.TryParse(defaultWeightSetting?.CurrentValue, out var weight))
        {
            defaultWeight = weight;
        }
        else
        {
            defaultWeight = 1f;
        }
        defaultWeightSetting.OnSettingChanged += static (settings, value) =>
        {
            if (float.TryParse(value, out var newWeight))
            {
                defaultWeight = newWeight;
            }
        };
        GearsImpl.OnGlobalSettingsSaved += SaveUserWeights;
        GearsImpl.OnGlobalSettingsOpened += CreateSettingEntries;
        GearsImpl.OnGlobalSettingsClosed += CleanupSettingEntries;
        LoadUserWeights();
    }

    private static void LoadUserWeights()
    {
        if (!Directory.Exists(SavePathDir))
        {
            Directory.CreateDirectory(SavePathDir);
        }

        if (!File.Exists(SavePath))
        {
            return;
        }

        using (StreamReader reader = File.OpenText(SavePath))
        {
            JObject saveObj = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
            foreach (JProperty itemProp in saveObj.Properties())
            {
                if (itemProp.Value is JObject itemObj)
                {
                    var itemName = itemProp.Name;
                    foreach (var valueProp in itemObj.Properties())
                    {
                        switch (valueProp.Name)
                        {
                            case "__enabled":
                                if (bool.TryParse((string)valueProp.Value, out var enabled))
                                {
                                    SetEnableUserWeight(itemName, enabled);
                                }
                                break;
                            case "__weight":
                                if (float.TryParse((string)valueProp.Value, out var weight))
                                {
                                    SetWeaponWeight(itemName, weight);
                                }
                                break;
                            default:
                                if (float.TryParse((string)valueProp.Value, out var tagWeight))
                                {
                                    int weightTagHash = Animator.StringToHash(valueProp.Name);
                                    SetUserWeight(itemName, weightTagHash, tagWeight);
                                }
                                break;
                        }
                    }
                }
            }
        }
    }

    private static void SaveUserWeights(IModGlobalSettings _)
    {
        if (!Directory.Exists(SavePathDir))
        {
            Directory.CreateDirectory(SavePathDir);
        }

        using (StreamWriter writer = File.CreateText(SavePath))
        {
            JObject saveObj = new JObject();
            foreach (var kvp in dict_user_weights)
            {
                var itemName = kvp.Key;
                var holder = kvp.Value;
                JObject itemObj = new JObject
                {
                    ["__enabled"] = holder.enabled.ToString(),
                    ["__weight"] = holder.weight.ToString()
                };
                foreach (var tagWeight in holder.dict)
                {
                    itemObj[tagWeight.Key.ToString()] = tagWeight.Value.ToString();
                }
                saveObj[itemName] = itemObj;
            }
            writer.Write(saveObj.ToString(Formatting.Indented));
        }
    }

    private static void CreateSettingEntries(IModGlobalSettings modSettings)
    {
        var tab = modSettings.GetTab("CameraAnimationSettings");
        tab.RemoveCategory("WeaponOverride");
        var player = GameManager.Instance?.World?.GetPrimaryPlayer();
        var targets = AnimationRiggingManager.GetActiveRigTargetsFromPlayer(player);
        var script = targets?.ItemAnimator?.GetComponent<CameraAnimationEvents>();
        ItemClass item = null;
        if (script)
        {
            if (script.tags == null || script.tags.Length == 0)
            {
                return;
            }
            item = player.inventory?.slots?[targets.SlotIndex]?.item;
        }
        
        if (string.IsNullOrEmpty(item?.Name))
        {
            return;
        }

        var category = tab.CreateCategory("WeaponOverride", "kflibCategoryWeaponOverrideName");

        if (!dict_user_weights.TryGetValue(item.Name, out var holder))
        {
            holder = new WeightHolder();
            dict_user_weights[item.Name] = holder;
            script.weightHolder = holder;
        }

        string itemLocalizedName = item.GetLocalizedItemName();
        var currentWeaponIndicator = category.CreateSetting<ISelectorGlobalSetting>("CurrentWeapon", "kflibSettingCurrentWeaponName");
        currentWeaponIndicator.SetAllowedValues(itemLocalizedName);
        currentWeaponIndicator.CurrentValue = itemLocalizedName;

        var enableOverride = category.CreateSetting<ISwitchGlobalSetting>("EnableOverride", "kflibSettingEnableOverrideName");
        enableOverride.TooltipKey = "kflibSettingEnableOverrideDesc";
        enableOverride.SetSwitchValues("Disabled", "Enabled");
        enableOverride.CurrentValue = holder.enabled ? "Enabled" : "Disabled";
        enableOverride.OnSettingChanged += (_, value) =>
        {
            holder.enabled = value == "Enabled";
        };

        var defaultWeight = category.CreateSetting<ISliderGlobalSetting>("DefaultWeight", "kflibSettingCAIMultiplierName");
        defaultWeight.TooltipKey = "kflibSettingDefaultWeightDesc";
        defaultWeight.SetAllowedValues(0.01f, 0f, 1f);
        defaultWeight.CurrentValue = holder.weight.ToString();
        defaultWeight.FormatterString = "0.00";
        defaultWeight.OnSettingChanged += (_, value) =>
        {
            if (float.TryParse(value, out var weight))
            {
                holder.weight = weight;
            }
        };

        foreach (var tag in script.tags)
        {
            int tagHash = Animator.StringToHash(tag);
            string descKey = $"kflibSettingCAI{tag}Name", tooltipKey = $"kflibSettingCAI{tag}Desc";
            var tagOverride = category.CreateSetting<ISliderGlobalSetting>(tag, Localization.Exists(descKey) ? descKey : tag);
            if (Localization.Exists(tooltipKey))
            {
                tagOverride.TooltipKey = tooltipKey;
            }
            tagOverride.SetAllowedValues(0.01f, 0f, 1f);
            tagOverride.CurrentValue = holder.GetWeightRaw(tagHash).ToString();
            tagOverride.FormatterString = "0.00";
            tagOverride.OnSettingChanged += (_, value) =>
            {
                if (float.TryParse(value, out var weight))
                {
                    holder.SetWeight(tagHash, weight);
                }
            };
        }
    }

    private static void CleanupSettingEntries(IModGlobalSettings modSettings)
    {
        var tab = modSettings.GetTab("CameraAnimationSettings");
        tab.RemoveCategory("WeaponOverride");
    }
#endif

    private void Awake()
    {
        animator = GetComponent<Animator>();
#if NotEditor
        if (!(player = this.GetLocalPlayerInParent()))
        {
            Destroy(this);
            return;
        }
#else
        if (debugCamera)
        {
            initialDebugCameraLocalPos = debugCamera.transform.localPosition;
            initialDebugCameraLocalRot = debugCamera.transform.localRotation;
        }
#endif
    }

    private void OnEnable()
    {
        list_curves.Clear();
    }

    private void OnDisable()
    {
        list_curves.Clear();
    }

    private void LateUpdate()
    {
        Vector3 localPos = Vector3.zero;
        Quaternion localRot = Quaternion.identity;
        if (cameraOffsetTrans)
        {
            localPos = cameraOffsetTrans.localPosition;
            localRot = cameraOffsetTrans.localRotation;
        }
#if NotEditor
        if (weightHolder == null && !weightHolderChecked)
        {
            weightHolderChecked = true;
            var targets = AnimationRiggingManager.GetActiveRigTargetsFromPlayer(player);
            if (targets == null)
            {
                Log.Warning("CameraAnimationEvents: No active rig targets found.");
            }
            else
            {
                var itemName = player.inventory?.slots?[targets.SlotIndex]?.item?.Name;
                if (!string.IsNullOrEmpty(itemName))
                {
                    dict_user_weights.TryGetValue(itemName, out weightHolder);
                }
            }
        }
#endif
        if (list_curves.Count > 0)
        {
            foreach (CameraCurveData curve in list_curves)
            {
                float userWeight = defaultWeight;
#if NotEditor
                if (weightHolder != null && weightHolder.enabled)
                {
                    userWeight = weightHolder.GetWeight(curve.weightTagHash);
                }
#endif
                curve.Update(animator, Time.deltaTime);
                curve.Modify(ref localPos, ref localRot, axisCorrection, userWeight);
            }

            for (int i = list_curves.Count - 1; i >= 0; i--)
            {
                if (list_curves[i].Finished)
                {
                    list_curves.RemoveAt(i);
                }
            }
        }

        Vector3 camPosOffset = Vector3.Lerp(Vector3.zero, localPos, cameraAnimWeight);
        Quaternion camRotOffset = Quaternion.Slerp(Quaternion.identity, localRot, cameraAnimWeight);
#if NotEditor
        CameraAnimationUpdater.SupplyCameraOffset(camPosOffset, camRotOffset);
#else
        if (debugCamera)
        {
            debugCamera.transform.localPosition = initialDebugCameraLocalPos + camPosOffset;
            debugCamera.transform.localRotation = camRotOffset * initialDebugCameraLocalRot;
        }
#endif
    }

    public void Interrupt(int fromStateHash)
    {
        foreach (var active in list_curves)
        {
            if (active.stateHash != fromStateHash)
                active.Interrupt();
        }
    }

    public void Play(CameraCurveData curveData)
    {
        list_curves.Add(curveData);
    }

    public MonoBehaviour Init(Transform playerAnimatorTrans, bool isLocalPlayer)
    {
        if (!isLocalPlayer)
        {
            return null;
        }
        var copy = playerAnimatorTrans.AddMissingComponent<CameraAnimationEvents>();
        copy.cameraAnimWeight = cameraAnimWeight;
        copy.cameraOffsetTrans = cameraOffsetTrans;
#if !NotEditor
        copy.debugCamera = debugCamera;
        copy.initialDebugCameraLocalPos = initialDebugCameraLocalPos;
        copy.initialDebugCameraLocalRot = initialDebugCameraLocalRot;
#endif
        return copy;
    }

    public void Disable(Transform playerAnimatorTrans)
    {
        enabled = false;
    }

#if UNITY_EDITOR
    public void OnBeforeSerialize()
    {
        if (EditorApplication.isUpdating)
        {
            return;
        }
        var animator = gameObject.GetComponent<Animator>()?.runtimeAnimatorController as AnimatorController;
        var list = new List<string>();
        if (animator != null)
        {
            var scripts = animator.GetBehaviours<AnimatorCameraAnimationState>();
            foreach (var script in scripts)
            {
                if (!string.IsNullOrEmpty(script.tagOverride))
                {
                    list.Add(script.tagOverride);
                    continue;
                }
                var context = AnimatorController.FindStateMachineBehaviourContext(script);
                if (context != null && context.Length > 0)
                {
                    var state = context[0].animatorObject as AnimatorState;
                    if (state != null)
                    {
                        list.Add(string.IsNullOrEmpty(state.tag) ? state.name : state.tag);
                    }
                }
            }
        }
        tags = list.Distinct().ToArray();
    }

    public void OnAfterDeserialize()
    {
    }
#endif
}
