using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace OriginLab;

public class FileHash
{
    public static string FromString(string contents)
    {
        var hash = XxHash3.HashToUInt64(MemoryMarshal.AsBytes(contents.AsSpan()));
        return String.Create(16, hash, static (span, hash) => hash.TryFormat(span, out _, "x"));
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
