using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace OriginLab;

public class FileHash
{
    public static ulong FromString(string contents)
    {
        return XxHash3.HashToUInt64(MemoryMarshal.AsBytes(contents.AsSpan()));
    }

    public static ulong FromFile(string path)
    {
        using var fs = File.OpenRead(path);
        var xx = new XxHash3();

        xx.Append(fs);

        return xx.GetCurrentHashAsUInt64();
    }
}
