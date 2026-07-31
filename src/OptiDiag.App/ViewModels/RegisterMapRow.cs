using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.App.ViewModels;

public sealed class RegisterMapRow
{
    private readonly string[] _cells;

    private RegisterMapRow(string location, string[] cells)
    {
        Location = location;
        _cells = cells;
    }

    public string Location { get; }

    public string this[int column] => _cells[column];

    public static IReadOnlyList<RegisterMapRow> Build(IEnumerable<RegisterValue> registers)
    {
        var result = new List<RegisterMapRow>();

        foreach (var region in registers.GroupBy(register => register.RegionId))
        {
            var valuesByOffset = region.ToDictionary(register => register.Offset, register => register.Value);
            if (valuesByOffset.Count == 0)
            {
                continue;
            }

            var firstRowOffset = valuesByOffset.Keys.Min() & ~0x0F;
            var lastRowOffset = valuesByOffset.Keys.Max() & ~0x0F;

            for (var rowOffset = firstRowOffset; rowOffset <= lastRowOffset; rowOffset += 16)
            {
                var cells = new string[16];
                for (var column = 0; column < cells.Length; column++)
                {
                    cells[column] = valuesByOffset.TryGetValue(rowOffset + column, out var value)
                        ? value.ToString("X2")
                        : "--";
                }

                result.Add(new RegisterMapRow($"{region.Key}  {rowOffset:X2}", cells));
            }
        }

        return result;
    }
}
