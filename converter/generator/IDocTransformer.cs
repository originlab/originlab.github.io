namespace OriginLab.DocumentGeneration;

public interface IDocTransformer
{
    Task TransformAsync();

    void PrintProblems();
}