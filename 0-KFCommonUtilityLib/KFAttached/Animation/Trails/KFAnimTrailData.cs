using System;
using System.IO;
using UnityEngine;

/// <summary>Imported notify windows and three socket tracks in the bound skeleton root's local space.</summary>
[CreateAssetMenu(menuName = "KF2/Animation Trail Data")]
public sealed class KFAnimTrailData : ScriptableObject
{
    public KFAnimTrailClip[] trails = Array.Empty<KFAnimTrailClip>();
    [Tooltip("Optional explicit style alternatives; never required for Source/default notifications.")]
    public KFAnimTrailStyleSet styleSet;

    [SerializeField, HideInInspector] private byte[] runtimePayload;
    [SerializeField, HideInInspector] private AnimationClip[] runtimeClips;
    [SerializeField, HideInInspector] private Material[] runtimeMaterials;
    [NonSerialized] private bool runtimeDataAttempted;

    private const int RuntimePayloadMagic = 0x4B464154;
    private const int RuntimePayloadVersion = 1;

    public void PrepareRuntimePayload()
    {
        KFAnimTrailClip[] source = trails;
        int count = source == null ? 0 : source.Length;
        AnimationClip[] clips = source == null ? null : new AnimationClip[count];
        Material[] materials = source == null ? null : new Material[count];
        byte[] payload;

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(RuntimePayloadMagic);
            writer.Write(RuntimePayloadVersion);
            writer.Write(source != null);
            writer.Write(count);

            for (int i = 0; i < count; i++)
            {
                KFAnimTrailClip trail = source[i];
                writer.Write(trail != null);
                if (trail == null) continue;

                WriteNullableString(writer, trail.sourceNotify);
                writer.Write(trail.startTime);
                writer.Write(trail.endTime);
                WriteProfile(writer, trail.profile);
                WriteNullableString(writer, trail.firstSocketPath);
                WriteNullableString(writer, trail.controlSocketPath);
                WriteNullableString(writer, trail.secondSocketPath);
                WriteSamples(writer, trail.samples);
                WriteSamples(writer, trail.referenceSamples);

                clips[i] = trail.clip;
                materials[i] = trail.profile == null ? null : trail.profile.material;
            }

            writer.Flush();
            payload = stream.ToArray();
        }

