using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Infrastructure;

public sealed record ByteDifference(
    string RegionId,
    byte DeviceAddress,
    byte? Page,
    byte? Bank,
    int Offset,
    byte? Left,
    byte? Right,
    bool IsVolatile)
{
    public string Address => Page.HasValue && Bank.HasValue
        ? $"0x{DeviceAddress:X2} B{Bank:X2} P{Page:X2}:{Offset:X2}"
        : Page.HasValue
            ? $"0x{DeviceAddress:X2} P{Page:X2}:{Offset:X2}"
        : $"0x{DeviceAddress:X2}:{Offset:X2}";

    public string LeftHex => Left.HasValue ? $"{Left:X2}" : "--";
    public string RightHex => Right.HasValue ? $"{Right:X2}" : "--";
}

public sealed record DumpComparison(
    ModuleDump Left,
    ModuleDump Right,
    IReadOnlyList<ByteDifference> Differences)
{
    public int StaticDifferenceCount => Differences.Count(x => !x.IsVolatile);
    public int VolatileDifferenceCount => Differences.Count(x => x.IsVolatile);
}

public sealed class DumpComparisonService
{
    public DumpComparison Compare(ModuleDump left, ModuleDump right)
    {
        var differences = new List<ByteDifference>();
        var regionIds = left.Regions.Select(x => x.Id)
            .Union(right.Regions.Select(x => x.Id), StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var id in regionIds)
        {
            var leftRegion = left.FindRegion(id);
            var rightRegion = right.FindRegion(id);
            var length = Math.Max(leftRegion?.Data.Length ?? 0, rightRegion?.Data.Length ?? 0);
            var offsetBase = leftRegion?.Offset ?? rightRegion?.Offset ?? 0;
            for (var index = 0; index < length; index++)
            {
                byte? leftValue = index < (leftRegion?.Data.Length ?? 0) ? leftRegion!.Data[index] : null;
                byte? rightValue = index < (rightRegion?.Data.Length ?? 0) ? rightRegion!.Data[index] : null;
                if (leftValue == rightValue)
                {
                    continue;
                }

                var reference = leftRegion ?? rightRegion!;
                differences.Add(new ByteDifference(
                    id,
                    reference.DeviceAddress,
                    reference.Page,
                    reference.Bank,
                    offsetBase + index,
                    leftValue,
                    rightValue,
                    (leftRegion?.Volatile ?? false) || (rightRegion?.Volatile ?? false)));
            }
        }

        return new DumpComparison(left, right, differences);
    }
}
