namespace OriginLab.DocumentGeneration;

public interface IOutputOperations
{
    void CreateDirectory(string path);

    TextWriter CreateStreamWriter(string fileName);

    void WriteAllText(string file, string? contents);

    void CopyFile(string from, string to, bool overwrite);
}
