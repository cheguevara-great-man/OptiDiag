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
                    protocol.Revision,
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

    public async Task WriteByteAsync(
        byte deviceAddress,
        byte? page,
        byte offset,
        byte value,
        byte pageSelectOffset = 127,
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
        CancellationToken cancellationToken)
    {
        await _adapter.WriteRegistersAsync(
            deviceAddress,
            pageSelectOffset,
            new[] { page },
            cancellationToken).ConfigureAwait(false);

        // SFF-8472 requires an unsupported page selection to fall back to 00h.
        // Reading the selector back prevents both reads and writes from targeting
        // a different page than the one shown in the UI.
        if (page == 0)
        {
            return;
        }

        var selected = await _adapter.ReadRegistersAsync(
            deviceAddress,
            pageSelectOffset,
            1,
            cancellationToken).ConfigureAwait(false);
        if (selected.Length != 1 || selected[0] != page)
        {
            var actual = selected.Length == 1 ? $"0x{selected[0]:X2}" : $"{selected.Length} 字节";
            throw new InvalidDataException($"模块未接受页面 0x{page:X2}（读回 {actual}）。");
        }
    }
}
