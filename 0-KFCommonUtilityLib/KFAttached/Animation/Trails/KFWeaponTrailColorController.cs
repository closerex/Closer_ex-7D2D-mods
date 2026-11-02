using UnityEngine;

/// <summary>Root adapter for the existing MinEventActionSetWeaponLabelColor / NetPackageSyncWeaponLabelColor route.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("KFAttachments/Weapon Display Controllers/KF Trail Color Controller")]
public sealed class KFWeaponTrailColorController : WeaponColorControllerBase
{
    public KFAnimTrailPlayer[] players;
    public int trailRendererSlot;
    public int trailMaterialSlot;
    public string colorProperty = "_TrailColor";
    public string resetProperty = "_TrailColorReset";
    [Tooltip("The existing HTML-color MinEventAction provides sRGB RGB and linear alpha.")]
    public bool inputIsSrgb = true;
    [Tooltip("Optional existing controller to handle all non-trail slots; do not add a second root base controller.")]
    public WeaponColorControllerBase fallback;

    private static bool TryStyleCommand(int nameId, out KFAnimTrailStyle style)
    {
        style = KFAnimTrailStyle.Source;
        if (nameId == Shader.PropertyToID("_TrailStyleReset")) return true;
        if (nameId == Shader.PropertyToID("_TrailStyleRed")) { style = KFAnimTrailStyle.Red; return true; }
        if (nameId == Shader.PropertyToID("_TrailStyleBlue")) { style = KFAnimTrailStyle.Blue; return true; }
        if (nameId == Shader.PropertyToID("_TrailStyleWhite")) { style = KFAnimTrailStyle.White; return true; }
        if (nameId == Shader.PropertyToID("_TrailStyleDistortion")) { style = KFAnimTrailStyle.Distortion; return true; }
        return false;
    }

    public override bool setMaterialColor(int renderer_index, int material_index, int nameId, Color data)
    {
        KFAnimTrailStyle style;
        if (renderer_index == trailRendererSlot && material_index == trailMaterialSlot && TryStyleCommand(nameId, out style))
        {
            // Unlike the legacy color route, style changes are all-or-nothing, including missing array members.
            if (players == null || players.Length == 0) return false;
            foreach (var player in players) if (player == null || !player.CanSetTrailStyle(style)) return false;
            foreach (var player in players) player.SetTrailStyle(style);
            return true;
        }
        bool reset = nameId == Shader.PropertyToID(resetProperty);
        if (renderer_index != trailRendererSlot || material_index != trailMaterialSlot || (!reset && nameId != Shader.PropertyToID(colorProperty)))
            return fallback != null && fallback != this && fallback.setMaterialColor(renderer_index, material_index, nameId, data);
        if (players == null || players.Length == 0) return false;
        if (reset)
        {
            bool restored = false;
            foreach (var player in players) if (player != null) { player.ResetTrailColor(); restored = true; }
            return restored;
        }
        if (!KFAnimTrailMesh.Finite(data)) return false;
        Color linear = inputIsSrgb ? new Color(Mathf.GammaToLinearSpace(data.r), Mathf.GammaToLinearSpace(data.g), Mathf.GammaToLinearSpace(data.b), data.a) : data;
        bool accepted = false;
        foreach (var player in players) if (player != null) accepted |= player.SetTrailColor(linear);
        return accepted;
    }
}
