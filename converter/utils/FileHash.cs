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

    public static ulong FromFile(string path)
    {
        using var fs = File.OpenRead(path);
        var xx = new XxHash3();

        xx.Append(fs);

        return xx.GetCurrentHashAsUInt64();
    }
}
