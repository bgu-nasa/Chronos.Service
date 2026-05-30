using Chronos.Admin.Organizations.Contracts;

namespace Chronos.Admin.Cli.Output;

public static class AdminOrganizationTableWriter
{
    public static void Write(IReadOnlyList<OrgSummary> organizations)
    {
        if (organizations.Count == 0)
        {
            Console.WriteLine("No organizations found.");
            return;
        }

        var rows = organizations.Select(o => new[]
        {
            o.OrganizationId.ToString(),
            o.Name,
            string.Join(", ", o.AdminEmails),
            o.UserCount.ToString(),
            o.CreatedAt.ToString("u")
        }).ToList();

        var headers = new[] { "OrganizationId", "Name", "AdminEmails", "UserCount", "Created (UTC)" };
        var widths = headers.Select((h, i) =>
            Math.Max(h.Length, rows.Max(r => r[i].Length))).ToArray();

        Console.WriteLine(FormatRow(headers, widths));
        Console.WriteLine(new string('-', widths.Sum() + (widths.Length - 1) * 2));

        foreach (var row in rows)
        {
            Console.WriteLine(FormatRow(row, widths));
        }
    }

    private static string FormatRow(string[] cells, int[] widths) =>
        string.Join("  ", cells.Select((cell, i) => cell.PadRight(widths[i])));
}
