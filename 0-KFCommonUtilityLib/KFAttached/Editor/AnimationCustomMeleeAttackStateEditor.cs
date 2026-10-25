#if UNITY_EDITOR && !NotEditor
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

[CustomEditor(typeof(AnimationCustomMeleeAttackState))]
public sealed class AnimationCustomMeleeAttackStateEditor : Editor
{
    // Preview-only state. It is deliberately not serialized into animation assets.
    private static float previewFloor = 0.5f;
    private static bool showLegacyScheduling;
    private const string LanguagePreferenceKey = "KFCommonUtilityLib.MeleeInspector.UseEnglish";
    private static readonly string[] LanguageNames = { "中文", "English" };
    private static bool useEnglish;

    private static string L(string chinese, string english) => useEnglish ? english : chinese;

    private void DrawLanguage()
    {
        int selected = EditorGUILayout.Popup("Language / 语言", useEnglish ? 1 : 0, LanguageNames);
        if (selected != (useEnglish ? 1 : 0))
        {
            useEnglish = selected == 1;
            SessionState.SetBool(LanguagePreferenceKey, useEnglish);
            Repaint();
        }
        EditorGUILayout.Space();
    }

    private SerializedProperty globalDamageMultiplier;
    private SerializedProperty useWindowDamage;
    private SerializedProperty windowDamageSigma;
    private SerializedProperty raycastTime;
    private SerializedProperty raycastTimes;
    private bool legacyRaycastTimesChanged;
    private SerializedProperty grazeAsRaycast;
    private SerializedProperty useRaycast;
    private SerializedProperty useGraze;
    private SerializedProperty customGrazeCastTime;
    private SerializedProperty customGrazeCastDuration;
    private SerializedProperty customGrazeCastInterval;
    private SerializedProperty grazeCanHitSameTarget;
    private SerializedProperty impactDuration;
    private SerializedProperty impactPlaybackSpeed;
    private SerializedProperty attackDurationNormalized;
    private SerializedProperty swingAngle;
    private SerializedProperty swingDegrees;
    private SerializedProperty forceActionIndex;
    private SerializedProperty isAlternative;
    private SerializedProperty invariableRpm;
    private SerializedProperty cancelLoopOnRelease;
    private SerializedProperty clipLength;

    private void OnEnable()
    {
        useEnglish = SessionState.GetBool(
            LanguagePreferenceKey, !Application.systemLanguage.ToString().StartsWith("Chinese"));
        globalDamageMultiplier = Find("GlobalDamageMultiplier");
        useWindowDamage = Find("UseWindowDamage");
        windowDamageSigma = Find("WindowDamageSigma");
        raycastTime = Find("RaycastTime");
        raycastTimes = Find("RaycastTimes");
        grazeAsRaycast = Find("GrazeAsRaycast");
        useRaycast = Find("UseRaycast");
        useGraze = Find("UseGraze");
        customGrazeCastTime = Find("CustomGrazeCastTime");
        customGrazeCastDuration = Find("CustomGrazeCastDuration");
        customGrazeCastInterval = Find("CustomGrazeCastInterval");
        grazeCanHitSameTarget = Find("GrazeCanHitSameTarget");
        impactDuration = Find("ImpactDuration");
        impactPlaybackSpeed = Find("ImpactPlaybackSpeed");
        attackDurationNormalized = Find("attackDurationNormalized");
        swingAngle = Find("SwingAngle");
        swingDegrees = Find("SwingDegrees");
        forceActionIndex = Find("ForceActionIndex");
        isAlternative = Find("IsAlternative");
        invariableRpm = Find("InvariableRPM");
        cancelLoopOnRelease = Find("CancelLoopOnRelease");
        clipLength = Find("ClipLength");
        RefreshSourceClipLengths();
        if (NormalizeLegacyRaycastTimes())
        {
            serializedObject.ApplyModifiedProperties();
        }
    }

