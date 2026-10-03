namespace RandomRoom.Api.Games.Bingo;

/// <summary>The geometry of a 5x5 card: a free centre and the twelve lines that win.</summary>
public static class BingoCard
{
    public const int Size = 5;
    public const int Cells = Size * Size;
    public const int FreeCell = Cells / 2;
    public const int ItemsPerCard = Cells - 1;

    private static readonly int[][] Lines = BuildLines();

    private static int[][] BuildLines()
    {
        var lines = new List<int[]>();
        for (var i = 0; i < Size; i++)
        {
            lines.Add(Enumerable.Range(0, Size).Select(c => i * Size + c).ToArray());
            lines.Add(Enumerable.Range(0, Size).Select(r => r * Size + i).ToArray());
        }
        lines.Add(Enumerable.Range(0, Size).Select(i => i * Size + i).ToArray());
        lines.Add(Enumerable.Range(0, Size).Select(i => i * Size + (Size - 1 - i)).ToArray());
        return lines.ToArray();
    }

    /// <summary>The first complete line of marked cells, or null.</summary>
    public static int[]? WinningLine(IReadOnlyList<bool> marks) =>
        Lines.FirstOrDefault(line => line.All(cell => marks[cell]));

    /// <summary>Lays 24 items onto the card around the free centre (which holds an empty string).</summary>
    public static List<string> Lay(IEnumerable<string> items)
    {
        var cells = items.ToList();
        cells.Insert(FreeCell, "");
        return cells;
    }
}
