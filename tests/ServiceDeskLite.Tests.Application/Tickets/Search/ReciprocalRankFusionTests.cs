using FluentAssertions;

using ServiceDeskLite.Application.Tickets.Search;

namespace ServiceDeskLite.Tests.Application.Tickets.Search;

public sealed class ReciprocalRankFusionTests
{
    [Fact]
    public void Fuse_ItemInBothLists_ScoresHigherThanItemInOne()
    {
        var a = new[] { "x", "y" };
        var b = new[] { "x", "z" };

        var scores = ReciprocalRankFusion.Fuse([((IReadOnlyList<string>)a, 1.0), (b, 1.0)]);

        scores["x"].Should().BeGreaterThan(scores["y"]);
        scores["x"].Should().BeGreaterThan(scores["z"]);
    }

    [Fact]
    public void Fuse_EarlierRank_ScoresHigher()
    {
        var ranking = new[] { "first", "second", "third" };

        var scores = ReciprocalRankFusion.Fuse([((IReadOnlyList<string>)ranking, 1.0)]);

        scores["first"].Should().BeGreaterThan(scores["second"]);
        scores["second"].Should().BeGreaterThan(scores["third"]);
    }

    [Fact]
    public void Fuse_RespectsWeights()
    {
        var strong = new[] { "a" };
        var weak = new[] { "b" };

        var scores = ReciprocalRankFusion.Fuse([((IReadOnlyList<string>)strong, 2.0), (weak, 1.0)]);

        scores["a"].Should().BeGreaterThan(scores["b"]);
    }

    [Fact]
    public void Fuse_EmptySignals_ReturnsEmpty()
    {
        ReciprocalRankFusion.Fuse<string>([]).Should().BeEmpty();
    }

    [Theory]
    [InlineData(5.0, 10.0, 0.5)]
    [InlineData(10.0, 10.0, 1.0)]
    [InlineData(1.0, 0.0, 0.0)]
    public void Normalize_ScalesToMax(double score, double max, double expected)
    {
        ReciprocalRankFusion.Normalize(score, max).Should().Be(expected);
    }
}
