namespace OriginLab.DocumentGeneration;

readonly record struct NavFiles(string? Parent, string[]? Siblings, string[]? Children);
