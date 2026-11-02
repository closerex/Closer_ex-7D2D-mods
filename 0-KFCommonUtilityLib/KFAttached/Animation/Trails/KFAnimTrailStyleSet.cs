using UnityEngine;

/// <summary>Explicit author requests, not inferred KF2 perk conditions. Source preserves each notify's own profile.</summary>
public enum KFAnimTrailStyle { Source = 0, Red = 1, Blue = 2, White = 3, Distortion = 4 }

[CreateAssetMenu(menuName = "KF2/Animation Trail Style Set")]
public sealed class KFAnimTrailStyleSet : ScriptableObject
{
    public KFAnimTrailProfile red;
    public KFAnimTrailProfile blue;
    public KFAnimTrailProfile white;
    [Tooltip("Leave missing until the source distortion profile has a verified interpretation.")]
    public KFAnimTrailProfile distortion;

    public bool TryGetProfile(KFAnimTrailStyle style, out KFAnimTrailProfile profile)
    {
        profile = null;
        switch (style)
        {
            case KFAnimTrailStyle.Red: profile = red; break;
            case KFAnimTrailStyle.Blue: profile = blue; break;
            case KFAnimTrailStyle.White: profile = white; break;
            case KFAnimTrailStyle.Distortion: profile = distortion; break;
            default: return false;
        }
        return profile != null && profile.IsValid() && profile.material.shader != null &&
               profile.material.shader.FindPropertyIndex("_UEUseColorData") >= 0;
    }
}
