namespace OriginLab.DocumentGeneration.Tests;

public class UtilsTests
{
    [Theory]
    [InlineData("", new string[0])]
    [InlineData("", new string?[] { null })]
    [InlineData("", new[] { "" })]
    [InlineData("", new[] { "", null })]
    [InlineData("/a", new[] { "a", null })]
    [InlineData("/a", new[] { "a", "" })]
    [InlineData("/a/b", new[] { "a", "b" })]
    [InlineData("/b", new[] { null, "b" })]
    [InlineData("/b", new[] { "", "b" })]
    public void TryPrefixEachSkipsEmptyItems(string expected, string?[] items)
    {
        Assert.Equal(expected, '/'.TryPrefixEach(items));
    }

    [Theory]
    [InlineData("", new string[0])]
    [InlineData("", new string?[] { null })]
    [InlineData("", new[] { "" })]
    [InlineData("", new[] { "", null })]
    [InlineData("/a/", new[] { "a", null })]
    [InlineData("/a/", new[] { "a", "" })]
    [InlineData("/a/b/", new[] { "a", "b" })]
    [InlineData("/b/", new[] { null, "b" })]
    [InlineData("/b/", new[] { "", "b" })]
    public void TrySurroundEachSkipsEmptyItems(string expected, string?[] items)
    {
        Assert.Equal(expected, '/'.TrySurroundEach(items));
    }

}
