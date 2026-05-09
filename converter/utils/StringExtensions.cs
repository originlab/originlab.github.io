using System.Diagnostics.CodeAnalysis;

namespace OriginLab;

public static class StringExtensions
{
    extension([NotNullWhen(false)] string? str)
    {
        public bool IsEmpty => String.IsNullOrEmpty(str);

        public bool IsBlank => String.IsNullOrWhiteSpace(str);
    }
}
