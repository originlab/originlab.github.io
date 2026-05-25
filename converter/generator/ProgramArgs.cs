using CommandLine;

namespace OriginLab.DocumentGeneration;

internal class ProgramArgs
{
    [Value(0, Required = true)]
    public required string SourceBookPath { get; init; }

    [Option]
    public bool Merge { get; init; }

    [Option]
    public bool Optimizing { get; init; }
}
