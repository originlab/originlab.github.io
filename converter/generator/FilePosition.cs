using AngleSharp.Text;

namespace OriginLab.DocumentGeneration;

public readonly record struct FilePosition(string File, TextPosition? Position);