    public override void OnInspectorGUI()
    {
        DrawLanguage();
        serializedObject.UpdateIfRequiredOrScript();
        legacyRaycastTimesChanged = false;
        EditorGUI.BeginChangeCheck();

        DrawMode();
        EditorGUILayout.Space();
        DrawSharedSettings();

        if (EditorGUI.EndChangeCheck() || legacyRaycastTimesChanged)
        {
            serializedObject.ApplyModifiedProperties();
            if (useWindowDamage != null && useWindowDamage.boolValue)
            {
                NormalizeWindowFields();
            }
        }
    }

    private void DrawMode()
    {
        EditorGUILayout.LabelField(L("近战命中模式", "Melee hit mode"), EditorStyles.boldLabel);
        if (useWindowDamage == null)
        {
            EditorGUILayout.HelpBox(L("当前脚本尚未包含时间窗口伤害字段。请同步 AnimationCustomMeleeAttackState 的新版字段后再编辑。", "This script does not contain the window damage fields yet. Sync the updated AnimationCustomMeleeAttackState before editing."), MessageType.Warning);
            DrawLegacyMode();
            return;
        }

        EditorGUILayout.HelpBox(L("选择原有模式可保持旧资产的命中调度不变；时间窗口模式用于有限时长的普通扇扫。", "Legacy mode preserves the hit timing of existing assets. Window damage is for ordinary sweeps with a finite duration."), MessageType.Info);
        Property(useWindowDamage, L("使用时间窗口伤害（新）", "Use window damage (new)"));

        if (useWindowDamage.hasMultipleDifferentValues)
        {
            EditorGUILayout.HelpBox(L("多选对象的模式不同。修改上方开关后可统一为同一种模式。", "The selected objects use different modes. Change the toggle above to apply one mode to all selected objects."), MessageType.Info);
            return;
        }

        if (useWindowDamage.boolValue)
        {
            DrawWindowDamageMode();
        }
        else
        {
            DrawLegacyMode();
        }
    }

