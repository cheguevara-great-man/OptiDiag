# CMIS 协议与实现导读

## 1. CMIS 是什么

CMIS（Common Management Interface Specification）是一套面向多通道可插拔模块的管理接口。QSFP-DD、OSFP、部分 QSFP/SFP-DD 和后续形态可以采用 CMIS。它与 SFF-8472 是并行协议，不是 SFF-8472 的附加页面；自动检测后必须进入不同的采集计划和解析器。

本项目的实现基线是 OIF-CMIS-05.3（2024 年 9 月），同时参考 OIF 发布的 CMIS 5.x 勘误。代码接受 CMIS 4.x/5.x 模块常见的 Identifier，界面显示模块自己声明的版本；遇到未知枚举时保留原始十六进制值。

## 2. I²C 地址和内存结构

CMIS 使用 7 位 I²C 地址 `0x50`。每次可见 256 字节：

| 地址 | 内容 |
|---|---|
| 0–127 | Lower Memory，始终可见 |
| 128–255 | 当前选择的 Upper Page |
| 126 | Bank Select |
| 127 | Page Select |

Lower byte 2 bit 7 区分两种模型：

- `1`：平面内存，只存在 Lower 和 Upper Page 00h，不应访问页面选择寄存器。
- `0`：分页内存，可根据能力声明访问 Page 01h、02h、10h、11h 等。

CMIS 要求任意 Bank/Page 切换通过一次连续 WRITE 写入 byte 126 和 127。本项目发送：

```text
START + 0x50(W) + 126 + Bank + Page + STOP
```

随后读回 126–127 校验选择是否被模块接受。页面操作被 `ModuleMemoryService` 的互斥锁串行化，因此周期读取和手动寄存器操作不会互相改错页面。

## 3. 当前采集页面

| 区域 | 用途 | 访问 |
|---|---|---|
| Lower 0–127 | 版本、内存模型、模块状态、实时温度/电压、标志、应用描述符、控制和密码入口 | 混合 |
| Page 00h | Identifier 副本、厂商、OUI、料号、版本、序列号、日期、CLEI、功耗、连接器和媒体技术 | 静态只读/厂商 |
| Page 01h | 固件/硬件版本、链路长度、波长、支持页面、银行数、监控和控制能力 | 只读 |
| Page 02h | 模块级和通道级高/低告警、预警阈值 | 只读 |
| Page 03h | 主机可读写用户 EEPROM | 读写 |
| Banked Page 10h | 通道控制、暂存配置和标志屏蔽 | 混合读写 |
| Banked Page 11h | 数据通道状态、通道标志、Tx/Rx 光功率、偏置电流和输出状态 | 只读/读清除 |

Page 01h 的银行能力决定采集 1、2 或 4 个 Bank，每个 Bank 表示 8 个通道，因此通用解析器可处理最多 32 通道。内置 QSFP-DD 模拟模块声明 1 个 Bank、8 个通道。

## 4. 数值换算

所有多字节标准数值按大端读取。

| 监控量 | 原始类型 | 换算 |
|---|---|---|
| 模块温度 | S16 | `raw / 256 °C` |
| 3.3 V 电压 | U16 | `raw × 100 µV` |
| Tx 光功率 | U16 | `raw × 0.1 µW` |
| Rx 光功率 | U16 | `raw × 0.1 µW` |
| Tx 偏置电流 | U16 | `raw × 2 µA × Page01h.160 的倍率` |
| 标称波长 | U16 | `raw × 0.05 nm` |
| 波长容差 | U16 | `raw × 0.005 nm` |

光功率卡片同时显示 mW 和 dBm。阈值直接附加到对应测量值，曲线默认显示模块温度、电压和第 1 通道的偏置、Tx、Rx。

## 5. 告警与状态

Lower byte 8–11 保存模块级故障和阈值越界标志。Page 11h byte 134–153 按通道保存数据通道状态变化、Tx/Rx LOS、CDR 失锁、Tx 内部故障、光功率及偏置高低告警/预警等锁存标志。

这些标志不能仅用当前测量值重新计算，因为真实模块可能实现滞回、锁存或读清除。软件同时展示“标志位”和“当前数值”，不把两者混为一谈。

Page 11h 还会解码：

- 每个 Host Lane 的 Data Path State。
- 每个 Rx Host Lane 的输出有效性。
- 每个 Tx Media Lane 的输出有效性。

## 6. 代码运行流程

```text
数据源选择
  -> SwitchableI2cAdapter 切换独立模拟模块
  -> ProtocolDetectionService 读取 Lower 标头
  -> ModuleSession 选择 CmisProtocol
  -> CmisProtocol.CapturePlan + ShouldCaptureRegion
  -> ModuleMemoryService 原子选择 Bank/Page 并采集
  -> CmisProtocol.Decode
  -> 通用 DecodedModule
  -> WPF 页面、曲线、日志、Dump 和 CSV
```

协议项目不引用 WPF 或模拟器，模拟器也不引用 CMIS 解析器。将来接入真实 USB-I²C 适配器时，CMIS 解码代码不需要改写。

## 7. 如何在软件里试用

1. 在顶部“来源”选择“模拟 CMIS 5.3 模块”。
2. 点击“连接 / 断开”；软件自动识别 Identifier 和 CMIS Revision。
3. 在“概览”查看身份及 8 通道监控。
4. 在“阈值”“告警与状态”“协议解析”查看语义结果。
5. 在“寄存器全景”双击任意字节读取；对允许写入的 Page 03h/Page 10h 字节可解锁后写入。
6. 使用周期读取、CSV 导出和 `.omodump` 保存/比较验证固件行为。

原始地址会显示成 `0x50 B00 P11:9A`，表示 7 位设备地址 0x50、Bank 00h、Page 11h、byte 154（9Ah）。

## 8. 当前边界

本版本实现可用于基础 CMIS 模块调试的核心页面。以下可选功能尚未做结构化采集与语义解析：

- Page 04h 可调谐激光器能力、Page 12h 动态调谐。
- Page 05h CMIS-FF、Page 13h/14h 诊断页面。
- Page 15h 固件管理、Page 16h/17h Network Path。
- Page 20h–2Fh VDM。
- Page 30h–4Fh 相干模块扩展。
- Page 50h–53h Link Training。
- CDB 命令体、密码策略和厂商自定义页的业务语义。

这些页面不会被猜测为标准字段。后续应依据模块能力位按需增加采集计划、访问属性、换算和测试；真实模块兼容性验证仍需要去敏 Dump。

## 9. 官方资料

- [OIF CMIS 5.3 PDF](https://www.oiforum.com/wp-content/uploads/OIF-CMIS-05.3.pdf)
- [OIF Implementation Agreements 与 CMIS 勘误](https://www.oiforum.com/technical-work/implementation-agreements-ias/)
- [SNIA SFF-8024 Rev 4.14](https://members.snia.org/document/dl/26423)
