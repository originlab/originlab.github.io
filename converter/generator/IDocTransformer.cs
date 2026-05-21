namespace OriginLab.DocumentGeneration;

public interface IDocTransformer
{
    Task TransformAsync();

    void WriteProblems(TextWriter textWriter);
}