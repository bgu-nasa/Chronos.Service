using Chronos.Admin.Auth.Contracts;

namespace Chronos.Admin.Cli.Output;

public static class AdminAccountTableWriter
{
    public static void Write(IReadOnlyList<UserResponse> accounts)
    {
        if (accounts.Count == 0)
        {
            Console.WriteLine("No platform admin accounts.");
            return;
        }

        var rows = accounts.Select(a => new[]
        {
            a.Email,
            $"{a.FirstName} {a.LastName}",
            a.Verified ? "yes" : "no",
            a.IsBootstrap ? "yes" : "no",
            a.CreatedAt.ToString("u")
        }).ToList();

        var headers = new[] { "Email", "Name", "Verified", "Bootstrap", "Created (UTC)" };
        var widths = headers.Select((h, i) =>
            Math.Max(h.Length, rows.Max(r => r[i].Length))).ToArray();

        Console.WriteLine(FormatRow(headers, widths));
        Console.WriteLine(new string('-', widths.Sum() + (widths.Length - 1) * 2));

        foreach (var row in rows)
        {
            Console.WriteLine(FormatRow(row, widths));
        }
    }

    private static string FormatRow(string[] cells, int[] widths)
    {
        return string.Join("  ", cells.Select((cell, i) => cell.PadRight(widths[i])));
    }
}
