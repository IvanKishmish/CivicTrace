using System.Text.RegularExpressions;
using CivicTrace.Domain.Entities;
using CivicTrace.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CivicTrace.Persistence.Seeding;

public static partial class RentalSeedImporter
{
    private const string DefaultCsvPath = "../../../data/listProzorroSales.csv";

    public static async Task<int> SeedRentalsAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        string contentRootPath,
        CancellationToken ct = default)
    {
        var configured = configuration["Seed:RentalsCsvPath"];
        var path = Path.GetFullPath(string.IsNullOrWhiteSpace(configured) ? DefaultCsvPath : configured, contentRootPath);

        if (!File.Exists(path))
            return 0;

        var lines = await File.ReadAllLinesAsync(path, ct);
        if (lines.Length <= 1) return 0;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = (await db.RentalObjects.Select(r => r.Ocid).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseCsvLine(line);
            if (cols.Count < 3) continue;

            var ocid = cols[0].Trim();
            var rawTitle = cols[1];
            var url = cols[2].Trim();

            if (string.IsNullOrWhiteSpace(ocid) || existing.Contains(ocid))
                continue;

            var defects = new List<string>();
            if (rawTitle.Length != rawTitle.Trim().Length)
                defects.Add("Зайвий пробіл на початку/кінці назви в P3-01");
            if (rawTitle.Contains("нежитові", StringComparison.OrdinalIgnoreCase))
                defects.Add("Описка «нежитові» в наборі P3-01");
            if (url.Contains("planning//", StringComparison.OrdinalIgnoreCase))
                defects.Add("Подвійний слеш «planning//» в URL P3-01");
            if (!url.Contains("prozorro.sale", StringComparison.OrdinalIgnoreCase))
                defects.Add("Посилання веде на сторонній майданчик замість prozorro.sale");

            var cleanTitle = rawTitle.Trim().Replace("нежитові", "нежитлові", StringComparison.OrdinalIgnoreCase);
            if (cleanTitle.Length > 0)
                cleanTitle = char.ToUpper(cleanTitle[0]) + cleanTitle[1..];

            var objectType = NormalizeType(cleanTitle);
            int? year = YearRegex().Match(ocid) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var y)
                ? y
                : null;

            db.RentalObjects.Add(RentalObject.CreateFromList(
                ocid,
                rawTitle,
                cleanTitle,
                objectType,
                url,
                year,
                defects.Count > 0 ? string.Join("; ", defects) : null));

            existing.Add(ocid);
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        return added;
    }

    private static string NormalizeType(string title)
    {
        var lower = title.ToLowerInvariant();
        if (lower.Contains("адміністративній будівлі")) return "Приміщення в адмінбудівлі";
        if (lower.Contains("частина нежитлового")) return "Частина нежитлового приміщення";
        if (lower.Contains("підвальн")) return "Підвальне приміщення";
        if (lower.Contains("даху") || lower.Contains("труби")) return "Дах / технічні елементи";
        if (lower.Contains("громадськ")) return "Громадська будівля";
        if (lower.Contains("нежитлов")) return "Нежитлове приміщення";
        return "Інше нерухоме майно";
    }

    private static List<string> ParseCsvLine(string line)
    {
        var list = new List<string>();
        var inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                list.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        list.Add(current.ToString());
        return list;
    }

    [GeneratedRegex(@"^RGL001-UA-(\d{4})")]
    private static partial Regex YearRegex();
}