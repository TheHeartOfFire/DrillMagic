using System.Linq;

namespace DrillMagic.Core.Constants;

public static class DrillSymbols
{
    public static readonly string[] Symbols =
    [
        // A-Z (Single characters)
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        // a-z (Single characters)
        "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
        // 0-9 (Single characters)
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        // Standard keyboard symbols (13 characters)
        "?", "!", ":", "(", "{", "&", "@", "#", "*", "=", "/", "-", "—",
        // Remaining miscellaneous symbols (18 characters)
        "□", "△", "♠", "♣", "♥", "♦", "☆", "☾", "▱", "◫", "⌂", "⬡", "±", "÷", "∞", "⌽", "✓", "→",
        // AA-ZZ (Double characters)
        .. Enumerable.Range('A', 'Z' - 'A' + 1)
            .Select(c1 => (char)c1)
            .SelectMany(c1 => Enumerable.Range('A', 'Z' - 'A' + 1)
                .Select(c2 => (char)c2), (c1, c2) => $"{c1}{c2}"),
        // aa-zz (Double characters)
        .. Enumerable.Range('a', 'z' - 'a' + 1)
            .Select(c1 => (char)c1)
            .SelectMany(c1 => Enumerable.Range('a', 'z' - 'a' + 1)
                .Select(c2 => (char)c2), (c1, c2) => $"{c1}{c2}"),
    ];
}
