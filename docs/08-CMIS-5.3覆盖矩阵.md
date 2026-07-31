# CMIS 5.3 覆盖矩阵

## 1. “完整支持”的判定

本项目把“CMIS 5.3 基础规范完整支持”定义为：

1. Table 8-1 的每个 Page 范围都有明确分类。
2. 基础规范定义且模块声明支持的 Page 可以采集。
3. 有标准语义的字段被解析；未分配、Reserved、Custom 保留原始字节。
4. 逐字节访问权限可阻止明显危险的通用写入。
5. Chapter 9 定义的全部 CDB 命令可正确封包、触发、轮询、收取和校验。
6. 外部补充规范不会冒充 CMIS 5.3 基础字段。

这不等于已经对所有厂商模块做硬件互操作认证。真实兼容性仍需适配器、模块和去敏 Dump。

状态含义：

- **语义支持**：按 CMIS 5.3 解码/控制。
- **原始支持**：可按能力采集并显示，但语义属于外部文档或厂商。
- **禁止访问**：CMIS Reserved Page，不加入采集计划。

## 2. Page 覆盖

| Page | CMIS 5.3 用途 | 状态 | 实现 |
|---|---|---|---|
| Lower | 状态、标志、监控、控制、应用描述符、密码、Bank/Page | 语义支持 | 静态字段、实时监控、Flags/Masks、访问权限 |
| 00h | Administrative Information | 语义支持 | 身份、OUI、PN/SN、日期、功耗、线缆、连接器、媒体技术、校验 |
| 01h | Advertising | 语义支持 | 版本、链路、波长、页面/Bank、时序、监控、控制、CDB、NAD、HLS/LT |
| 02h | Thresholds | 语义支持 | 模块、Aux/Custom、Tx/Rx/Bias 阈值与单位 |
| 03h | User NV RAM | 语义支持 | 原始 128 字节、安全 RW |
| 04h | Laser Capabilities | 语义支持 | Grid、Channel/Frequency 范围、Fine Tuning、目标功率、校验 |
| 05h | CMIS-FF | 原始支持 | 能力存在时采集，标为外部 CMIS-FF |
| 06h–07h | Resource Module | 原始支持 | Resource Module 声明存在时采集 |
| 08h–0Bh | CMIS-LT | 原始支持 | LT 声明存在时采集 |
| 0Ch–0Fh | Reserved | 禁止访问 | 不生成采集请求 |
| 10h | Lane/Data Path Configuration | 语义支持 | Direct Control、SCS0/1、Apply、SI、Masks |
| 11h | Lane/Data Path Status | 语义支持 | State、Flags、Monitors、Config Status、Active Set、Mapping |
| 12h | Tunable Laser Control/Status | 语义支持 | Grid/Channel/Fine Tune/Frequency/Power/Flags/Masks |
| 13h | Performance Diagnostics Control | 语义支持 | PRBS、时钟、门控、环回、Pattern、Scratchpad、Masks |
| 14h | Performance Diagnostics Results | 语义支持 | BER F16、SNR、小端 U64 Counters、PSL、RO/COR Flags |
| 15h | Timing Characteristics | 语义支持 | Lane 1–32 的 Tx/Rx Latency |
| 16h | Network Path Control/Status | 语义支持 | NP SCS、Apply、Status、State、Timing、Advertising |
| 17h | Network Path Flags/Masks | 语义支持 | NP State Changed Flag/Mask |
| 18h | Configuration Extensions | 混合 | NAD 索引语义支持；VCS 参数区原始支持 |
| 19h | Status Extensions | 混合 | DP/NAD 状态语义支持；VCS 参数区原始支持 |
| 1Ah–1Bh | Resource Module | 原始支持 | 声明存在时按 Bank 采集 |
| 1Ch | Normalized Applications | 语义支持 | 最多 15 Bank × 15 描述符 |
| 1Dh | Host Lane Switching | 语义支持 | 配置、Enable、Commit、Result、Active Mapping |
| 1Eh–1Fh | Custom lane-banked | 原始/厂商 | 不自动猜测或采集，Dump 中可保留 |
| 20h–23h | VDM Descriptor Groups | 语义支持 | Type、Resource、Threshold Set |
| 24h–27h | VDM Samples | 语义支持 | 按 Observable Type 换算 |
| 28h–2Bh | VDM Thresholds | 语义支持 | 与 Descriptor/Threshold Set 关联 |
| 2Ch | VDM Flags | 语义支持 | 256 个 Flag Quad，RO/COR |
| 2Dh | VDM Masks | 语义支持 | 256 个 Mask Quad，RW |
| 2Eh | Reserved | 禁止访问 | 不生成采集请求 |
| 2Fh | VDM Advertising/Control | 语义支持 | 组数、Fine Interval、Freeze、Power Saving |
| 30h–4Fh | C-CMIS | 原始支持 | C-CMIS 声明存在时采集，语义需 C-CMIS |
| 50h–5Fh | CMIS-LT | 原始支持 | LT 声明存在时采集，语义需 CMIS-LT |
| 60h–9Eh | Reserved | 禁止访问 | 不生成采集请求 |
| 9Fh | CDB Message | 语义支持 | Header、LPL、校验、实例、命令状态机 |
| A0h–AFh | CDB EPL | 语义支持 | 按广告页数和命令长度读写，最多 2048 字节 |
| B0h–FFh | Custom | 原始/厂商 | 不自动猜测或采集，Dump 中可保留 |

