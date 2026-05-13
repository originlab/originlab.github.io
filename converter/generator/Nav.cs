namespace OriginLab.DocumentGeneration;

readonly record struct Nav(NavFiles Files, Dictionary<string, string> Titles);
