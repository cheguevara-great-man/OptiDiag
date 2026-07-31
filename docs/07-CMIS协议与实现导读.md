# CMIS 5.3 协议与实现导读

## 1. CMIS 与 SFF-8472 的关系

CMIS（Common Management Interface Specification）面向 QSFP-DD、OSFP、SFP-DD 等多通道模块。它和 SFF-8472 是并行的两套管理协议，不是把 SFF-8472 页面继续向后加。软件先读取 `0x50` 的 Lower Memory，通过 Identifier、版本和内存模型判断协议，再选择独立的采集计划和解码器。

本项目的基线是 OIF-CMIS-05.3（2024 年 9 月）及 SFF-8024 Rev 4.14。CMIS 4.x/5.x 模块也可被识别，但界面会显示模块实际声明的版本；字段解释以 5.3 为准。

## 2. 内存、Page 和 Bank

CMIS 使用 7 位 I²C 地址 `0x50`。当前可见窗口始终为 256 字节：

| 地址 | 内容 |
|---|---|
| 0–127 | Lower Memory |
| 128–255 | 当前选择的 Upper Page |
| 126 | Bank Select |
| 127 | Page Select |

Lower byte 2 bit 7 为 `1` 时是平面内存，只能读 Lower 和 Page 00h；为 `0` 时是分页内存。任意 Bank/Page 切换必须用一次连续写完成：

```text
START + 0x50(W) + 126 + Bank + Page + STOP
```

`ModuleMemoryService` 随后读回 126–127，确认模块接受了选择。所有选择、读取和写入都经过同一个互斥门，避免周期读取与手动操作互相改页。

Bank 0–3 分别代表 Lane 1–8、9–16、17–24、25–32。Page 1Ch 使用单独的 NAD Block 数量；Page 9Fh/A0h–AFh 的 Bank 代表 CDB 实例，而不是通道组。

## 3. 软件怎样决定读哪些页

软件不会盲读 256 个 Page。它先读 Lower、00h、01h、02h，再根据 Page 01h 的能力位和 Bank 数量动态决定后续页面：

- 03h：用户 NV RAM。
- 04h、12h：可调谐激光器。
- 10h–11h：通道控制与状态，分页模块必需。
- 13h–14h：模块性能诊断、PRBS、BER、SNR、计数器和环回。
- 15h：每通道 Tx/Rx 时延。
- 16h–17h：Network Path 控制、状态、标志和屏蔽。
- 18h–19h、1Ch：NAD 与方向独立的数据通道扩展。
- 1Dh：Host Lane Switching。
- 20h–2Fh：VDM 描述符、样本、阈值、标志、屏蔽和冻结控制。
- 9Fh/A0h–AFh：CDB 的 LPL/EPL。

可选页读取失败只形成采集警告，不会让整个模块快照失效。Reserved Page 从不访问。

## 4. 主要数值类型

CMIS 默认使用大端多字节数值，明确标注为小端的诊断计数器和 SNR 除外。

| 类型/监控量 | 换算 |
|---|---|
| 模块温度 S16 | `raw / 256 °C` |
| 供电电压 U16 | `raw × 0.0001 V` |
| Tx/Rx 光功率 U16 | `raw × 0.0001 mW` |
| 偏置电流 U16 | `raw × 0.002 mA × 广告倍率` |
| 波长 U16 | `raw × 0.05 nm` |
| 频率 U32 | 按字段单位换算为 GHz/THz |
| F16 | IEEE-754 binary16，常用于 BER/FERC |
| U48/U64 | RMON、FEC 和诊断计数器 |
| VDM | 根据 Observable Type 选择 S16/U16/F16 和比例 |

告警标志不会用当前数值重新计算。真实模块可能实现锁存、滞回和读清除，所以软件同时展示原始标志和测量值。

## 5. Page 10h–1Dh

这些页面覆盖运行中的多通道控制：

- Page 10h：直接控制、两个 Staged Control Set、Apply 命令、信号完整性参数和 Flag Mask。
- Page 11h：Data Path State、输出状态、锁存标志、Tx/Rx 功率、偏置、配置结果、活动控制集和 Lane Mapping。
- Page 12h：网格、Channel Number、Fine Tuning、当前激光频率、目标功率、状态、标志和屏蔽。
- Page 13h/14h：PRBS Pattern Generator/Checker、时钟源、门控、环回、用户 Pattern、BER、SNR、错误与总比特计数。
- Page 15h：每通道 Tx/Rx Latency。
- Page 16h/17h：Network Path 的 staged configuration、Apply、状态、能力、标志和屏蔽。
- Page 18h/19h：NAD 索引和基础规范定义的扩展状态；VCS Parameter Space 保持原始显示。
- Page 1Ch：最多 15 个 Bank、每 Bank 15 个 Normalized Application Descriptor。
- Page 1Dh：Host Lane Redirection 的配置、Commit、结果和活动映射。

寄存器全景使用逐字节访问表区分 RO、RO/COR、RW、RW/SC、WO、WO/SC、Mixed、Reserved 和 Vendor Specific。Mixed、RO/COR 与 Reserved 不允许直接从通用单字节写入口修改；需要状态机的命令通过专用工具执行。

