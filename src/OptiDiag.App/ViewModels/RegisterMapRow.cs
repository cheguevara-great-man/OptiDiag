using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.App.ViewModels;

public sealed record RegisterMapCell(RegisterValue Register)
{
    public string HexValue => Register.HexValue;
}

public sealed class RegisterMapRow
{
    private readonly RegisterMapCell?[] _cells;

    private RegisterMapRow(string location, RegisterMapCell?[] cells)
    {
        Location = location;
        _cells = cells;
    }

    public string Location { get; }

    public RegisterMapCell? this[int column] => _cells[column];

    public RegisterMapCell? GetCell(int column) =>
        column is >= 0 and < 16 ? _cells[column] : null;

    public static IReadOnlyList<RegisterMapRow> Build(IEnumerable<RegisterValue> registers)
    {
        var result = new List<RegisterMapRow>();

        foreach (var region in registers.GroupBy(register => register.RegionId))
        {
            var registersByOffset = region.ToDictionary(register => register.Offset);
            if (registersByOffset.Count == 0)
            {
                continue;
            }

            var firstRowOffset = registersByOffset.Keys.Min() & ~0x0F;
            var lastRowOffset = registersByOffset.Keys.Max() & ~0x0F;

            for (var rowOffset = firstRowOffset; rowOffset <= lastRowOffset; rowOffset += 16)
            {
                var cells = new RegisterMapCell?[16];
                for (var column = 0; column < cells.Length; column++)
                {
                    if (registersByOffset.TryGetValue(rowOffset + column, out var register))
                    {
                        cells[column] = new RegisterMapCell(register);
                    }
                }

                result.Add(new RegisterMapRow($"{region.Key}  {rowOffset:X2}", cells));
            }
        }

        return result;
    }
}
