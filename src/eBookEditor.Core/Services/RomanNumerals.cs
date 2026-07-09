namespace eBookEditor.Core.Services;

/// <summary>Converts positive integers to lowercase roman numerals (i, ii, iii, iv, …), the
/// conventional style for front matter page numbers in print books.</summary>
public static class RomanNumerals
{
    private static readonly (int Value, string Symbol)[] Map =
    [
        (1000, "m"), (900, "cm"), (500, "d"), (400, "cd"),
        (100, "c"), (90, "xc"), (50, "l"), (40, "xl"),
        (10, "x"), (9, "ix"), (5, "v"), (4, "iv"), (1, "i"),
    ];

    public static string ToLowerRoman(int number)
    {
        if (number <= 0)
            return number.ToString();

        var sb = new System.Text.StringBuilder();
        foreach (var (value, symbol) in Map)
        {
            while (number >= value)
            {
                sb.Append(symbol);
                number -= value;
            }
        }
        return sb.ToString();
    }
}
