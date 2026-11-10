using System.Text.RegularExpressions;
using ManolyWarehouse.Domain.Entities;

namespace ManolyWarehouse.Application.Services;

/// <summary>
/// Works out which stock line (Metro, SH, screws, Kent) a warehouse product belongs to,
/// from the way the team names products on the shelves, e.g.
///   "جرار ميترو زجاج ابيض 50/8.5"  → جرار Metro زجاج واطي 84-500mm · white
///   "حرار sh سلم سكني 40/17"        → جرار SH سلم عالي 167-400mm · grey
///   "براغي اتيكو وميترو 4*30"       → 4x30
///   "كنت 4268" · "(T)كنت 1000"     → 4268 · M1000T
/// Names already written like the stock line ("جرار Metro واطي 84-350mm أبيض") match too.
/// </summary>
public static class StockMatcher
{
    private static readonly Dictionary<string, int> Heights = new() { ["8.5"] = 84, ["12"] = 12, ["17"] = 167, ["20"] = 199 };

    private static readonly Dictionary<(string Kind, int H), string> Types = new()
    {
        [("بوكس", 84)] = "واطي", [("بوكس", 167)] = "عالي مع ماسورة",
        [("سلم", 84)] = "سلم واطي", [("سلم", 12)] = "سلم عالي", [("سلم", 167)] = "سلم عالي", [("سلم", 199)] = "سلم عالي",
        [("زجاج", 84)] = "زجاج واطي", [("زجاج", 167)] = "زجاج عالي مع ماسورة", [("زجاج", 199)] = "زجاج عالي",
    };

    private static readonly Regex Drawer = new(
        @"^(?:جرار|حرار)\s*(ميترو|metro|sh)\s*(بوكس|سلم|زجاج)\s*(ابيض|أبيض|سكني)\s*([\d.]+)\s*/\s*([\d.]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Screw = new(
        @"^براغي\s*اتيكو\s*و?\s*ميترو\s*(?:(سن كامل|نص سن)\s*)?([\d.]+)\s*[*x×]\s*(\d+)(?:\s*[*x×]\s*\d+)?$", RegexOptions.Compiled);

    // Pieces per carton written after the name: "… 55/12 *6", "… 55/12 6*", "… 55/12 x4", "… (6)"
    private static readonly Regex PerCarton = new(@"\s*(?:[*x×]\s*\d+|\d+\s*[*x×]|\(\s*\d+\s*\))\s*$", RegexOptions.Compiled);

    public static string Norm(string s)
    {
        s = Regex.Replace(s ?? "", @"\s+", " ").Trim();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s) sb.Append(ch is >= '٠' and <= '٩' ? (char)('0' + (ch - '٠')) : ch);
        return sb.ToString();
    }

    /// <summary>The stock line for this product name, or null if it isn't a Metro/SH/screws/Kent product.</summary>
    public static StockItem? Match(string productName, IReadOnlyList<StockItem> lines)
    {
        var n = Norm(productName);
        var byKey = lines.GroupBy(l => (l.Family, l.Name, l.Color)).ToDictionary(g => g.Key, g => g.First());

        // 1. Already named like the stock line: "<line name> أبيض|سكني"
        foreach (var (word, color) in new[] { ("أبيض", "white"), ("ابيض", "white"), ("سكني", "grey") })
            if (n.EndsWith(" " + word))
            {
                var core = n[..^(word.Length + 1)].Trim();
                var hit = lines.FirstOrDefault(l => l.Color == color && l.Name == core);
                if (hit != null) return hit;
            }

        // 2. Drawers as written on the shelves (with or without "*6" at the end)
        var m = Drawer.Match(n);
        if (!m.Success) m = Drawer.Match(PerCarton.Replace(n, "").Trim());
        if (m.Success)
        {
            var brand = m.Groups[1].Value.ToLowerInvariant() is "ميترو" or "metro" ? "Metro" : "SH";
            var kind = m.Groups[2].Value;
            var color = m.Groups[3].Value == "سكني" ? "grey" : "white";
            string a = m.Groups[4].Value, b = m.Groups[5].Value;
            if (double.TryParse(a, System.Globalization.CultureInfo.InvariantCulture, out var da)
                && double.TryParse(b, System.Globalization.CultureInfo.InvariantCulture, out var db) && da < db)
                (a, b, da) = (b, a, db);                                  // "8.5/50" written the other way round
            if (!Heights.TryGetValue(b, out var h) || !Types.TryGetValue((kind, h), out var type)) return null;
            var len = (int)Math.Round(da * 10);
            var family = brand == "Metro" ? "metro" : "sh";
            var name = $"جرار {brand} {type} {h}-{len}mm";
            if (byKey.TryGetValue((family, name, color), out var line)) return line;
            // Metro سلم 12: the stock file lists these sizes only under سكني
            if (h == 12 && byKey.TryGetValue((family, name, color == "white" ? "grey" : "white"), out line)) return line;
            return null;
        }

        // 3. Screws "براغي اتيكو وميترو 4*30" (and the two 5x70 kinds)
        var s = Screw.Match(n);
        if (s.Success)
        {
            var kind = s.Groups[1].Value;
            var key = kind switch
            {
                "سن كامل" => $"{s.Groups[2].Value}0x{s.Groups[3].Value} سن كامل",   // 5*70 → "50x70 سن كامل"
                _ => $"{s.Groups[2].Value}x{s.Groups[3].Value}"
            };
            return byKey.GetValueOrDefault(("screws", key, null));
        }
        if (n == lines.FirstOrDefault(l => l.Family == "screws" && l.Name == n)?.Name)
            return byKey[("screws", n, null)];

        // 4. Kent "كنت 4268", "(T)كنت 1000", "(H.G)كنت 700", "كنت ميترو 1098-400 …"
        if (n.Contains("كنت")) return MatchKent(n, lines);
        return null;
    }

    private static StockItem? MatchKent(string n, IReadOnlyList<StockItem> lines)
    {
        var kent = lines.Where(l => l.Family == "kent").ToList();
        if (n.Contains("1098-400")) return kent.FirstOrDefault(l => l.Name.StartsWith("1098-400"));
        if (n.Contains("سكالا") || Regex.IsMatch(n, @"\b[mM]?600")) return null;   // not in the Kent list
        var core = Core(n);
        if (core is null) return null;
        var cands = kent.Where(l => Core(l.Name) == core).ToList();
        if (cands.Count == 0) return null;
        if (cands.Count == 1) return cands[0];
        var f = Flags(n);
        var exact = cands.FirstOrDefault(c => Flags(c.Name).SetEquals(f));
        if (exact != null) return exact;
        if (f.Count == 0)
        {
            var noHg = cands.Where(c => !Flags(c.Name).Contains("HG")).ToList();
            if (noHg.Count == 1) return noHg[0];
        }
        return null;
    }

    private static string? Core(string s)
    {
        var m = Regex.Match(s, @"(\d+(?:[-_]\d+)?)");
        return m.Success ? m.Groups[1].Value.Replace('_', '-') : null;
    }

    private static HashSet<string> Flags(string s)
    {
        s = s.ToUpperInvariant().Replace("H.G", "HG");
        var f = new HashSet<string>();
        foreach (var t in new[] { "HG", "LE", "T", "C", "R", "W" })
            if (Regex.IsMatch(s, $@"(?<![A-Z]){t}(?![A-Z])") || Regex.IsMatch(s, $@"\d{t}\b")) f.Add(t);
        return f;
    }
}