    private void DrawLegacyMode()
    {
        legacyRaycastTimesChanged |= NormalizeLegacyRaycastTimes();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(L("原有模式", "Legacy mode"), EditorStyles.boldLabel);
        Property(useRaycast, L("使用 Raycast", "Use Raycast"));
        if (useRaycast != null && useRaycast.boolValue)
        {
            EditorGUI.indentLevel++;
            if (grazeAsRaycast != null && grazeAsRaycast.boolValue)
            {
                EditorGUILayout.HelpBox(L("Graze 作为 Raycast 开启时仍由 Graze 窗口调度，不使用 Raycast 时间序列。", "Graze as Raycast uses the graze window, not the Raycast time sequence."), MessageType.Info);
            }
            else
            {
                DrawLegacyRaycastTimes();
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        Property(useGraze, L("使用 Graze", "Use Graze"));
        if (useGraze != null && useGraze.boolValue)
        {
            EditorGUI.indentLevel++;
            Property(grazeAsRaycast, L("Graze 作为 Raycast", "Graze as Raycast"));
            EditorGUILayout.LabelField(L("Graze 时间窗口", "Graze time window"), EditorStyles.boldLabel);
            Property(customGrazeCastTime, L("窗口起点", "Window start"));
            Property(customGrazeCastDuration, L("持续时间", "Duration"));
            Property(customGrazeCastInterval, L("采样间隔", "Sample interval"));
            Property(grazeCanHitSameTarget, L("允许重复命中同一目标", "Allow repeat hits on the same target"));
            EditorGUI.indentLevel--;
        }
        else if (grazeAsRaycast != null && grazeAsRaycast.boolValue)
        {
            EditorGUILayout.HelpBox(L("Graze 作为 Raycast 仍为开启，原版 Graze 调度仍会执行。勾选“使用 Graze”后可修改该设置。", "Graze as Raycast is still enabled, so legacy graze scheduling remains active. Enable Use Graze to edit that setting."), MessageType.Info);
        }

        EditorGUILayout.Space();
        DrawSweepSettings();
    }

    private bool NormalizeLegacyRaycastTimes()
    {
        if (raycastTimes == null || raycastTime == null
            || (useWindowDamage != null && useWindowDamage.boolValue)) return false;
        bool changed = false;
        if (raycastTimes.arraySize == 0)
        {
            raycastTimes.arraySize = 1;
            raycastTimes.GetArrayElementAtIndex(0).floatValue = raycastTime.floatValue;
            changed = true;
        }
        float previous = raycastTimes.GetArrayElementAtIndex(0).floatValue;
        for (int i = 1; i < raycastTimes.arraySize; i++)
        {
            var time = raycastTimes.GetArrayElementAtIndex(i);
            if (time.floatValue < previous)
            {
                time.floatValue = previous;
                changed = true;
            }
            previous = time.floatValue;
        }
        return changed;
    }

    private void AddLegacyRaycastTime()
    {
        int index = raycastTimes.arraySize;
        float time = index > 0 ? raycastTimes.GetArrayElementAtIndex(index - 1).floatValue : raycastTime.floatValue;
        raycastTimes.arraySize++;
        raycastTimes.GetArrayElementAtIndex(index).floatValue = time;
        legacyRaycastTimesChanged = true;
    }

    private void DrawLegacyRaycastTimes()
    {
        EditorGUILayout.LabelField(L("触发时间序列（秒）", "Cast times (seconds)"), EditorStyles.boldLabel);
        for (int i = 0; i < raycastTimes.arraySize; i++)
        {
            var element = raycastTimes.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();
            float time = EditorGUILayout.FloatField(new GUIContent(
                L($"第 {i + 1} 次", $"Cast {i + 1}"),
                L("相对攻击开始，按攻速缩放；不得早于上一项。相同时间依次执行，不等待命中停顿。", "Offsets from attack start, scaled by attack speed. Each time must be at least the previous one. Equal times run serially without waiting for hit-stop.")), element.floatValue);
            if (i > 0)
            {
                time = Mathf.Max(time, raycastTimes.GetArrayElementAtIndex(i - 1).floatValue);
            }
            if (time != element.floatValue)
            {
                element.floatValue = time;
                legacyRaycastTimesChanged = true;
            }
            bool remove;
            using (new EditorGUI.DisabledScope(raycastTimes.arraySize <= 1))
            {
                remove = GUILayout.Button("-", GUILayout.Width(24f));
            }
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                raycastTimes.DeleteArrayElementAtIndex(i);
                legacyRaycastTimesChanged = true;
                break;
            }
        }
        if (GUILayout.Button(L("添加 Raycast", "Add Raycast")))
        {
            AddLegacyRaycastTime();
        }
    }

    private void DrawWindowDamageMode()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(L("时间窗口模式（新）", "Window damage mode (new)"), EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(L("仅用于有限时长的普通扇扫。窗口内倍率按半余弦从 xml 擦伤倍率升到 1，峰值附近是满伤平台，平台中心由 Raycast 时间决定；平台内所有命中都算直击。", "For ordinary sweeps with a finite duration. The multiplier ramps with a half cosine from the xml graze multiplier up to 1, full damage is the plateau around the peak, and the Raycast time decides where that plateau sits. Every hit inside it counts as a direct hit."), MessageType.Info);

        bool invalidDuration = customGrazeCastDuration != null && customGrazeCastDuration.floatValue <= 0f;
        bool invalidNormalizedDuration = attackDurationNormalized != null && attackDurationNormalized.floatValue <= 0f;
        bool usesRaycastGraze = grazeAsRaycast != null && grazeAsRaycast.boolValue;
        if (invalidDuration || invalidNormalizedDuration || usesRaycastGraze)
        {
            EditorGUILayout.HelpBox(L("此组合属于特殊攻击，请使用原有模式。时间窗口模式要求：持续时间 > 0、结束比例 > 0，且 Graze 作为 Raycast 为关闭。若仍启用新模式，运行时会明确警告并回退到原有模式；不会取消整次攻击。", "This combination requires legacy mode. Window damage needs Duration > 0, End ratio > 0, and Graze as Raycast disabled. If enabled with this combination, it logs a warning and falls back to legacy mode; the attack is not cancelled."), MessageType.Warning);
        }

        EditorGUILayout.LabelField(L("窗口与采样", "Window and sampling"), EditorStyles.boldLabel);
        Property(customGrazeCastTime, L("窗口起点", "Window start"));
        Property(customGrazeCastDuration, L("持续时间", "Duration"));
        Property(customGrazeCastInterval, L("采样间隔", "Sample interval"));
        Property(raycastTime, L("Raycast 时间（决定直击平台位置）", "Raycast time (places the full-damage plateau)"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(L("伤害曲线", "Damage curve"), EditorStyles.boldLabel);
        Property(windowDamageSigma, L("Sigma（直击平台半宽）", "Sigma (full-damage plateau half width)"));
        DrawWindowPreview();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(L("去重与扇扫", "Hit limits and sweep"), EditorStyles.boldLabel);
        Property(grazeCanHitSameTarget, L("允许重复命中同一目标", "Allow repeat hits on the same target"));
        DrawSweepSettings();

        EditorGUILayout.Space();
        showLegacyScheduling = EditorGUILayout.Foldout(showLegacyScheduling, L("旧调度参数（不参与新模式、保留原值）", "Legacy timing (inactive in new mode; values preserved)"), true);
        if (showLegacyScheduling)
        {
            EditorGUI.indentLevel++;
            Property(useRaycast, L("使用 Raycast", "Use Raycast"));
            Property(useGraze, L("使用 Graze", "Use Graze"));
            Property(grazeAsRaycast, L("Graze 作为 Raycast", "Graze as Raycast"));
            EditorGUI.indentLevel--;
        }
    }

    private void DrawSharedSettings()
    {
        EditorGUILayout.LabelField(L("共用设置", "Shared settings"), EditorStyles.boldLabel);
        Property(globalDamageMultiplier, L("全局伤害倍率", "Global damage multiplier"));
        EditorGUILayout.HelpBox(L("由 DynamicGraze 模块应用于 Raycast 与 GrazeCast 的实体、方块伤害；保留原版后续加成与最低伤害。窗口曲线预览不包含此倍率。", "Applied by DynamicGraze to Raycast and GrazeCast entity/block damage. Vanilla downstream modifiers and minimum damage remain; the window preview excludes this scale."), MessageType.None);
        Property(forceActionIndex, L("动作索引（-1 为当前动作）", "Action index (-1 = current action)"));
        Property(isAlternative, L("替代动作", "Alternative action"));
        Property(invariableRpm, L("恒定攻速", "Fixed attack speed"));
        Property(attackDurationNormalized, L("结束比例", "End ratio"));
        Property(impactDuration, L("命中停顿时长", "Hit-stop duration"));
        Property(impactPlaybackSpeed, L("命中停顿播放速度", "Hit-stop playback speed"));

        using (new EditorGUI.DisabledScope(true))
        {
            Property(clipLength, L("Clip 长度（只读）", "Clip length (read-only)"));
        }

        using (new EditorGUI.DisabledScope(!MeleeClipLengthUpdater.IsSourceController(AssetDatabase.GetAssetPath(target))))
        {
            if (GUILayout.Button(L("从源控制器刷新 Clip 长度", "Refresh clip length from source controller")))
            {
                RefreshSourceClipLengths();
            }
        }

        Property(cancelLoopOnRelease, L("松开时取消循环", "Cancel loop on release"));
        EditorGUILayout.HelpBox(L("CancelLoopOnRelease 目前仅为声明字段；实际循环退出仍依赖现有运行时脚本，本 Inspector 不改变该行为。", "CancelLoopOnRelease is currently a declared field only. Loop exit still depends on the existing runtime logic; this Inspector does not change that behavior."), MessageType.Info);
    }

    private void DrawSweepSettings()
    {
        EditorGUILayout.LabelField(L("扫角", "Sweep angles"), EditorStyles.boldLabel);
        Property(swingAngle, L("扇扫方向（Swing Angle）", "Sweep orientation (Swing Angle)"),
            L("按相机画面、正扫幅：0°从6点扫到12点；方向角逆时针增加，90°为3→9点，180°为12→6点，270°为9→3点。",
                "In the camera view with a positive span: 0° sweeps 6 to 12 o'clock. Increase the angle counterclockwise: 90° is 3 to 9, 180° is 12 to 6, and 270° is 9 to 3."));
        Property(swingDegrees, L("扇扫幅度（Swing Degrees）", "Sweep span (Swing Degrees)"),
            L("以准星为中点的总扫幅，两端各占一半角度。正值按所设方向扫，负值反向；0°不作角度扫动。",
                "Total arc centered on the crosshair, with half on each side. Positive follows the chosen orientation; negative reverses it. 0° gives no angular sweep."));
    }

    private void DrawWindowPreview()
    {
        EditorGUILayout.Space();
        previewFloor = EditorGUILayout.Slider(new GUIContent(L("预览下限（仅显示）", "Preview floor (display only)"), L("不保存到资产。实际下限由游戏 GrazeDamageMultiplier passive 决定。", "Not saved to the asset. The actual floor comes from the game's GrazeDamageMultiplier passive effect.")), previewFloor, 0f, 1f);
        EditorGUILayout.HelpBox(L("预览下限只用于显示；实际下限来自游戏 GrazeDamageMultiplier passive。它是每段擦伤区外端的倍率，曲线由它平滑升到 1。若 Raycast 时间靠近窗口端点、某一侧的擦伤区被直击平台吃掉，该端点的倍率就是 1 而不是它。", "The preview floor is display-only; the actual floor comes from GrazeDamageMultiplier. It is the multiplier at the outer end of each graze region, and the curve rises smoothly from it to 1. Where a Raycast time near a window end has let the plateau consume a side, that window end is full damage instead."), MessageType.None);

        float sigma = windowDamageSigma != null ? windowDamageSigma.floatValue : 0.2f;
        // The preview reads the same mapping the runtime uses, so the drawn plateau is the real one.
        float peakOffset = MeleeWindowDamage.PeakOffsetForRaycast(
            raycastTime != null ? raycastTime.floatValue : 0f,
            customGrazeCastTime != null ? customGrazeCastTime.floatValue : 0f,
            customGrazeCastDuration != null ? customGrazeCastDuration.floatValue : 0f);
        float directWindow = MeleeWindowDamage.DirectWindowWidth(sigma, peakOffset);
        float peak = MeleeWindowDamage.PeakPhase(peakOffset);
        Rect rect = GUILayoutUtility.GetRect(10f, 130f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.88f, 0.88f, 0.88f));
        DrawPreviewGrid(rect, peak, directWindow);

        Handles.BeginGUI();
        Color oldColor = Handles.color;
        Handles.color = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.75f, 1f) : new Color(0.05f, 0.35f, 0.75f);
        Vector3 previous = Vector3.zero;
        for (int i = 0; i <= 80; i++)
        {
            float x = i / 80f;
            // Plot the multiplier that actually applies, so the plateau reads as full damage and
            // the tails read as the graze floor rather than as an abstract 0..1 envelope.
            float y = MeleeWindowDamage.GrazeMultiplier(x, previewFloor, sigma, peakOffset);
            Vector3 point = new Vector3(Mathf.Lerp(rect.x, rect.xMax, x), Mathf.Lerp(rect.yMax - 6f, rect.y + 6f, y), 0f);
            if (i > 0)
            {
                Handles.DrawLine(previous, point);
            }
            previous = point;
        }
        Handles.color = oldColor;
        Handles.EndGUI();

        float low = Mathf.Clamp01(peak - directWindow);
        float high = Mathf.Clamp01(peak + directWindow);
        EditorGUILayout.LabelField(L($"峰值相位（= Raycast 相位）：{peak:0.00}　直击平台：{low:0.00} – {high:0.00}（完整直击，约 {(high - low) * 100f:0}% 窗口）\n擦伤区间：其余相位，倍率由 {previewFloor:0.00} 平滑升到 1；端点已落在平台上时该端为满伤",
            $"Peak phase (= Raycast phase): {peak:0.00}   Full-damage plateau: {low:0.00} - {high:0.00} (~{(high - low) * 100f:0}% of the window)\nGraze: the rest of the window, rising smoothly from {previewFloor:0.00} to 1 (a window end already on the plateau is full damage)"), EditorStyles.centeredGreyMiniLabel);
    }

    private static void DrawPreviewGrid(Rect rect, float peak, float directWindow)
    {
        Color line = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.22f) : new Color(0f, 0f, 0f, 0.2f);

        // Shade the direct-hit window so the offset is visible against the curve.
        Color band = EditorGUIUtility.isProSkin ? new Color(1f, 0.85f, 0.3f, 0.13f) : new Color(0.6f, 0.45f, 0f, 0.16f);
        float low = Mathf.Clamp01(peak - directWindow);
        float high = Mathf.Clamp01(peak + directWindow);
        EditorGUI.DrawRect(new Rect(Mathf.Lerp(rect.x, rect.xMax, low), rect.y, Mathf.Lerp(rect.x, rect.xMax, high) - Mathf.Lerp(rect.x, rect.xMax, low), rect.height), band);

        float peakX = Mathf.Lerp(rect.x, rect.xMax, peak);
        EditorGUI.DrawRect(new Rect(peakX, rect.y, 1f, rect.height), line);
        GUI.Label(new Rect(rect.x + 4f, rect.y + 3f, 160f, 16f), L("峰值 1", "Peak 1"), EditorStyles.miniLabel);
        float labelX = Mathf.Clamp(peakX + 3f, rect.x + 4f, rect.xMax - 120f);
        GUI.Label(new Rect(labelX, rect.yMax - 18f, 120f, 16f), L("直击窗口", "Direct window"), EditorStyles.miniLabel);
    }

