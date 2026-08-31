using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Application;

public enum RegisterWriteRisk
{
    Elevated,
    Critical,
    Blocked
}

public sealed record RegisterWriteAssessment(
    RegisterWriteRisk Risk,
    string Title,
    string Reason,
    bool RequiresTypedConfirmation = false)
{
    public bool IsAllowed => Risk != RegisterWriteRisk.Blocked;
}

public sealed record RegisterWriteResult(
    RegisterValue Register,
    byte PreviousValue,
    byte RequestedValue,
    byte? ReadBackValue,
    RegisterWriteAssessment Assessment)
{
    public bool WasReadBackVerified => ReadBackValue == RequestedValue;
}

/// <summary>
/// Central policy for raw register writes. UI code may add confirmations, but it
/// must not decide whether a byte is writable by itself.
/// </summary>
public static class RegisterWritePolicy
{
    public static RegisterWriteAssessment Assess(RegisterValue register)
    {
        if (register.Page is null && register.Offset is 126 or 127)
        {
            return Blocked(
                "Bank/Page 选择寄存器受保护",
                "直接修改选择器会让后续事务落到错误的 Bank/Page；请让会话层执行原子选择和读回校验。");
        }

        if (register.Access is RegisterAccess.ReadOnly
            or RegisterAccess.ReadOnlyClearOnRead
            or RegisterAccess.Mixed
            or RegisterAccess.Reserved)
        {
            return Blocked(
                "协议禁止原始写入",
                $"{register.Name} 的访问属性是 {register.Access}；混合位域也必须通过专用协议操作修改。");
        }

        if (register.Access is RegisterAccess.WriteOnly
            or RegisterAccess.WriteOnlySelfClearing
            or RegisterAccess.ReadWriteSelfClearing)
        {
            return Critical(
                "触发型寄存器",
                "该字节可能启动复位、应用配置、清除计数器或其他一次性动作，写后也不一定能读回相同值。");
        }

        if (register.Access == RegisterAccess.VendorSpecific)
        {
            return Critical(
                "厂商自定义寄存器",
                "公开协议无法判断该字节的副作用；只有持有对应厂商寄存器说明时才应写入。");
        }

        var semanticText = $"{register.Name} {register.Description}";
        if (ContainsDangerousSemantic(semanticText))
        {
            return Critical(
                "控制/状态切换寄存器",
                "字段语义表明它可能影响链路、复位、密码、固件、激光器或当前配置。");
        }

        return new RegisterWriteAssessment(
            RegisterWriteRisk.Elevated,
            "可读写寄存器",
            "写入会立即改变模块状态；系统将先核对旧值，并在写入后执行回读验证。");
    }

    public static void EnsureAuthorized(
        RegisterValue register,
        bool writeUnlocked,
        bool criticalConfirmed)
    {
        if (!writeUnlocked)
        {
            throw new InvalidOperationException("请先开启本次写操作解锁。");
        }

        var assessment = Assess(register);
        if (!assessment.IsAllowed)
        {
            throw new InvalidOperationException($"{assessment.Title}：{assessment.Reason}");
        }

        if (assessment.RequiresTypedConfirmation && !criticalConfirmed)
        {
            throw new InvalidOperationException("该操作属于高风险写入，需要完成二次输入确认。");
        }
    }

    private static bool ContainsDangerousSemantic(string text)
    {
        string[] markers =
        [
            "reset", "apply", "commit", "password", "firmware", "download",
            "laser", "disable", "deinit", "切换", "复位", "应用", "提交",
            "密码", "固件", "下载", "激光", "禁用", "去初始化"
        ];
        return markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static RegisterWriteAssessment Blocked(string title, string reason) =>
        new(RegisterWriteRisk.Blocked, title, reason);

    private static RegisterWriteAssessment Critical(string title, string reason) =>
        new(RegisterWriteRisk.Critical, title, reason, RequiresTypedConfirmation: true);
}

/// <summary>
/// Performs an optimistic write: check the currently selected byte, write once,
/// then verify ordinary read/write registers. This prevents stale UI data from
/// silently overwriting a value changed by polling, firmware, or another host.
/// </summary>
public sealed class SafeRegisterWriter(ModuleSession session)
{
    public async Task<RegisterWriteResult> WriteAsync(
        RegisterValue register,
        byte value,
        bool writeUnlocked,
        bool criticalConfirmed,
        CancellationToken cancellationToken = default)
    {
        RegisterWritePolicy.EnsureAuthorized(register, writeUnlocked, criticalConfirmed);
        var assessment = RegisterWritePolicy.Assess(register);
        var canReadBefore = register.Access is not RegisterAccess.WriteOnly
            and not RegisterAccess.WriteOnlySelfClearing;
        var previous = register.Value;

        if (canReadBefore)
        {
            previous = await ReadAsync(register, cancellationToken).ConfigureAwait(false);
            if (previous != register.Value)
            {
                throw new InvalidOperationException(
                    $"寄存器 {register.AddressText} 已从 0x{register.Value:X2} 变为 0x{previous:X2}。"
                    + "为避免覆盖其他操作，本次写入已取消；请重新读取后再确认。");
            }
        }

        await session.WriteByteAsync(
            register.DeviceAddress,
            register.Page,
            checked((byte)register.Offset),
            value,
            bank: register.Bank,
            bankSelectOffset: register.Bank.HasValue ? (byte)126 : null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        byte? readBack = null;
        if (register.Access is RegisterAccess.ReadWrite or RegisterAccess.VendorSpecific)
        {
            readBack = await ReadAsync(register, cancellationToken).ConfigureAwait(false);
            if (readBack != value)
            {
                throw new InvalidDataException(
                    $"写入 {register.AddressText} 后回读不一致：期望 0x{value:X2}，实际 0x{readBack:X2}。"
                    + "模块可能拒绝了写入，或该字段具有未声明的自清除语义。");
            }
        }

        return new RegisterWriteResult(register, previous, value, readBack, assessment);
    }

    private Task<byte> ReadAsync(RegisterValue register, CancellationToken cancellationToken) =>
        session.ReadByteAsync(
            register.DeviceAddress,
            register.Page,
            checked((byte)register.Offset),
            bank: register.Bank,
            bankSelectOffset: register.Bank.HasValue ? (byte)126 : null,
            cancellationToken: cancellationToken);
}