        runtimeClips = clips;
        runtimeMaterials = materials;
        runtimePayload = payload;
        runtimeDataAttempted = false;
    }

    public void EnsureRuntimeData()
    {
        if (runtimeDataAttempted || (trails != null && trails.Length > 0)) return;
        if (runtimePayload == null || runtimePayload.Length == 0) return;

        KFAnimTrailClip[] decoded = ReadRuntimePayload(runtimePayload, runtimeClips, runtimeMaterials);
        trails = decoded;
        runtimeDataAttempted = true;
    }

    private void OnEnable()
    {
        EnsureRuntimeData();
    }

    private static KFAnimTrailClip[] ReadRuntimePayload(byte[] payload, AnimationClip[] clips, Material[] materials)
    {
        using (var stream = new MemoryStream(payload, false))
        using (var reader = new BinaryReader(stream))
        {
            if (reader.ReadInt32() != RuntimePayloadMagic)
                throw new InvalidDataException("Invalid KFAnimTrailData payload magic.");
            if (reader.ReadInt32() != RuntimePayloadVersion)
                throw new InvalidDataException("Unsupported KFAnimTrailData payload version.");

            bool hasTrailsArray = reader.ReadBoolean();
            int count = ReadCount(reader, stream, 1);
            if (!hasTrailsArray && count != 0)
                throw new InvalidDataException("A null trails array cannot contain entries.");
            if (hasTrailsArray)
            {
                if (clips == null || clips.Length != count || materials == null || materials.Length != count)
                    throw new InvalidDataException("KFAnimTrailData reference array count mismatch.");
            }
            else if (clips != null || materials != null)
            {
                throw new InvalidDataException("KFAnimTrailData reference arrays do not match the null trails array.");
            }

            KFAnimTrailClip[] result = hasTrailsArray ? new KFAnimTrailClip[count] : null;
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadBoolean()) continue;

                var trail = new KFAnimTrailClip
                {
                    sourceNotify = ReadNullableString(reader),
                    startTime = reader.ReadSingle(),
                    endTime = reader.ReadSingle(),
                    clip = clips[i],
                    profile = ReadProfile(reader, materials[i]),
                    firstSocketPath = ReadNullableString(reader),
                    controlSocketPath = ReadNullableString(reader),
                    secondSocketPath = ReadNullableString(reader),
                    samples = ReadSamples(reader, stream),
                    referenceSamples = ReadSamples(reader, stream)
                };
                result[i] = trail;
            }

            if (stream.Position != stream.Length)
                throw new InvalidDataException("KFAnimTrailData payload has trailing bytes.");
            return result;
        }
    }

    private static void WriteProfile(BinaryWriter writer, KFAnimTrailProfile profile)
    {
        writer.Write(profile != null);
        if (profile == null) return;

        WriteNullableString(writer, profile.sourceSystem);
        writer.Write(profile.lifetime);
        writer.Write(profile.color.r);
        writer.Write(profile.color.g);
        writer.Write(profile.color.b);
        writer.Write(profile.color.a);
        WriteCurve(writer, profile.rgbMultiplier);
        WriteCurve(writer, profile.alpha);
        WriteCurve(writer, profile.width);
        writer.Write(profile.distanceTessellationStep);
        writer.Write(profile.tilingDistance);
    }

    private static KFAnimTrailProfile ReadProfile(BinaryReader reader, Material material)
    {
        if (!reader.ReadBoolean()) return null;

        return new KFAnimTrailProfile
        {
            sourceSystem = ReadNullableString(reader),
            material = material,
            lifetime = reader.ReadSingle(),
            color = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            rgbMultiplier = ReadCurve(reader, reader.BaseStream as MemoryStream),
            alpha = ReadCurve(reader, reader.BaseStream as MemoryStream),
            width = ReadCurve(reader, reader.BaseStream as MemoryStream),
            distanceTessellationStep = reader.ReadSingle(),
            tilingDistance = reader.ReadSingle()
        };
    }

    private static void WriteCurve(BinaryWriter writer, AnimationCurve curve)
    {
        writer.Write(curve != null);
        if (curve == null) return;

        writer.Write((int)curve.preWrapMode);
        writer.Write((int)curve.postWrapMode);
        Keyframe[] keys = curve.keys;
        writer.Write(keys.Length);
        for (int i = 0; i < keys.Length; i++)
        {
            Keyframe key = keys[i];
            writer.Write(key.time);
            writer.Write(key.value);
            writer.Write(key.inTangent);
            writer.Write(key.outTangent);
            writer.Write(key.inWeight);
            writer.Write(key.outWeight);
            writer.Write((int)key.weightedMode);
        }
    }

    private static AnimationCurve ReadCurve(BinaryReader reader, MemoryStream stream)
    {
        if (!reader.ReadBoolean()) return null;

        WrapMode preWrapMode = (WrapMode)reader.ReadInt32();
        WrapMode postWrapMode = (WrapMode)reader.ReadInt32();
        int count = ReadCount(reader, stream, 28);
        var keys = new Keyframe[count];
        for (int i = 0; i < count; i++)
        {
            var key = new Keyframe(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
            key.weightedMode = (WeightedMode)reader.ReadInt32();
            keys[i] = key;
        }

        var curve = new AnimationCurve(keys)
        {
            preWrapMode = preWrapMode,
            postWrapMode = postWrapMode
        };
        return curve;
    }

    private static void WriteSamples(BinaryWriter writer, KFAnimTrailSample[] samples)
    {
        writer.Write(samples != null);
        if (samples == null) return;

        writer.Write(samples.Length);
        for (int i = 0; i < samples.Length; i++)
        {
            writer.Write(samples[i].time);
            WriteVector3(writer, samples[i].first);
            WriteVector3(writer, samples[i].control);
            WriteVector3(writer, samples[i].second);
        }
    }

    private static KFAnimTrailSample[] ReadSamples(BinaryReader reader, MemoryStream stream)
    {
        if (!reader.ReadBoolean()) return null;

        int count = ReadCount(reader, stream, 40);
        var samples = new KFAnimTrailSample[count];
        for (int i = 0; i < count; i++)
        {
            samples[i] = new KFAnimTrailSample
            {
                time = reader.ReadSingle(),
                first = ReadVector3(reader),
                control = ReadVector3(reader),
                second = ReadVector3(reader)
            };
        }
        return samples;
    }

    private static void WriteVector3(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.x);
        writer.Write(value.y);
        writer.Write(value.z);
    }

    private static Vector3 ReadVector3(BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    private static void WriteNullableString(BinaryWriter writer, string value)
    {
        writer.Write(value != null);
        if (value != null) writer.Write(value);
    }

    private static string ReadNullableString(BinaryReader reader)
    {
        return reader.ReadBoolean() ? reader.ReadString() : null;
    }

    private static int ReadCount(BinaryReader reader, MemoryStream stream, int minimumBytesPerItem)
    {
        int count = reader.ReadInt32();
        long remainingBytes = stream.Length - stream.Position;
        if (count < 0 || count > remainingBytes / minimumBytesPerItem)
            throw new InvalidDataException("Invalid KFAnimTrailData payload count.");
        return count;
    }
}

