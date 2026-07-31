using System.Buffers.Binary;

namespace OptiDiag.Protocols.Cmis;

public sealed record CmisFirmwareDownloadOptions(
    bool UseExtendedPayload = true,
    int BlockSize = 128,
    bool SkipErasedBlocks = false,
    byte ErasedByte = 0xFF,
    byte[]? StartVendorData = null);

public sealed record CmisFirmwareProgress(
    long CompletedBytes,
    long TotalBytes,
    int BlocksWritten,
    int BlocksSkipped)
{
    public double Percentage => TotalBytes == 0 ? 100 : CompletedBytes * 100d / TotalBytes;
}

public sealed record CmisFirmwareDownloadResult(
    long ImageSize,
    int BlocksWritten,
    int BlocksSkipped,
    TimeSpan Elapsed);

/// <summary>
/// CMIS 5.3 section 9.7 firmware download state machine.
/// Running or committing the new image are explicit separate calls so that a
/// successful transfer never resets traffic without a deliberate host action.
/// </summary>
public sealed class CmisFirmwareUpdateService
{
    private readonly CmisCdbExecutor _executor;
    private readonly byte _instance;

    public CmisFirmwareUpdateService(CmisCdbExecutor executor, byte instance = 0)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        if (instance > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(instance));
        }

        _instance = instance;
    }

    public async Task<CmisFirmwareDownloadResult> DownloadAsync(
        ReadOnlyMemory<byte> image,
        CmisFirmwareDownloadOptions? options = null,
        IProgress<CmisFirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CmisFirmwareDownloadOptions();
        if (image.Length == 0)
        {
            throw new ArgumentException("Firmware image cannot be empty.", nameof(image));
        }

        var maximumBlockSize = options.UseExtendedPayload ? 2048 : 116;
        if (options.BlockSize < 1 || options.BlockSize > maximumBlockSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"Block size must be 1..{maximumBlockSize} for the selected transfer mechanism.");
        }

        var vendorData = options.StartVendorData ?? [];
        if (vendorData.Length > 112)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Start command vendor data cannot exceed 112 bytes.");
        }

        var started = DateTimeOffset.UtcNow;
        var startPayload = new byte[8 + vendorData.Length];
        BinaryPrimitives.WriteUInt32BigEndian(startPayload.AsSpan(0, 4), checked((uint)image.Length));
        vendorData.CopyTo(startPayload, 8);
        EnsureSuccess(await _executor.ExecuteAsync(
            new CmisCdbCommand(0x0101, startPayload),
            _instance,
            cancellationToken: cancellationToken).ConfigureAwait(false), "Start Firmware Download");

        var written = 0;
        var skipped = 0;
        var completed = 0;
        try
        {
            while (completed < image.Length)
            {
                var count = Math.Min(options.BlockSize, image.Length - completed);
                var block = image.Slice(completed, count);
                if (options.SkipErasedBlocks && IsErased(block.Span, options.ErasedByte))
                {
                    skipped++;
                }
                else
                {
                    var address = checked((uint)completed);
                    CmisCdbCommand command;
                    if (options.UseExtendedPayload)
                    {
                        var local = new byte[4];
                        BinaryPrimitives.WriteUInt32BigEndian(local, address);
                        command = new CmisCdbCommand(0x0104, local, block.ToArray());
                    }
                    else
                    {
                        var local = new byte[4 + count];
                        BinaryPrimitives.WriteUInt32BigEndian(local.AsSpan(0, 4), address);
                        block.CopyTo(local.AsMemory(4));
                        command = new CmisCdbCommand(0x0103, local);
                    }

                    EnsureSuccess(await _executor.ExecuteAsync(
                        command,
                        _instance,
                        cancellationToken: cancellationToken).ConfigureAwait(false), "Write Firmware Block");
                    written++;
                }

                completed += count;
                progress?.Report(new CmisFirmwareProgress(completed, image.Length, written, skipped));
            }

            EnsureSuccess(await _executor.ExecuteAsync(
                new CmisCdbCommand(0x0107),
                _instance,
                cancellationToken: cancellationToken).ConfigureAwait(false), "Complete Firmware Download");
        }
        catch
        {
            await TryAbortAsync().ConfigureAwait(false);
            throw;
        }

        return new CmisFirmwareDownloadResult(
            image.Length,
            written,
            skipped,
            DateTimeOffset.UtcNow - started);

        async Task TryAbortAsync()
        {
            try
            {
                await _executor.ExecuteAsync(
                    new CmisCdbCommand(0x0102),
                    _instance,
                    timeout: TimeSpan.FromSeconds(2),
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Preserve the original download exception.
            }
        }
    }

    public Task<CmisCdbExecutionResult> GetFirmwareInfoAsync(
        CancellationToken cancellationToken = default) =>
        _executor.ExecuteAsync(
            new CmisCdbCommand(0x0100),
            _instance,
            cancellationToken: cancellationToken);

    public Task<CmisCdbExecutionResult> RunImageAsync(
        byte imageToRun,
        ushort delayToResetMilliseconds,
        CancellationToken cancellationToken = default)
    {
        var payload = new byte[4];
        payload[1] = imageToRun;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), delayToResetMilliseconds);
        return _executor.ExecuteAsync(
            new CmisCdbCommand(0x0109, payload),
            _instance,
            cancellationToken: cancellationToken);
    }

    public Task<CmisCdbExecutionResult> CommitRunningImageAsync(
        CancellationToken cancellationToken = default) =>
        _executor.ExecuteAsync(
            new CmisCdbCommand(0x010A),
            _instance,
            cancellationToken: cancellationToken);

    private static bool IsErased(ReadOnlySpan<byte> data, byte erasedByte)
    {
        foreach (var value in data)
        {
            if (value != erasedByte)
            {
                return false;
            }
        }

        return true;
    }

    private static void EnsureSuccess(CmisCdbExecutionResult result, string operation)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException($"{operation} failed with CDB status 0x{result.Status:X2}.");
        }
    }
}
