using RandomRoom.Api.Services;

namespace RandomRoom.Tests;

public class ShuffleTests
{
    /// <summary>Returns scripted picks in order and records every count it was asked for.</summary>
    private sealed class RecordingSource(params int[] picks) : IRandomChoiceSource
    {
        private readonly Queue<int> script = new(picks);

        public List<int> RequestedCounts { get; } = [];

        public int PickIndex(int count)
        {
            RequestedCounts.Add(count);
            return script.Dequeue();
        }
    }

    private static IRandomChoiceSource Source(params int[] picks) => new RecordingSource(picks);

    [Fact]
    public void Shuffle_EmptyInput_ReturnsEmptyWithoutPicking()
    {
        var source = new RecordingSource();

        var result = ((IRandomChoiceSource)source).Shuffle(Array.Empty<string>());

        Assert.Empty(result);
        Assert.Empty(source.RequestedCounts);
    }

    [Fact]
    public void Shuffle_SingleItem_ReturnsItWithoutPicking()
    {
        var source = new RecordingSource();

        var result = ((IRandomChoiceSource)source).Shuffle(new[] { "only" });

        Assert.Equal(new[] { "only" }, result);
        Assert.Empty(source.RequestedCounts);
    }

    [Fact]
    public void Shuffle_ScriptedPicks_ProducesExactOrder()
    {
        // [a,b,c,d]: i=3<->0 -> [d,b,c,a]; i=2<->2 -> same; i=1<->0 -> [b,d,c,a]
        var result = Source(0, 2, 0).Shuffle(new[] { "a", "b", "c", "d" });

        Assert.Equal(new[] { "b", "d", "c", "a" }, result);
    }

    [Fact]
    public void Shuffle_PicksOwnPosition_LeavesOrderUnchanged()
    {
        var result = Source(3, 2, 1).Shuffle(new[] { 1, 2, 3, 4 });

        Assert.Equal(new[] { 1, 2, 3, 4 }, result);
    }

    [Fact]
    public void Shuffle_Result_IsPermutationOfInput()
    {
        var input = new[] { 5, 3, 3, 9, 1, 5 };

        var result = Source(2, 0, 4, 1, 0).Shuffle(input);

        Assert.Equal(input.OrderBy(x => x), result.OrderBy(x => x));
    }

    [Fact]
    public void Shuffle_DoesNotMutateInput()
    {
        var input = new[] { "a", "b", "c", "d" };

        Source(0, 2, 0).Shuffle(input);

        Assert.Equal(new[] { "a", "b", "c", "d" }, input);
    }

    [Fact]
    public void Shuffle_CallsPickIndexWithCountDownToTwo()
    {
        var source = new RecordingSource(0, 0, 0, 0);

        ((IRandomChoiceSource)source).Shuffle(new[] { 1, 2, 3, 4, 5 });

        Assert.Equal(new[] { 5, 4, 3, 2 }, source.RequestedCounts);
    }
}
