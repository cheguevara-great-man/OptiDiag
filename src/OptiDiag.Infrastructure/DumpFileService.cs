using System.IO.Compression;
using System.Text.Json;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Infrastructure;

public sealed class DumpFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task SaveAsync(string path, ModuleDump dump, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true);
        var regionManifests = new List<RegionManifest>();
        foreach (var region in dump.Regions)
        {
            var fileName = $"regions/{Sanitize(region.Id)}.bin";
            var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
            await using (var stream = entry.Open())
            {
                await stream.WriteAsync(region.Data, cancellationToken).ConfigureAwait(false);
            }

            regionManifests.Add(new RegionManifest(
                region.Id,
                region.DisplayName,
                region.DeviceAddress,
                region.Offset,
                region.Page,
                region.Bank,
                region.Volatile,
                fileName,
                region.Data.Length,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(region.Data))));
        }

        var manifest = new DumpManifest(
            dump.FormatVersion,
            dump.ProtocolId,
            dump.ProtocolRevision,
            dump.CapturedAt,
            dump.Source,
            dump.Metadata,
            regionManifests);
        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ModuleDump> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidDataException("不是有效的 .omodump 文件：缺少 manifest.json。");
        await using var manifestStream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<DumpManifest>(manifestStream, JsonOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("Dump 清单为空或格式不正确。");

        var regions = new List<MemoryRegionData>();
        foreach (var item in manifest.Regions)
        {
            var entry = archive.GetEntry(item.FileName)
                ?? throw new InvalidDataException($"Dump 缺少区域文件 {item.FileName}。");
            await using var stream = entry.Open();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            var data = memory.ToArray();
            if (data.Length != item.Length)
            {
                throw new InvalidDataException($"区域 {item.Id} 长度不匹配。");
            }

            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data));
            if (!string.Equals(hash, item.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"区域 {item.Id} 的 SHA-256 校验失败。");
            }

            regions.Add(new MemoryRegionData(
                item.Id,
                item.DisplayName,
                item.DeviceAddress,
                item.Offset,
                data,
                item.Page,
                item.Bank,
                item.Volatile));
        }

        return new ModuleDump(
            manifest.FormatVersion,
            manifest.ProtocolId,
            manifest.ProtocolRevision,
            manifest.CapturedAt,
            manifest.Source,
            regions,
            manifest.Metadata);
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    private sealed record DumpManifest(
        int FormatVersion,
        string ProtocolId,
        string ProtocolRevision,
        DateTimeOffset CapturedAt,
        string Source,
        IReadOnlyDictionary<string, string>? Metadata,
        IReadOnlyList<RegionManifest> Regions);

    private sealed record RegionManifest(
        string Id,
        string DisplayName,
        byte DeviceAddress,
        byte Offset,
        byte? Page,
        byte? Bank,
        bool Volatile,
        string FileName,
        int Length,
        string Sha256);
}
