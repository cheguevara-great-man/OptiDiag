using System.Buffers.Binary;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Cmis;

internal static class CmisCdbDecoder
{
    public static void Decode(ModuleDump dump, List<DecodedField> fields)
    {
        var sink = new CmisFieldSink(fields);
        foreach (var region in dump.Regions
                     .Where(x => x.Page == 0x9F && x.Data.Length >= CmisCdbCodec.PageSize)
                     .OrderBy(x => x.Bank))
        {
            DecodeInstance(region, sink);
        }
    }

    private static void DecodeInstance(MemoryRegionData region, CmisFieldSink sink)
    {
        var data = region.Data;
        var instance = (region.Bank ?? 0) + 1;
        var category = $"CDB 实例 {instance}";
        var commandId = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(0, 2));
        var definition = CmisCdbCommandCatalog.Find(commandId);
        var commandCheck = CmisCdbCodec.ComputeCommandCheckCode(data);
        var replyLength = data[6];

        sink.Add(category, "CMDID",
            definition is null ? $"0x{commandId:X4}（保留/自定义）" : $"0x{commandId:X4} {definition.Title}",
            CmisDecoderHelpers.Source(0x9F, 128, region.Bank),
            definition is null ? "未由 CMIS 5.3 基础规范定义。" : $"{definition.Group}，CMIS {definition.Section}");
        sink.Add(category, "EPLLength", BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2, 2)),
            CmisDecoderHelpers.Source(0x9F, 130, region.Bank), unit: "bytes");
        sink.Add(category, "LPLLength", data[4],
            CmisDecoderHelpers.Source(0x9F, 132, region.Bank), unit: "bytes");
        sink.Add(category, "CdbChkCode",
            $"存储 0x{data[5]:X2} / 计算 0x{commandCheck:X2} / {(data[5] == commandCheck ? "有效" : "无效")}",
            CmisDecoderHelpers.Source(0x9F, 133, region.Bank));
        sink.Add(category, "RPLLength", FormatReplyLength(replyLength),
            CmisDecoderHelpers.Source(0x9F, 134, region.Bank));
        sink.Add(category, "RPLChkCode", $"0x{data[7]:X2}",
            CmisDecoderHelpers.Source(0x9F, 135, region.Bank));

        if (replyLength <= CmisCdbCodec.LocalPayloadCapacity)
        {
            var reply = CmisCdbCodec.DecodeReply(data);
            sink.Hex(category, "回复 LPL", reply.LocalPayload,
                CmisDecoderHelpers.Source(0x9F, 136, region.Bank),
                reply.CheckCodeValid ? "回复校验码有效。" : "回复校验码无效。");
        }
    }

    private static string FormatReplyLength(byte value) => value switch
    {
        <= CmisCdbCodec.LocalPayloadCapacity => $"{value} bytes LPL",
        >= 240 => $"{value - 239} EPL page(s)",
        _ => $"0x{value:X2}（保留编码）"
    };
}
