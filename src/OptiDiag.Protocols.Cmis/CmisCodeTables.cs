namespace OptiDiag.Protocols.Cmis;

public static class CmisCodeTables
{
    public static string Identifier(byte value) => value switch
    {
        0x18 => "QSFP-DD 8X",
        0x19 => "OSFP 8X",
        0x1E => "QSFP+/QSFP28（CMIS）",
        0x1F => "SFP-DD（CMIS）",
        0x20 => "SFP+/SFP28（CMIS）",
        0x21 => "OSFP-XD（CMIS）",
        0x22 => "OIF-ELSFP（CMIS）",
        0x23 => "CDFP x4 PCIe（CMIS）",
        0x24 => "CDFP x8 PCIe（CMIS）",
        0x25 => "CDFP x16 PCIe（CMIS）",
        0x26 => "XPO",
        _ => $"CMIS Identifier 0x{value:X2}"
    };

    public static string Connector(byte value) => value switch
    {
        0x00 => "未知或未指定",
        0x01 => "SC",
        0x07 => "LC",
        0x0B => "光纤尾纤",
        0x0C => "MPO 1x12",
        0x0D => "MPO 2x16",
        0x21 => "铜缆尾纤",
        0x23 => "无可分离连接器",
        0x24 => "MXC 2x16",
        0x25 => "CS",
        0x26 => "SN",
        0x27 => "MPO 2x12",
        0x28 => "MPO 1x16",
        _ => $"连接器代码 0x{value:X2}"
    };

    public static string MediaType(byte value) => value switch
    {
        0x00 => "未定义",
        0x01 => "多模光纤",
        0x02 => "单模光纤",
        0x03 => "无源/线性铜缆",
        0x04 => "有源铜缆",
        0x05 => "BASE-T",
        >= 0x40 and <= 0x8F => $"厂商自定义媒体类型 0x{value:X2}",
        _ => $"保留媒体类型 0x{value:X2}"
    };

    public static string MediaTechnology(byte value) => value switch
    {
        0x00 => "850 nm VCSEL",
        0x01 => "1310 nm VCSEL",
        0x02 => "1550 nm VCSEL",
        0x03 => "1310 nm FP",
        0x04 => "1310 nm DFB",
        0x05 => "1550 nm DFB",
        0x06 => "1310 nm EML",
        0x07 => "1550 nm EML",
        0x08 => "其他激光器",
        0x09 => "1490 nm DFB",
        0x0A => "无源铜缆（未均衡）",
        0x0B => "无源铜缆（均衡）",
        0x0C => "近端/远端限幅有源均衡",
        0x0D => "远端限幅有源均衡",
        0x0E => "近端限幅有源均衡",
        0x0F => "线性有源铜缆（已废弃）",
        0x10 => "C 波段可调谐激光器",
        0x11 => "L 波段可调谐激光器",
        0x12 => "近端/远端线性有源均衡",
        0x13 => "远端线性有源均衡",
        0x14 => "近端线性有源均衡",
        _ => $"媒体技术代码 0x{value:X2}"
    };

    public static string ModuleState(byte value) => value switch
    {
        1 => "ModuleLowPwr",
        2 => "ModulePwrUp",
        3 => "ModuleReady",
        4 => "ModulePwrDn",
        5 => "ModuleFault",
        _ => $"保留状态 {value}"
    };

    public static string DataPathState(byte value) => value switch
    {
        1 => "DPDeactivated",
        2 => "DPInit",
        3 => "DPDeinit",
        4 => "DPActivated",
        5 => "DPTxTurnOn",
        6 => "DPTxTurnOff",
        7 => "DPInitialized",
        _ => $"保留状态 {value}"
    };
}
