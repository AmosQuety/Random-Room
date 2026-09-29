using System.Security.Cryptography;

namespace RandomRoom.Api.Services;

public interface IRandomChoiceSource
{
    /// <summary>Returns an index in [0, count).</summary>
    int PickIndex(int count);

    /// <summary>A uniformly shuffled copy (Fisher-Yates), built on PickIndex so fakes that only script PickIndex keep working.</summary>
    IReadOnlyList<T> Shuffle<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = PickIndex(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}

public sealed class CryptoRandomChoiceSource : IRandomChoiceSource
{
    // GetInt32 uses rejection sampling, so the result is unbiased.
    public int PickIndex(int count) => RandomNumberGenerator.GetInt32(count);
}