## 3. CDB 覆盖

| 命令组 | CMD ID | 状态 |
|---|---|---|
| Module | 0000h、0001h、0002h、0004h | 封包、长度合同、执行；Query Status 回复解析 |
| Capability Inquiry | 0040h–0045h、0050h、0051h | 封包、执行、支持位/属性/接口描述解析 |
| Firmware | 0100h–010Ah | 全部命令合同；Start/Write/Complete/Abort 下载状态机；Run/Commit 显式分离 |
| Performance Monitoring | 0200h、0201h、0210h–0217h、0220h、0230h–0233h | 合同、执行、PM Record/RMON/FEC 回复解析 |
| Data Recording | 0280h、0281h、0290h | 合同、执行、温度直方图解析 |
| Diagnostics | 0380h | 规范保留的空命令体流程 |
| Security | 0400h–0405h | LPL/EPL、分段索引、状态、证书/摘要/签名长度与内容 |

`0300h–037Fh` 在 CMIS 5.3 中为将来 BERT 命令保留，现有 BER/BERT 功能位于 Page 13h/14h；`4000h–40FFh` 的 VCS 命令属于 CMIS-VCS 外部补充规范。

## 4. 自动测试对应关系

`tests/OptiDiag.Protocols.Cmis.Tests` 验证：

- CMIS 自动检测与 SFF-8472 并行切换。
- 256 个 Page 地址均且仅有一个分类。
- Reserved Page 不进入采集计划。
- Optional Page、VDM、CDB 按能力动态采集。
- 代表性的静态、可调谐、诊断、时延、Network Path、NAD、HLS、VDM 字段。
- RO/COR、WO/SC、Mixed、Reserved 等写保护。
- 48 个基础 CDB 命令与 48 个 payload 合同一一对应。
- CDB Check Code、Query Status 执行和回复校验。
- 固件镜像分块、Complete 与失败时 Abort 路径的核心状态机。

## 5. 尚需真实资料才能完成的验证

- 具体 USB-I²C 适配器驱动与事务长度限制。
- 不同厂商对可选能力、超时、读清除和写后生效时序的实现差异。
- 真实 CMIS 4.x/5.x 去敏 Dump 回归。
- C-CMIS、CMIS-LT、CMIS-FF、CMIS-VCS、Resource Module 的正式补充规范语义。
- 公司自定义 Page、CDB 命令、密码和解锁流程。

这些项目不影响纯软件模拟和 CMIS 5.3 基础规范实现，但决定真实硬件能否称为经过互操作验证。
