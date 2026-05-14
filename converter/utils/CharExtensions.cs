using System.Runtime.CompilerServices;

namespace OriginLab;

public static class CharExtensions
{
    extension(char separator)
    {
        public string TryPrefixEach(params ReadOnlySpan<string?> parts)
        {
            var count = parts.CountAny(null, "");

            if (count == parts.Length)
            {
                return "";
            }
            else if (count == 0)
            {
                return $"{separator}{String.Join(separator, parts)}";
            }
            else
            {
                var length = 0;

                foreach (var item in parts)
                {
                    if (!item.IsEmpty)
                    {
                        length++;
                        length += item.Length;
                    }
                }

                var handler = new DefaultInterpolatedStringHandler(length, 0);

                foreach (var item in parts)
                {
                    if (!item.IsEmpty)
                    {
                        handler.AppendFormatted(separator);
                        handler.AppendLiteral(item);
                    }
                }

                return handler.ToStringAndClear();
            }
        }

        public string TrySurroundEach(params ReadOnlySpan<string?> parts)
        {
            var count = parts.CountAny(null, "");

            if (count == parts.Length)
            {
                return "";
            }
            else if (count == 0)
            {
                return $"{separator}{String.Join(separator, parts)}{separator}";
            }
            else
            {
                var length = 0;

                foreach (var item in parts)
                {
                    if (!item.IsEmpty)
                    {
                        length++;
                        length += item.Length;
                    }
                }

                length++;

                var handler = new DefaultInterpolatedStringHandler(length, 0);

                foreach (var item in parts)
                {
                    if (!item.IsEmpty)
                    {
                        handler.AppendFormatted(separator);
                        handler.AppendLiteral(item);
                    }
                }

                handler.AppendFormatted(separator);

                return handler.ToStringAndClear();
            }
        }
    }

}
