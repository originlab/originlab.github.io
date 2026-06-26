using System.Buffers.Binary;
using System.Buffers.Text;
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

    public static string ToBase64Url(ulong hash)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, hash);

        return Base64Url.EncodeToString(buffer);
    }
}
