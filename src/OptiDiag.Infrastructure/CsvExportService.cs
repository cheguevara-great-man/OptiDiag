using System.Globalization;
using System.Text;
using OptiDiag.Application;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Infrastructure;

public sealed class CsvExportService
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public async Task ExportHistoryAsync(
        string path,
        IEnumerable<TrendSample> samples,
        CancellationToken cancellationToken = default)
    {
        var rows = samples.ToArray();
        var keys = rows.SelectMany(x => x.Values.Keys).Distinct(StringComparer.Ordinal).OrderBy(x => x).ToArray();
        await using var writer = new StreamWriter(path, false, Utf8WithBom);
        await writer.WriteLineAsync($"timestamp,{string.Join(',', keys.Select(Escape))}").ConfigureAwait(false);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = keys.Select(key => row.Values.TryGetValue(key, out var value)
                ? value.ToString("G17", CultureInfo.InvariantCulture)
                : string.Empty);
            await writer.WriteLineAsync($"{row.Timestamp:O},{string.Join(',', values)}").ConfigureAwait(false);
        }
    }

    public async Task ExportRegistersAsync(
        string path,
        IEnumerable<RegisterValue> registers,
        CancellationToken cancellationToken = default)
    {
        await using var writer = new StreamWriter(path, false, Utf8WithBom);
        await writer.WriteLineAsync("region,address7,bank,page,offset,hex,decimal,ascii,name,access,description").ConfigureAwait(false);
        foreach (var item in registers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = string.Join(',',
                Escape(item.RegionId),
                $"0x{item.DeviceAddress:X2}",
                item.Bank.HasValue ? $"0x{item.Bank:X2}" : string.Empty,
                item.Page.HasValue ? $"0x{item.Page:X2}" : string.Empty,
                $"0x{item.Offset:X2}",
                item.HexValue,
                item.Value.ToString(CultureInfo.InvariantCulture),
                Escape(item.Ascii.ToString()),
                Escape(item.Name),
                item.Access,
                Escape(item.Description));
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
