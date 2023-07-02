using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public static class PlatformIndependentHash
{
    public static int StringToInt32(string str)
    {
        byte[] encoded = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(str));
        return BitConverter.ToInt32(encoded, 0);
    }

    public static UInt16 StringToUInt16(string str)
    {
        byte[] encoded = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(str));
        return (UInt16)BitConverter.ToUInt32(encoded, 0);
    }
}

