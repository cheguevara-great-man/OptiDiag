namespace OptiDiag.Protocols.Cmis;

/// <summary>
/// Protocol-neutral memory boundary used by the CDB state machine.
/// The WPF/application layer adapts this to its I2C session; the CMIS project
/// does not depend on a USB bridge or on WPF.
/// </summary>
public interface ICmisCdbMemoryAccess
{
    Task WriteAsync(
        byte? page,
        byte bank,
        byte offset,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken);

    Task<byte[]> ReadAsync(
        byte? page,
        byte bank,
        byte offset,
        int length,
        CancellationToken cancellationToken);
}

public sealed record CmisCdbExecutionResult(
    byte Instance,
    byte Status,
    TimeSpan Elapsed,
    CmisCdbReply Reply)
{
    public bool Success => (Status >> 6) == 0 && (Status & 0x3F) == 1;
}

/// <summary>
/// Implements the CMIS 5.3 CDB command/reply sequence:
/// EPL first, non-triggering Page 9Fh content next, CMDID last, status polling,
/// then reply retrieval and validation.
/// </summary>
public sealed class CmisCdbExecutor
{
    private readonly ICmisCdbMemoryAccess _memory;

    public CmisCdbExecutor(ICmisCdbMemoryAccess memory)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    public async Task<CmisCdbExecutionResult> ExecuteAsync(
        CmisCdbCommand command,
        byte instance = 0,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (instance > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(instance), "CMIS 5.3 supports at most two CDB instances.");
        }

        var messagePage = CmisCdbCodec.EncodeMessagePage(command);
        var extendedPages = CmisCdbCodec.EncodeExtendedPayloadPages(command.ExtendedPayload);
        for (var index = 0; index < extendedPages.Count; index++)
        {
            await _memory.WriteAsync(
                (byte)(0xA0 + index),
                instance,
                128,
                extendedPages[index],
                cancellationToken).ConfigureAwait(false);
        }

        // Bytes 130-255 cannot trigger execution. CMDID is written last in a
        // dedicated two-byte write ending at byte 129, valid for both trigger modes.
        await _memory.WriteAsync(
            0x9F,
            instance,
            130,
            messagePage.AsMemory(2, 126),
            cancellationToken).ConfigureAwait(false);
        await _memory.WriteAsync(
            0x9F,
            instance,
            128,
            messagePage.AsMemory(0, 2),
            cancellationToken).ConfigureAwait(false);

        var started = DateTimeOffset.UtcNow;
        var deadline = started + (timeout ?? TimeSpan.FromSeconds(10));
        byte status;
        do
        {
            status = (await _memory.ReadAsync(
                null,
                0,
                (byte)(37 + instance),
                1,
                cancellationToken).ConfigureAwait(false))[0];
            if ((status >> 6) != 2)
            {
                break;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"CDB instance {instance + 1} command 0x{command.CommandId:X4} did not finish before the timeout.");
            }

            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        } while (true);

        var replyPage = await _memory.ReadAsync(
            0x9F,
            instance,
            128,
            CmisCdbCodec.PageSize,
            cancellationToken).ConfigureAwait(false);
        var encodedReplyLength = replyPage[6];
        byte[] extendedReply = [];
        if (encodedReplyLength >= 240)
        {
            var pageCount = encodedReplyLength - 239;
            extendedReply = new byte[pageCount * CmisCdbCodec.PageSize];
            for (var index = 0; index < pageCount; index++)
            {
                var page = await _memory.ReadAsync(
                    (byte)(0xA0 + index),
                    instance,
                    128,
                    CmisCdbCodec.PageSize,
                    cancellationToken).ConfigureAwait(false);
                page.CopyTo(extendedReply, index * CmisCdbCodec.PageSize);
            }
        }

        var reply = CmisCdbCodec.DecodeReply(replyPage, extendedReply);
        return new CmisCdbExecutionResult(instance, status, DateTimeOffset.UtcNow - started, reply);
    }
}
