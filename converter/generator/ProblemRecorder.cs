using AngleSharp.Text;

namespace OriginLab.DocumentGeneration;

public sealed class ProblemRecorder
{
    private readonly string SourceFolder;
    private readonly Dictionary<string, List<(FilePosition filePosition, string? details)>> Problems = [];

    public bool Any => Problems.Count > 0;

    public ProblemRecorder(DocTransformerArgs args)
    {
        SourceFolder = args.SourceFolder;
    }

    public void Record(string sourcePath, string category, string? details = null, TextPosition? position = null)
    {
        var file = Path.GetRelativePath(SourceFolder, sourcePath);

        if (!Problems.TryGetValue(category, out var list))
        {
            Problems[category] = list = [];
        }

        list.Add((new FilePosition(file, position), details));
    }

    public List<(string category, List<(FilePosition filePosition, string? details)> locations)> GetRecords()
    {
        return (from kvp in Problems select (kvp.Key, kvp.Value)).ToList();
    }
}
