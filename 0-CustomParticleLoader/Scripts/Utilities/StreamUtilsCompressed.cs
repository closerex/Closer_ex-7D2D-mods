using System.IO;
using UnityEngine;

public static class StreamUtilsCompressed
{
    public static void Write(BinaryWriter _bw, Vector3 vec)
    {
        _bw.Write(Mathf.FloatToHalf(vec.x));
        _bw.Write(Mathf.FloatToHalf(vec.y));
        _bw.Write(Mathf.FloatToHalf(vec.z));
    }

    public static Vector3 ReadHalfVector3(BinaryReader _br)
    {
        return new Vector3(Mathf.HalfToFloat(_br.ReadUInt16()), Mathf.HalfToFloat(_br.ReadUInt16()), Mathf.HalfToFloat(_br.ReadUInt16()));
    }

    public static void Write(BinaryWriter _bw, Quaternion rot)
    {
        _bw.Write(Mathf.FloatToHalf(rot.x));
        _bw.Write(Mathf.FloatToHalf(rot.y));
        _bw.Write(Mathf.FloatToHalf(rot.z));
        _bw.Write(Mathf.FloatToHalf(rot.w));
    }

    public static Quaternion ReadHalfQuaternion(BinaryReader _br)
    {
        return new Quaternion(Mathf.HalfToFloat(_br.ReadUInt16()), Mathf.HalfToFloat(_br.ReadUInt16()), Mathf.HalfToFloat(_br.ReadUInt16()), Mathf.HalfToFloat(_br.ReadUInt16()));
    }
}