    // Keeps the mode's two authored values inside the curve's domain: the Raycast time is pulled into
    // the graze window and sigma is capped to the plateau that fits the peak it lands on.
    private void NormalizeWindowFields()
    {
        bool usable = customGrazeCastDuration != null && customGrazeCastDuration.floatValue > 0f
                   && attackDurationNormalized != null && attackDurationNormalized.floatValue > 0f
                   && grazeAsRaycast != null && !grazeAsRaycast.boolValue;
        if (!usable || raycastTime == null || windowDamageSigma == null) { return; }

        Object[] selected = targets;
        for (int i = 0; i < selected.Length; i++)
        {
            AnimationCustomMeleeAttackState state = selected[i] as AnimationCustomMeleeAttackState;
            if (state == null) { continue; }

            SerializedObject stateObject = new SerializedObject(state);
            SerializedProperty timeProperty = stateObject.FindProperty("RaycastTime");
            SerializedProperty sigmaProperty = stateObject.FindProperty("WindowDamageSigma");
            SerializedProperty startProperty = stateObject.FindProperty("CustomGrazeCastTime");
            SerializedProperty durationProperty = stateObject.FindProperty("CustomGrazeCastDuration");
            if (timeProperty == null || sigmaProperty == null || startProperty == null || durationProperty == null) { continue; }

            float start = startProperty.floatValue;
            float duration = durationProperty.floatValue;
            float time = Mathf.Clamp(timeProperty.floatValue, start, start + duration);
            float peakOffset = MeleeWindowDamage.PeakOffsetForRaycast(time, start, duration);
            float sigma = Mathf.Clamp(sigmaProperty.floatValue, 0f, MeleeWindowDamage.MaxSigma(peakOffset));
            if (Mathf.Approximately(time, timeProperty.floatValue)
                && Mathf.Approximately(sigma, sigmaProperty.floatValue)) { continue; }

            Undo.RecordObject(state, "Normalize window damage fields");
            timeProperty.floatValue = time;
            sigmaProperty.floatValue = sigma;
            stateObject.ApplyModifiedProperties();
        }

        // Re-read so the drawn fields reflect what was just written back.
        serializedObject.UpdateIfRequiredOrScript();
        Repaint();
    }

