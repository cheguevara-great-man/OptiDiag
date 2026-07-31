using System.Buffers.Binary;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Sff8472;

public sealed partial class Sff8472Protocol
{
    private static void DecodeTimingFields(
        ModuleDump dump,
        IReadOnlyList<Measurement> measurements,
        ICollection<DecodedField> fields,
        ICollection<DecodeDiagnostic> diagnostics)
    {
        var page = dump.FindRegion(A2Page03RegionId)?.Data;
        if (page is not { Length: >= 128 })
        {
            return;
        }

        var formatId = ReadUInt16P03(page, 128);
        Add(fields, "高精度时延/公共头", "Format ID",
            formatId switch
            {
                0xCA1B => "CA1Bh - 光模块校准",
                0x100B => "100Bh - 回环模块校准",
                _ => $"未定义 (0x{formatId:X4})"
            }, "A2h P03.128-129");
        if (formatId is not (0xCA1B or 0x100B))
        {
            return;
        }

        Add(fields, "高精度时延/公共头", "Format Version", $"{P03(page, 130)}", "A2h P03.130");
        if (P03(page, 130) != 1)
        {
            diagnostics.Add(new DecodeDiagnostic(
                DiagnosticSeverity.Warning,
                $"Page 03h Format Version 应为 1，实际为 {P03(page, 130)}。",
                A2Page03RegionId));
        }

        Add(fields, "高精度时延/公共头", "Calibration Date", DecodeCalibrationDate(page),
            "A2h P03.131-133", "日期使用压缩位域，并非三个普通十进制字节。");
        Add(fields, "高精度时延/公共头", "Calibration OUI/CID",
            $"{P03(page, 134):X2}-{P03(page, 135):X2}-{P03(page, 136):X2}",
            "A2h P03.134-136");
        Add(fields, "高精度时延/公共头", "Calibration OSI",
            $"{P03(page, 137):X2}-{P03(page, 138):X2}-{P03(page, 139):X2}",
            "A2h P03.137-139");
        Add(fields, "高精度时延/公共头", "Calibration Stratum",
            P03(page, 140) == 0xFF ? "未定义" : P03(page, 140).ToString(),
            "A2h P03.140", "0 表示最高校准精度。");

        if (formatId == 0xCA1B)
        {
            DecodeOpticalTiming(page, measurements, fields, diagnostics);
        }
        else
        {
            DecodeLoopbackTiming(page, fields);
        }
    }

    private static void DecodeOpticalTiming(
        byte[] page,
        IReadOnlyList<Measurement> measurements,
        ICollection<DecodedField> fields,
        ICollection<DecodeDiagnostic> diagnostics)
    {
        var lanes = P03(page, 150);
        var operationMode = P03(page, 151);
        Add(fields, "高精度时延/光模块", "Number of Lanes", lanes.ToString(), "A2h P03.150");
        Add(fields, "高精度时延/光模块", "Operation Mode ID", operationMode.ToString(), "A2h P03.151");
        if (lanes != 1)
        {
            diagnostics.Add(new DecodeDiagnostic(
                DiagnosticSeverity.Warning,
                $"SFF-8472 Page 03h 的 Nb_Lanes 应为 1，实际为 {lanes}。",
                A2Page03RegionId));
        }

        var coefficients = new double[5];
        for (var order = 0; order < coefficients.Length; order++)
        {
            var offset = 152 + order * 3;
            coefficients[order] = ReadSigned24P03(page, offset) / 65536d;
            Add(fields, "高精度时延/RX 功率修正", $"Rx_Pwr_Dly({order})",
                $"{coefficients[order]:0.########} ns/dBm^{order}",
                $"A2h P03.{offset}-{offset + 2}", "signed q8.16");
        }

        var detuneOffsetNm = ReadInt16P03(page, 167) / 256d * 0.1;
        var detuneSlopeNmPerC = ReadInt16P03(page, 169) / 256d * 0.01;
        Add(fields, "高精度时延/温度修正", "T_Detune_Offset",
            $"{detuneOffsetNm:0.########} nm", "A2h P03.167-168", "signed q8.8，单位 0.1 nm");
        Add(fields, "高精度时延/温度修正", "T_Detune_Slope",
            $"{detuneSlopeNmPerC:0.########} nm/°C", "A2h P03.169-170", "signed q8.8，单位 0.01 nm/°C");

        var deltaRx = ReadUnsignedQ16_16P03(page, 171);
        var deltaTx = ReadUnsignedQ16_16P03(page, 175);
        var averageRx = ReadUnsignedQ16_16P03(page, 179);
        var averageTx = ReadUnsignedQ16_16P03(page, 183);
        AddDelay("Delta RX Max", deltaRx, 171);
        AddDelay("Delta TX Max", deltaTx, 175);
        AddDelay("Average RX Lane 1", averageRx, 179);
        AddDelay("Average TX Lane 1", averageTx, 183);
        Add(fields, "高精度时延/光模块", "RX Statistical Range",
            $"{Math.Max(0, averageRx - deltaRx):0.######} .. {Math.Min(65535.999984741, averageRx + deltaRx):0.######} ns",
            "A2h P03.171-174,179-182", "Avg_Rx ± Delta_Rx_Max");
        Add(fields, "高精度时延/光模块", "TX Statistical Range",
            $"{Math.Max(0, averageTx - deltaTx):0.######} .. {Math.Min(65535.999984741, averageTx + deltaTx):0.######} ns",
            "A2h P03.175-178,183-186", "Avg_Tx ± Delta_Tx_Max");

        var rxPower = measurements.FirstOrDefault(x => x.Id == "rx-power")?.Value;
        if (rxPower is > 0)
        {
            var rxDbm = 10 * Math.Log10(rxPower.Value);
            var correction = Math.Clamp(EvaluatePolynomial(coefficients, rxDbm), -128, 127.999984741);
            var corrected = Math.Clamp(averageRx + correction, 0, 65535.999984741);
            Add(fields, "高精度时延/实时计算", "RX Power", $"{rxDbm:0.###} dBm",
                "A2h.104-105");
            Add(fields, "高精度时延/实时计算", "RX Power Delay Correction",
                $"{correction:0.######} ns", "A2h P03.152-166",
                "四阶多项式 Rx_Pwr_Dly(4..0)，结果按 q8.16 范围限幅。");
            Add(fields, "高精度时延/实时计算", "Corrected Average RX Delay",
                $"{corrected:0.######} ns", "A2h P03.152-166,179-182",
                "Avg_Rx_Lane1 + Rx_Pwr_Dly_Cor");
        }

        var temperature = measurements.FirstOrDefault(x => x.Id == "temperature")?.Value;
        if (temperature.HasValue)
        {
            var detune = Math.Clamp(
                detuneSlopeNmPerC * temperature.Value + detuneOffsetNm,
                -12.8,
                12.799609375);
            Add(fields, "高精度时延/实时计算", "Temperature Wavelength Detune",
                $"{detune:0.######} nm", "A2h P03.167-170",
                "T_Detune_Slope × Temperature + T_Detune_Offset");
        }

        void AddDelay(string name, double value, int offset) =>
            Add(fields, "高精度时延/光模块", name, $"{value:0.######} ns",
                $"A2h P03.{offset}-{offset + 3}", "unsigned q16.16");
    }

