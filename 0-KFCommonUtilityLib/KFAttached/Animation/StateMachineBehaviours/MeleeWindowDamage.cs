using System;

// Pure evaluator shared by the runtime and its Inspector; no serialized configuration objects.
//
// The curve describes one melee graze window. The direct-hit window is a plateau of full damage,
// symmetric about the peak and always inside the graze window, so it is never truncated by a
// window end. Between the plateau edge and a window end the multiplier ramps with a half cosine,
// which is monotone between them and has zero slope at both ends, so neither the switch to the
// direct path nor the window boundary can show a jump in damage.
//
// The ramp used to be a Gaussian normalised against the near window end, which is wrong: the
// graze region on a side can be shorter than the plateau half width, and the normalising
// denominator then changes sign and drives the envelope above 1 just outside the plateau. The
// cosine ramp has no such degeneracy.
//
// Two authored values place and size the plateau: the Raycast time fixes the peak through
// PeakOffsetForRaycast, and sigma is its half width, capped so that the plateau fits inside the
// window; the graze region on each side is whatever remains. A peak offset therefore first
// consumes one side's graze region and only then compresses the plateau, so a side can end up with
// no graze region at all -- and the window end there carries full damage rather than the trait's
// GrazeDamageMultiplier. Either width is legal, including zero.
public static class MeleeWindowDamage
{
    // Where the peak sits in the window, 0..1: the middle shifted by the offset. Both ends are
    // reachable, so a plateau of half width zero is a legal configuration rather than an edge case.
    public static float PeakPhase(float peakOffset) => Clamp01(0.5f + peakOffset);

    public static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));

    // Largest peak offset that still lets a plateau of half width sigma fit inside the window.
    // It is zero once sigma reaches half the window, because no peak can then keep a plateau that
    // wide inside it.
    public static float MaxPeakOffset(float sigma) => Math.Max(0f, 0.5f - Clamp01(sigma));

    // Domain rule for an authored peak offset: pulls it back toward the middle so a plateau of the
    // requested half width still fits. Superseded in production by MaxSigma, the same boundary on sigma.
    public static float ClampPeakOffset(float peakOffset, float sigma)
    {
        float limit = MaxPeakOffset(sigma);
        return Math.Max(-limit, Math.Min(limit, peakOffset));
    }

    // Largest plateau half width that still fits for a peak at this offset: the inverse of
    // MaxPeakOffset. Zero on either window end, because a peak there leaves no room for a plateau.
    public static float MaxSigma(float peakOffset)
    {
        float peak = PeakPhase(peakOffset);
        return Math.Min(peak, 1f - peak);
    }

    // Phase of the authored Raycast time inside the graze window, 0..1. The runtime and the Inspector
    // both use this, so the plateau can only ever sit where the impact is authored.
    public static float RaycastPhase(float raycastTime, float windowStart, float windowDuration)
        => windowDuration <= 0f ? 0.5f : Clamp01((raycastTime - windowStart) / windowDuration);

    // Peak offset that centres the plateau on the raycast. A raycast time outside the window lands on
    // the nearer end, so the curve's domain is never left.
    public static float PeakOffsetForRaycast(float raycastTime, float windowStart, float windowDuration)
        => RaycastPhase(raycastTime, windowStart, windowDuration) - 0.5f;

    // Half width of the full-damage plateau: sigma, capped so that the plateau always fits inside
    // the window. The cap is the distance to the nearer window end, so the plateau reaches (but
    // never crosses) that end; moving the peak past that point compresses the plateau instead.
    public static float DirectWindowWidth(float sigma, float peakOffset)
    {
        float u = Clamp01(sigma);
        float peak = PeakPhase(peakOffset);
        return Math.Min(u, Math.Min(peak, 1f - peak));
    }

    // Far edge of the plateau, i.e. the last phase that still counts as a direct hit.
    public static float PlateauEnd(float sigma, float peakOffset)
        => PeakPhase(peakOffset) + DirectWindowWidth(sigma, peakOffset);

    // Envelope of the curve in 0..1, with no floor applied: 0 at the ends of the graze window,
    // 1 across the direct-hit window.
    public static float Envelope(float phase, float sigma, float peakOffset)
    {
        phase = Clamp01(phase);
        float peak = PeakPhase(peakOffset);
        float u = DirectWindowWidth(sigma, peakOffset);
        float offset = phase - peak;
        float distance = Math.Abs(offset);
        if (distance <= u) return 1f;

        // Length of the graze region on the side this phase falls on. It is zero exactly when the
        // plateau has consumed that side, and then the early return above has already matched every
        // phase on this side (distance <= u holds for the whole side), so the ramp never divides by
        // it. Where the graze region is positive the window end carries the floor; where it is zero
        // the window end sits on the plateau and carries full damage.
        float side = offset < 0f ? peak - u : 1f - peak - u;

        return 0.5f - 0.5f * (float)Math.Cos(Math.PI * (side - (distance - u)) / side);
    }

    // Whether the current position inside the graze window is a direct hit. This is exactly the
    // plateau of the envelope, so it is also where GrazeMultiplier returns full damage.
    public static bool InDirectWindow(float phase, float sigma, float peakOffset)
        => Math.Abs(Clamp01(phase) - PeakPhase(peakOffset)) <= DirectWindowWidth(sigma, peakOffset);

    // Fallback for the discrete sampling of the window: once the plateau is behind this phase and
    // no direct cast has happened yet, this tick takes the direct path. The runtime also runs one
    // tick past the window end, whose phase exceeds any plateau end, so the rule cannot be missed.
    public static bool MustForceDirectHit(float phase, float plateauEnd, bool directHitDone)
        => !directHitDone && phase > plateauEnd;

    // Damage multiplier for a grazing hit: the envelope interpolated between the trait's
    // GrazeDamageMultiplier at the ends of the window and 1 at the edge of the direct-hit window,
    // so the trait value is the floor and a hit at the plateau deals full damage.
    public static float GrazeMultiplier(float phase, float floor, float sigma, float peakOffset)
    {
        floor = Clamp01(floor);
        return floor + (1f - floor) * Envelope(phase, sigma, peakOffset);
    }
}