    private void RefreshSourceClipLengths()
    {
        var paths = new HashSet<string>();
        foreach (Object selected in targets)
        {
            string path = AssetDatabase.GetAssetPath(selected);
            if (MeleeClipLengthUpdater.IsSourceController(path)) paths.Add(path);
        }
        if (paths.Count > 0) MeleeClipLengthUpdater.RefreshControllers(paths, false, true);
        serializedObject.UpdateIfRequiredOrScript();
    }

    private SerializedProperty Find(string name)
    {
        return serializedObject.FindProperty(name);
    }

    private static void Property(SerializedProperty property, string label, string tooltip = "")
    {
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
        }
    }
}

// Source-controller traversal only; serialization and runtime/bundle objects never query Animator context.
public sealed class MeleeClipLengthUpdater : AssetModificationProcessor
{
    public static bool IsSourceController(string path)
    {
        return !string.IsNullOrEmpty(path) && path.EndsWith(".controller", StringComparison.OrdinalIgnoreCase)
            && AssetDatabase.LoadMainAssetAtPath(path) is AnimatorController;
    }

    public static int RefreshForAssets(string[] paths)
    {
        var controllers = new HashSet<string>();
        foreach (string path in AssetDatabase.GetDependencies(paths, true))
        {
            if (IsSourceController(path)) controllers.Add(path);
        }
        return RefreshControllers(controllers, true);
    }

