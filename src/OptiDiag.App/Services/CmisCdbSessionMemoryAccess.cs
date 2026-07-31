using OptiDiag.Application;
using OptiDiag.Protocols.Cmis;

namespace OptiDiag.App.Services;

internal sealed class CmisCdbSessionMemoryAccess : ICmisCdbMemoryAccess
{
    private readonly ModuleSession _session;

    public CmisCdbSessionMemoryAccess(ModuleSession session)
    {
        _session = session;
    }

    public Task WriteAsync(
        byte? page,
        byte bank,
        byte offset,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken) =>
        _session.WriteBytesAsync(
            0x50,
            page,
            offset,
            data,
            cancellationToken,
            page.HasValue ? bank : null,
            page.HasValue ? (byte)126 : null);

    public Task<byte[]> ReadAsync(
        byte? page,
        byte bank,
        byte offset,
        int length,
        CancellationToken cancellationToken) =>
        _session.ReadBytesAsync(
            0x50,
            page,
            offset,
            length,
            cancellationToken,
            page.HasValue ? bank : null,
            page.HasValue ? (byte)126 : null);
}
