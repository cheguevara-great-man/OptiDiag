using OptiDiag.I2c.Abstractions;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Application;

public sealed record CaptureResult(ModuleDump Dump, IReadOnlyList<string> Warnings);

public sealed class ModuleMemoryService
{
    private readonly II2cAdapter _adapter;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public ModuleMemoryService(II2cAdapter adapter)
    {
        _adapter = adapter;
    }

    public async Task<CaptureResult> CaptureAsync(
        IOpticalModuleProtocol protocol,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var regions = new List<MemoryRegionData>();
            var warnings = new List<string>();
            foreach (var request in protocol.CapturePlan)
            {
                if (!protocol.ShouldCaptureRegion(request, regions))
                {
                    continue;
                }

                try
                {
                    var data = await ReadRegionCoreAsync(request, cancellationToken).ConfigureAwait(false);
                    regions.Add(new MemoryRegionData(
                        request.Id,
                        request.DisplayName,
                        request.DeviceAddress,
                        request.Offset,
                        data,
                        request.Page,
                        request.Bank,
                        request.Volatile));
                }
                catch (Exception ex) when (request.Optional && ex is not OperationCanceledException)
                {
                    warnings.Add($"可选区域 {request.DisplayName} 读取失败：{ex.Message}");
                }
            }

            return new CaptureResult(
                new ModuleDump(
                    1,
                    protocol.Id,
                    protocol is ICapturedRevisionProvider revisionProvider
                        ? revisionProvider.ResolveRevision(regions)
                        : protocol.Revision,
                    DateTimeOffset.Now,
                    _adapter.Info.DisplayName,
                    regions,
                    new Dictionary<string, string>
                    {
                        ["adapterId"] = _adapter.Info.Id,
                        ["adapterProvider"] = _adapter.Info.Provider,
                        ["addressFormat"] = "7-bit"
                    }),
                warnings);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<byte[]> ReadRegionAsync(
        MemoryCaptureRegion region,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadRegionCoreAsync(region, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<byte> ReadByteAsync(
        byte deviceAddress,
        byte? page,
        byte offset,
        byte pageSelectOffset = 127,
        byte? bank = null,
        byte? bankSelectOffset = null,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (page.HasValue)
            {
                await SelectPageCoreAsync(
                    deviceAddress,
                    page.Value,
                    pageSelectOffset,
                    bank,
                    bankSelectOffset,
                    cancellationToken).ConfigureAwait(false);
            }

            var data = await _adapter.ReadRegistersAsync(
                deviceAddress,
                offset,
                1,
                cancellationToken).ConfigureAwait(false);
            if (data.Length != 1)
            {
                throw new InvalidDataException($"读取寄存器 0x{offset:X2} 时返回了 {data.Length} 字节。");
            }

            return data[0];
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<byte[]> ReadBytesAsync(
        byte deviceAddress,
        byte? page,
        byte offset,
        int length,
        byte pageSelectOffset = 127,
        byte? bank = null,
        byte? bankSelectOffset = null,
        CancellationToken cancellationToken = default)
    {
        if (length < 0 || offset + length > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Read must stay within the 256-byte address window.");
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (page.HasValue)
            {
                await SelectPageCoreAsync(
                    deviceAddress,
                    page.Value,
                    pageSelectOffset,
                    bank,
                    bankSelectOffset,
                    cancellationToken).ConfigureAwait(false);
            }

            var result = new byte[length];
            var completed = 0;
            var maximum = Math.Clamp(_adapter.Info.MaximumReadLength, 1, 128);
            while (completed < length)
            {
                var count = Math.Min(maximum, length - completed);
                var chunk = await _adapter.ReadRegistersAsync(
                    deviceAddress,
                    (byte)(offset + completed),
                    count,
                    cancellationToken).ConfigureAwait(false);
                chunk.CopyTo(result, completed);
                completed += count;
            }

            return result;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task WriteByteAsync(
        byte deviceAddress,
        byte? page,
        byte offset,
        byte value,
        byte pageSelectOffset = 127,
        byte? bank = null,
        byte? bankSelectOffset = null,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (page.HasValue)
            {
                await SelectPageCoreAsync(
                    deviceAddress,
                    page.Value,
                    pageSelectOffset,
                    bank,
                    bankSelectOffset,
                    cancellationToken).ConfigureAwait(false);
            }

            await _adapter.WriteRegistersAsync(deviceAddress, offset, new[] { value }, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task WriteBytesAsync(
        byte deviceAddress,
        byte? page,
        byte offset,
        ReadOnlyMemory<byte> data,
        byte pageSelectOffset = 127,
        byte? bank = null,
        byte? bankSelectOffset = null,
        CancellationToken cancellationToken = default)
    {
        if (offset + data.Length > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "Write must stay within the 256-byte address window.");
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (page.HasValue)
            {
                await SelectPageCoreAsync(
                    deviceAddress,
                    page.Value,
                    pageSelectOffset,
                    bank,
                    bankSelectOffset,
                    cancellationToken).ConfigureAwait(false);
            }

            var completed = 0;
            var maximum = Math.Clamp(_adapter.Info.MaximumWriteLength, 1, 128);
            while (completed < data.Length)
            {
                var count = Math.Min(maximum, data.Length - completed);
                await _adapter.WriteRegistersAsync(
                    deviceAddress,
                    (byte)(offset + completed),
                    data.Slice(completed, count),
                    cancellationToken).ConfigureAwait(false);
                completed += count;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<byte[]> ReadRegionCoreAsync(
        MemoryCaptureRegion request,
        CancellationToken cancellationToken)
    {
        if (request.Page.HasValue)
        {
            var selector = request.PageSelectOffset
                ?? throw new InvalidOperationException($"区域 {request.Id} 设置了页面但未指定 Page Select 偏移。");
            await SelectPageCoreAsync(
                request.DeviceAddress,
                request.Page.Value,
                selector,
                request.Bank,
                request.BankSelectOffset,
                cancellationToken).ConfigureAwait(false);
        }

        var output = new byte[request.Length];
        var completed = 0;
        var maximum = Math.Clamp(_adapter.Info.MaximumReadLength, 1, 128);
        while (completed < request.Length)
        {
            var chunkLength = Math.Min(maximum, request.Length - completed);
            var absoluteOffset = request.Offset + completed;
            if (absoluteOffset > byte.MaxValue || absoluteOffset + chunkLength > 256)
            {
                throw new InvalidOperationException($"区域 {request.Id} 超出 8 位寄存器地址范围。");
            }

            var chunk = await _adapter.ReadRegistersAsync(
                request.DeviceAddress,
                (byte)absoluteOffset,
                chunkLength,
                cancellationToken).ConfigureAwait(false);
            chunk.CopyTo(output, completed);
            completed += chunkLength;
        }

        return output;
    }

    private async Task SelectPageCoreAsync(
        byte deviceAddress,
        byte page,
        byte pageSelectOffset,
        byte? bank,
        byte? bankSelectOffset,
        CancellationToken cancellationToken)
    {
        if (bankSelectOffset.HasValue)
        {
            if (pageSelectOffset != bankSelectOffset.Value + 1)
            {
                throw new InvalidOperationException("CMIS Bank Select 和 Page Select 必须是相邻寄存器。");
            }

            // CMIS requires an arbitrary bank/page change to be one WRITE
            // transaction beginning at byte 126.
            await _adapter.WriteRegistersAsync(
                deviceAddress,
                bankSelectOffset.Value,
                new[] { bank ?? (byte)0, page },
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (bank.HasValue)
            {
                throw new InvalidOperationException("设置 Bank 时必须提供 Bank Select 偏移。");
            }

            await _adapter.WriteRegistersAsync(
                deviceAddress,
                pageSelectOffset,
                new[] { page },
                cancellationToken).ConfigureAwait(false);
        }

        // Reading the selector back prevents both reads and writes from targeting
        // a different page than the one shown in the UI. SFF-8472 page 00h is
        // exempt because older modules may not provide a reliable readback.
        if (page == 0 && !bankSelectOffset.HasValue)
        {
            return;
        }

        var selected = await _adapter.ReadRegistersAsync(
            deviceAddress,
            bankSelectOffset ?? pageSelectOffset,
            bankSelectOffset.HasValue ? 2 : 1,
            cancellationToken).ConfigureAwait(false);
        var pageIndex = bankSelectOffset.HasValue ? 1 : 0;
        var accepted = selected.Length > pageIndex && selected[pageIndex] == page;
        if (bankSelectOffset.HasValue)
        {
            accepted &= selected[0] == (bank ?? (byte)0);
        }

        if (!accepted)
        {
            var actual = string.Join(' ', selected.Select(value => $"{value:X2}"));
            throw new InvalidDataException(
                $"模块未接受 Bank/Page B{bank ?? 0:X2}/P{page:X2}（读回 {actual}）。");
        }
    }
}