    public static int RefreshControllers(IEnumerable<string> paths, bool save = false, bool undo = false)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer
            || AssetDatabase.IsAssetImportWorkerProcess())
            throw new InvalidOperationException("ClipLength 更新需要稳定的 Edit Mode 源控制器。");

        var lengths = new Dictionary<AnimationCustomMeleeAttackState, float>();
        var owners = new Dictionary<AnimationCustomMeleeAttackState, AnimatorController>();
        foreach (string path in new HashSet<string>(paths))
        {
            if (!IsSourceController(path) || !AssetDatabase.IsOpenForEdit(path))
                throw new InvalidOperationException("不能更新非可编辑源控制器：" + path);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            foreach (var layer in controller.layers)
            {
                var machine = layer.syncedLayerIndex >= 0
                    ? controller.layers[layer.syncedLayerIndex].stateMachine : layer.stateMachine;
                Gather(machine, controller, path, lengths, owners, layer.syncedLayerIndex >= 0);
            }
        }

        // Validate every context before writing, so a conflicting shared behaviour cannot half-update assets.
        var changed = new HashSet<AnimatorController>();
        int count = 0;
        foreach (var entry in lengths)
        {
            var serialized = new SerializedObject(entry.Key);
            var property = serialized.FindProperty("ClipLength");
            if (Mathf.Approximately(property.floatValue, entry.Value)) continue;
            if (undo) Undo.RecordObject(entry.Key, "Refresh melee clip length");
            property.floatValue = entry.Value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(entry.Key);
            changed.Add(owners[entry.Key]);
            count++;
        }
        foreach (var controller in changed)
        {
            EditorUtility.SetDirty(controller);
            if (save) AssetDatabase.SaveAssetIfDirty(controller);
        }
        return count;
    }

    private static void Gather(AnimatorStateMachine machine, AnimatorController owner, string path,
        Dictionary<AnimationCustomMeleeAttackState, float> lengths,
        Dictionary<AnimationCustomMeleeAttackState, AnimatorController> owners, bool synced)
    {
        foreach (var child in machine.states)
        {
            foreach (var behaviour in child.state.behaviours)
            {
                if (!(behaviour is AnimationCustomMeleeAttackState melee)) continue;
                if (synced) throw new InvalidOperationException("ClipLength 尚不支持近战同步层：" + path);
                if (AssetDatabase.GetAssetPath(melee) != path
                    || !(child.state.motion is AnimationClip clip) || clip.length <= 0f
                    || float.IsNaN(clip.length) || float.IsInfinity(clip.length))
                    throw new InvalidOperationException("无有效源 ClipLength：" + path + " / " + child.state.name);
                if (lengths.TryGetValue(melee, out float length) && !Mathf.Approximately(length, clip.length))
                    throw new InvalidOperationException("共享近战 Behaviour 的 ClipLength 冲突：" + path + " / " + child.state.name);
                lengths[melee] = clip.length;
                owners[melee] = owner;
            }
        }
        foreach (var child in machine.stateMachines) Gather(child.stateMachine, owner, path, lengths, owners, synced);
    }

    private static string[] OnWillSaveAssets(string[] paths)
    {
        var controllers = new List<string>();
        foreach (string path in paths)
        {
            if (IsSourceController(path)) controllers.Add(path);
        }
        if (controllers.Count > 0) RefreshControllers(controllers);
        return paths;
    }
}
#endif
