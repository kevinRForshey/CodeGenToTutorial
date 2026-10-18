using CodeGenToTutorial.Core.Helpers;

namespace CodeGenToTutorial.Core.Tests.Helpers;

public class TokenEstimatorTests
{
    [Fact]
    public void EstimateTokenCount_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, TokenEstimator.EstimateTokenCount(string.Empty));
    }

    [Theory]
    [InlineData("abcd", 1)]
    [InlineData("abcde", 2)]
    [InlineData("abcdefgh", 2)]
    public void EstimateTokenCount_RoundsUpToTheNearestToken(string text, int expected)
    {
        Assert.Equal(expected, TokenEstimator.EstimateTokenCount(text));
    }

    [Fact]
    public void EstimateTokenCount_LongerText_ReturnsMoreTokensThanShorterText()
    {
        var shortCount = TokenEstimator.EstimateTokenCount("a short prompt");
        var longCount = TokenEstimator.EstimateTokenCount(string.Concat(Enumerable.Repeat("a much longer prompt ", 20)));

        Assert.True(longCount > shortCount);
    }
}
