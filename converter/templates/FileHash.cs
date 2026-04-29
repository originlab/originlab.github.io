using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace OriginLab.DocumentGeneration.Templates;

public class FileHash
{
    public static string FromString(string contents)
    {
        var hashBytes = XxHash3.Hash(MemoryMarshal.AsBytes(contents.AsSpan()));
        return Convert.ToHexStringLower(hashBytes);
    }
}