[Serializable]
public sealed class KFAnimTrailClip
{
    public string sourceNotify;
    public AnimationClip clip;
    public float startTime;
    public float endTime;
    public KFAnimTrailProfile profile;
    public string firstSocketPath;
    public string controlSocketPath;
    public string secondSocketPath;
    public KFAnimTrailSample[] samples = Array.Empty<KFAnimTrailSample>();
    [Tooltip("Whole-clip socket reference, including poses after the notify window; used only for live pose residuals.")]
    public KFAnimTrailSample[] referenceSamples = Array.Empty<KFAnimTrailSample>();

    public bool IsValid()
    {
        if (clip == null || profile == null || !profile.IsValid() || samples == null || samples.Length < 2 ||
            !KFAnimTrailMesh.Finite(startTime) || !KFAnimTrailMesh.Finite(endTime) || endTime <= startTime)
            return false;
        for (int i = 0; i < samples.Length; i++)
            if (!samples[i].IsFinite() || (i > 0 && samples[i].time <= samples[i - 1].time)) return false;
        if (referenceSamples == null || referenceSamples.Length < 2 || referenceSamples[0].time > startTime ||
            referenceSamples[referenceSamples.Length - 1].time < clip.length - 0.0001f) return false;
        for (int i = 0; i < referenceSamples.Length; i++)
            if (!referenceSamples[i].IsFinite() || (i > 0 && referenceSamples[i].time <= referenceSamples[i - 1].time)) return false;
        return Mathf.Abs(samples[0].time - startTime) < 0.0001f &&
               Mathf.Abs(samples[samples.Length - 1].time - endTime) < 0.0001f;
    }
}

[Serializable]
public sealed class KFAnimTrailProfile
{
    public string sourceSystem;
    public Material material;
    [Min(0.001f)] public float lifetime = 0.2f;
    [ColorUsage(true, true)] public Color color = Color.white;
    [Tooltip("Optional RGB-only lifetime scale. Null/empty preserves legacy constant RGB; alpha stays independent.")]
    public AnimationCurve rgbMultiplier;
    public AnimationCurve alpha = AnimationCurve.Linear(0, 1, 1, 0);
    public AnimationCurve width = AnimationCurve.Constant(0, 1, 1);
    [Tooltip("Distances are in the same skeleton-root-local units as the samples.")]
    [Min(0.000001f)] public float distanceTessellationStep = 0.001f;
    [Min(0)] public float tilingDistance = 0.0256f;

    public bool IsValid()
    {
        return material != null && KFAnimTrailMesh.Finite(lifetime) && lifetime > 0 &&
               KFAnimTrailMesh.Finite(distanceTessellationStep) && distanceTessellationStep > 0 &&
               KFAnimTrailMesh.Finite(tilingDistance) && tilingDistance >= 0 &&
               KFAnimTrailMesh.Finite(color) && alpha != null && width != null;
    }
}

[Serializable]
public struct KFAnimTrailSample
{
    public float time;
    public Vector3 first;
    public Vector3 control;
    public Vector3 second;

    public bool IsFinite()
    {
        return KFAnimTrailMesh.Finite(time) && KFAnimTrailMesh.Finite(first) &&
               KFAnimTrailMesh.Finite(control) && KFAnimTrailMesh.Finite(second);
    }
}
