namespace OriginLab.DocumentGeneration.Tests;

internal class FakeDocToStaticPagesTransformer : DocToStaticPagesTransformer
{
    public FakeDocToStaticPagesTransformer(DocToStaticPagesTransformerArgs args, ProblemRecorder problems) : base(args, problems)
    {
    }

    protected override Task TransformAsync(string language)
    {
        throw new NotImplementedException();
    }
}
