using System.Security.Cryptography;

namespace RandomRoom.Api.Services;

public interface IRandomChoiceSource
{
    /// <summary>Returns an index in [0, count).</summary>
    int PickIndex(int count);
}

public sealed class CryptoRandomChoiceSource : IRandomChoiceSource
{
    // GetInt32 uses rejection sampling, so the result is unbiased.
    public int PickIndex(int count) => RandomNumberGenerator.GetInt32(count);
}
