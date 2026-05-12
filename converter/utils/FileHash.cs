using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace OriginLab;

public class FileHash
{
    public static string FromString(string contents)
    {
        Span<byte> hash = stackalloc byte[8];
        XxHash3.Hash(MemoryMarshal.AsBytes(contents.AsSpan()), hash);

        return Convert.ToHexStringLower(hash);
    }

    public static ulong UInt64FromFile(string path)
    {
        using var fs = File.OpenRead(path);
        var xx = new XxHash3();

        xx.Append(fs);

        return xx.GetCurrentHashAsUInt64();
    }

    public static string StringFromFile(string path)
    {
        using var fs = File.OpenRead(path);
        var xx = new XxHash3();

        xx.Append(fs);

        Span<byte> hash = stackalloc byte[8];
        xx.GetCurrentHash(hash);

        return Convert.ToHexStringLower(hash);
    }
}
