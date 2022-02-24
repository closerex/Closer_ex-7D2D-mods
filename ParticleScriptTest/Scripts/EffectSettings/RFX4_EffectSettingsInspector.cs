#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(RFX4_EffectSettings))]
public class RFX4_EffectSettingsInspector : Editor
{

    public override void OnInspectorGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Main Parameters", EditorStyles.boldLabel);
        var script = (RFX4_EffectSettings)target;
        var isMobilePlatfrom = IsMobilePlatform();

        script.ParticlesBudget = EditorGUILayout.Slider("Particles Budget", script.ParticlesBudget, 0.1f, 1);
        if(!isMobilePlatfrom) script.UseLightShadows = EditorGUILayout.Toggle("Use Light Shadows", script.UseLightShadows);

        //if (script.GetComponentInChildren<RFX4_Decal>() != null && isMobilePlatfrom)
            script.UseFastFlatDecalsForMobiles = EditorGUILayout.Toggle("Use Fast Flat Decals for Mobiles", script.UseFastFlatDecalsForMobiles);

        script.UseCustomColor = EditorGUILayout.Toggle("Use Custom Color", script.UseCustomColor);
        if (script.UseCustomColor) script.EffectColor = EditorGUILayout.ColorField("Effect Color", script.EffectColor);
       
        script.IsVisible = EditorGUILayout.Toggle("Is Visible", script.IsVisible);
        script.FadeoutTime = EditorGUILayout.FloatField("Fadeout Time", script.FadeoutTime);

        EditorGUILayout.EndVertical();

        if (script.GetComponentInChildren<RFX4_PhysicsMotion>() != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Projectile parameters", EditorStyles.boldLabel);
            script.UseCollisionDetection = EditorGUILayout.Toggle("Use Collision Detection", script.UseCollisionDetection);
            script.LimitMaxDistance = EditorGUILayout.Toggle("Use Max Distance Limit", script.LimitMaxDistance);
            if (script.LimitMaxDistance) script.MaxDistnace = EditorGUILayout.FloatField("Max Distance", script.MaxDistnace);
            script.Mass = EditorGUILayout.FloatField("Mass", script.Mass);
            script.Speed = EditorGUILayout.FloatField("Speed", script.Speed);
            script.AirDrag = EditorGUILayout.FloatField("AirDrag", script.AirDrag);
            script.UseGravity = EditorGUILayout.Toggle("Use Gravity", script.UseGravity);
            EditorGUILayout.EndVertical();
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
        }
    }

    bool IsMobilePlatform()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
            || EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS
            || EditorUserBuildSettings.activeBuildTarget == BuildTarget.WSAPlayer) return true;

        return false;
    }
}
#endif