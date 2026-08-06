using KeyForwarder.Typing;

namespace KeyForwarder.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("abc", "abc")]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\rb", "a\nb")]
    [InlineData("a\nb", "a\nb")]
    [InlineData("a\r\n\r\nb", "a\n\nb")]
    public void Normalize_ConvertsNewlines(string? input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_PreservesVietnameseAndTabs()
    {
        var input = "Xin chào\tthế giới";
        Assert.Equal(input, TextNormalizer.Normalize(input));
    }
}