    private static void DecodeLoopbackTiming(byte[] page, ICollection<DecodedField> fields)
    {
        AddDelay("Calibration Inaccuracy", 150);
        AddDelay("TX to RX Delay", 155);
        AddDelay("TX to MON Delay", 160);
        AddDelay("RX to MON Delay", 165);

        void AddDelay(string name, int offset) =>
            Add(fields, "高精度时延/回环模块", name,
                $"{ReadUnsignedQ16_16P03(page, offset):0.######} ns",
                $"A2h P03.{offset}-{offset + 3}", "unsigned q16.16");
    }

    private static string DecodeCalibrationDate(byte[] page)
    {
        var yearByte = P03(page, 131);
        var month = P03(page, 132) >> 4;
        var dayCode = ((P03(page, 132) & 0x0F) << 1) | (P03(page, 133) >> 7);
        var sequence = P03(page, 133) & 0x7F;
        if (yearByte == 0xFF || month == 0x0F || dayCode == 0x1F || sequence == 0x7F)
        {
            return "无效/未定义";
        }

        var day = dayCode + 1;
        return month is >= 1 and <= 12 && day is >= 1 and <= 31
            ? $"{2000 + yearByte:D4}-{month:D2}-{day:D2} / #{sequence}"
            : $"编码越界：year={yearByte}, month={month}, day={day}, number={sequence}";
    }

    private static byte P03(byte[] page, int absoluteOffset) => page[absoluteOffset - 128];

    private static ushort ReadUInt16P03(byte[] page, int absoluteOffset) =>
        BinaryPrimitives.ReadUInt16BigEndian(page.AsSpan(absoluteOffset - 128, 2));

    private static short ReadInt16P03(byte[] page, int absoluteOffset) =>
        BinaryPrimitives.ReadInt16BigEndian(page.AsSpan(absoluteOffset - 128, 2));

    private static double ReadUnsignedQ16_16P03(byte[] page, int absoluteOffset) =>
        BinaryPrimitives.ReadUInt32BigEndian(page.AsSpan(absoluteOffset - 128, 4)) / 65536d;

    private static int ReadSigned24P03(byte[] page, int absoluteOffset)
    {
        var index = absoluteOffset - 128;
        var value = (page[index] << 16) | (page[index + 1] << 8) | page[index + 2];
        return (value & 0x800000) != 0 ? value | unchecked((int)0xFF000000) : value;
    }

    private static double EvaluatePolynomial(IReadOnlyList<double> coefficients, double value)
    {
        var result = 0d;
        for (var index = coefficients.Count - 1; index >= 0; index--)
        {
            result = result * value + coefficients[index];
        }

        return result;
    }
}
