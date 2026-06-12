namespace OriginLab.DocumentGeneration;

internal sealed class SystemOutputOperations : IOutputOperations
{
    public void CopyFile(string from, string to, bool overwrite)
        => File.Copy(from, to, overwrite);

    public void CreateDirectory(string path)
        => Directory.CreateDirectory(path);

    public TextWriter CreateStreamWriter(string fileName)
        => new StreamWriter(fileName);

    public void WriteAllText(string file, string? contents)
        => File.WriteAllText(file, contents);
}
