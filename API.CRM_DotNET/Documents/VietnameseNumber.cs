namespace API.CRM_DotNET.Documents;

/// <summary>Reads an amount of money in Vietnamese: 1250000 → "Một triệu hai trăm năm mươi nghìn đồng".</summary>
public static class VietnameseNumber
{
    private static readonly string[] Digits = ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];
    private static readonly string[] Units = ["", " nghìn", " triệu", " tỷ"];

    public static string ToWords(decimal amount)
    {
        var value = (long)Math.Round(Math.Abs(amount), 0, MidpointRounding.AwayFromZero);
        var words = value == 0 ? "không" : ReadNumber(value);
        if (amount < 0) words = "âm " + words;
        return char.ToUpper(words[0]) + words[1..] + " đồng";
    }

    private static string ReadNumber(long value)
    {
        // Above 999 tỷ, read the part before "tỷ" recursively ("một nghìn tỷ", "hai triệu tỷ").
        if (value >= 1_000_000_000_000)
        {
            var high = value / 1_000_000_000;
            var low = value % 1_000_000_000;
            return ReadNumber(high) + " tỷ" + (low > 0 ? " " + ReadGroups(low, true) : "");
        }
        return ReadGroups(value, false);
    }

    /// <summary>Reads groups of three digits. <paramref name="padFirst"/> forces "không trăm" on the leading group.</summary>
    private static string ReadGroups(long value, bool padFirst)
    {
        var groups = new List<int>();
        for (var v = value; v > 0; v /= 1000) groups.Add((int)(v % 1000));

        var parts = new List<string>();
        for (var i = groups.Count - 1; i >= 0; i--)
        {
            if (groups[i] == 0) continue;
            var isFirst = parts.Count == 0 && !padFirst;
            parts.Add(ReadTriple(groups[i], full: !isFirst) + Units[i]);
        }
        return string.Join(" ", parts);
    }

    /// <summary>Reads 1–999. <paramref name="full"/> = not the leading group, so "không trăm" / "linh" are spelled out.</summary>
    private static string ReadTriple(int n, bool full)
    {
        int hundreds = n / 100, tens = n / 10 % 10, ones = n % 10;
        var words = new List<string>();
        if (hundreds > 0 || full) words.Add(Digits[hundreds] + " trăm");

        if (tens == 0)
        {
            if (ones > 0 && words.Count > 0) words.Add("linh");
        }
        else if (tens == 1) words.Add("mười");
        else words.Add(Digits[tens] + " mươi");

        if (ones > 0)
        {
            words.Add(ones switch
            {
                1 when tens >= 2 => "mốt",
                5 when tens >= 1 => "lăm",
                4 when tens >= 2 => "tư",
                _ => Digits[ones],
            });
        }
        return string.Join(" ", words);
    }
}