## 6. VDM

VDM 由 Page 2Fh 广告组数，再按组使用：

```text
20h–23h  Observable Descriptor
24h–27h  Sample
28h–2Bh  Threshold Quad
2Ch      Threshold Crossing Flag（RO/COR）
2Dh      Mask
2Fh      广告、Fine Interval、Freeze/Power Saving 控制
```

解码器把 Descriptor 中的 Observable Type、Resource 和 Threshold Set 关联到样本、阈值、标志和屏蔽。支持基础规范定义的 Laser Age、TEC、频率误差、激光温度、SNR、PAM4 LTP、BER、FERC、SEW、辅助电压和 ELS 输入功率；Custom/Restricted 类型保留类型号和原始值。

## 7. CDB

CDB 是 CMIS 的命令/回复通道。Page 9Fh 包含 6 字节命令头、2 字节回复头和 120 字节 LPL；A0h–AFh 提供最多 2048 字节 EPL。

软件实现的顺序是：

```text
写 EPL（若有）
  -> 写 Page 9Fh 的长度、校验和 LPL，但不碰 CMDID
  -> 最后写 128–129 的 CMDID 触发执行
  -> 轮询 Lower 37/38 的 CdbStatus
  -> 读取 Page 9Fh 和所需 EPL
  -> 校验 RPLChkCode
  -> 按命令结构化解析回复
```

CMIS 5.3 基础规范明确定义的 48 个命令都进入命令目录，并有 LPL/EPL 长度合同和请求/回复结构说明。能力、固件信息、应用属性、接口描述、PM、RMON、FEC、温度直方图和安全回复可语义化显示；没有固定回复体的命令显示状态和原始回复。

“CMIS CDB”页允许选择命令、实例并输入 LPL/EPL HEX。非法长度会在 I²C 写入前被拒绝。

## 8. 固件下载

固件工具实现 CMIS 9.7 的状态机：

```text
0101 Start Firmware Download
  -> 0103 LPL 或 0104 EPL 分块写入
  -> 0107 Complete Firmware Download
```

任何分块失败时软件尝试发送 `0102 Abort`。传输完成后不会自动执行 `0109 Run Image` 或 `010A Commit Image`，避免未经验证就复位模块或改变下次启动镜像。接真实模块时必须先读取 `0041 Firmware Management Features`，按模块声明选择 LPL/EPL、块大小、擦除值和超时。

## 9. 基础规范与外部补充规范

OIF-CMIS-05.3 的 Table 8-1 把一些地址留给其他文档：

- Page 05h：CMIS-FF。
- Page 06h–0Bh、1Ah–1Bh：Resource Module 或 CMIS-LT。
- Page 30h–4Fh：C-CMIS。
- Page 50h–5Fh：CMIS-LT。
- Page 18h/19h 的 VCS Parameter Space、CMD 4000h–40FFh：CMIS-VCS。

基础 CMIS 文档只声明它们的地址和能力位，并不定义全部字段。软件会在能力声明存在时采集原始字节、标明所属补充规范，并阻止通用写入口猜测其语义。这不是遗漏 CMIS 5.3，而是明确的规范边界；要做这些字段的语义解析，需要对应补充规范。

## 10. 代码运行流程

```text
数据源
  -> ProtocolDetectionService
  -> ModuleSession
  -> CmisMemoryMap 动态采集计划
  -> ModuleMemoryService 原子 Bank/Page 访问
  -> CmisStaticDecoder / CmisPagedDecoder / CmisVdmDecoder / CmisCdbDecoder
  -> DecodedModule
  -> WPF、曲线、日志、Dump、CSV
```

CDB 发送使用 `ICmisCdbMemoryAccess` 边界，协议状态机不引用 WPF、USB 或具体 I²C 适配器。模拟器也不引用 CMIS 解码项目。

## 11. 在软件里试用

1. 顶部来源选择“模拟 CMIS 5.3 模块”。
2. 连接并立即读取。
3. 在“协议解析”查看静态、通道、诊断、VDM 与 CDB 字段。
4. 在“寄存器全景”查看所有已采集页；双击可读取，允许写的字段可解锁写入。
5. 在“CMIS CDB”执行 Query Status（LPL 为 `00 00`）或能力查询。
6. 固件下载先用模拟文件验证流程；真实模块上不要在不了解镜像格式时尝试。
7. 保存 `.omodump`，用第二次快照比较状态和控制位变化。

完整逐页边界见 [CMIS 5.3 覆盖矩阵](08-CMIS-5.3覆盖矩阵.md)。

## 12. 官方资料

- [OIF CMIS 5.3 PDF](https://www.oiforum.com/wp-content/uploads/OIF-CMIS-05.3.pdf)
- [OIF Implementation Agreements 与 CMIS 勘误](https://www.oiforum.com/technical-work/implementation-agreements-ias/)
- [SNIA SFF-8024 Rev 4.14](https://members.snia.org/document/dl/26423)

规范 PDF 不提交到仓库；实现和文档不能替代正式规范。
