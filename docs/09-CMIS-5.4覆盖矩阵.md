# CMIS 5.4 覆盖矩阵与兼容说明

## 1. 版本结论

OIF-CMIS-05.4 是 OIF 于 2026 年 5 月发布的正式 Implementation Agreement。项目保留 CMIS 5.3，不用 5.4 替换或删除它。

OIF 的兼容性表述可以概括为：5.3 合规实现原则上也是 5.4 合规实现；可能的例外是没有按 CMIS 5.x Errata 修正的旧 Network Path。这里的“兼容”不表示 5.3 模块会凭空拥有 5.4 新功能，所以软件仍必须按 Lower byte 1 的 Revision 和能力位区分。

## 2. 5.4 内存映射增量

| 位置 | 5.4 内容 | 软件状态 |
|---|---|---|
| Lower 8.3 / 31.3 | Abnormal Firmware Flag / Mask | 语义解析、告警与写保护 |
| Lower 61.7-4 | SFF-8024 Heatsink Type | 完整公共码值解析 |
| Page 01h 142/173/174/175/252 | 32 Lane Bank、0Ch/0Dh/60h–62h、U8 NAD Bank、6Dh | 版本化解析和动态采集 |
| Page 04h | 300 GHz 与相对 Tx 功率阈值能力 | 公式、范围和能力位解析 |
| Page 0Ch | 256-bit Supported Page Map、命名功能、NA/FW 行为 | 语义解析；位图驱动后续采集 |
| Page 0Dh | 固件能力、A/B 状态、A/B/Fixed Version Descriptor | 语义解析；保留 RW 控制区 |
| Page 12h | 相对阈值 Enable 与四个 ±0.5 dB 步进阈值 | 语义解析和受保护读写 |
| Page 1Ch | Host/Media Interface GID | 12-bit UID 组成信息解析 |
| Page 60h | 固定极性状态、四组 Counter Reset | 语义解析；Reset 标为 WO/SC |
| Page 61h | Tx/Rx Lane/DP Acquisition Counters | 32 Bank × 8 Lane、饱和 U16 |
| Page 62h | 每 Lane Tx Output Power Threshold Quad | S16 × 0.01 dBm |
| Page 6Dh | Media Lane Switching | 配置、Enable、Commit、逐 Lane Result/Status |
| Page 2Fh | Monitoring Duty Cycle | 能力、0%–100% 控制和 Freeze 握手显示 |

Lane-banked 页的采集计划覆盖 32 个 Bank，即最多 256 Lane。Page 1Ch 覆盖 255 个 NAD Bank，即最多 3825 个 Normalized Application Descriptor。未声明的 Bank/Page 会在读取前跳过。

## 3. 5.4 CDB 增量

| CMD | 名称 | 实现 |
|---|---|---|
| 0005h | Get Module Time | 空请求；S64 POSIX 纳秒及 UTC 解析 |
| 0006h | Set Module Time | S64 时间 + IsIncrement；新时间解析 |
| 0050h | Get Application Attributes | 保留单应用 LPL；增加 15 个应用的 EPL 批量回复 |
| 0051h | Get Interface Code Description | 修正 Name/Description 偏移；12-bit UID、精确 bits/symbol、最小 Grid |
| 010Bh | Check Firmware Activation Options | Traffic/Configuration Impact 位解析 |
| 010Ch | Store Firmware Load Tag | Bank、Clear、64-byte Tag 合同和服务方法 |
| 010Dh | Retrieve Firmware Load Tag | Bank 与 64-byte Tag 回复解析 |
| 0212h–0217h、0233h | 多于 32 Lane | LanesGroupIndex 合同与 20-byte 请求保持；回复支持 LPL/EPL |

目录保存 53 个跨版本命令定义。对 5.3 显示 48 个；对 5.4 显示 52 个，因为新增 5 个并按规范移除 0281h。0280h 保留并标为 obsolescent。

## 4. 模拟和测试

软件顶部提供互相独立的“模拟 CMIS 5.3 模块”和“模拟 CMIS 5.4 模块”。两者经过相同的 I²C 抽象、自动协议检测、动态采集和解码流程，不是把版本号作为界面假数据。

自动测试覆盖：

- 5.3/5.4 分别检测并将真实 Revision 写入 Dump。
- 5.4 新 Page 全部按能力采集，5.3 不访问这些 Reserved Page。
- 32 Lane Bank escape encoding 与 255 NAD Bank 解码。
- 300 GHz、Heatsink、命名功能、计数器、阈值、Media Lane Switching 和访问权限。
- 5.3/5.4 CDB 目录数量及移除/新增规则。
- Module Time 和 Firmware Load Tag 在 5.4 模拟器上的端到端执行。
- 0051h 修正偏移与精确字段。

## 5. 仍需真实硬件验证的边界

基础规范的软件语义和模拟流程已经实现，但“对所有厂商硬件完全兼容”必须在拿到以下资料后才能验证：具体 USB-I²C 适配器及驱动、至少一份去敏 5.3 Dump 和 5.4 Dump、模块声明的可选能力、超时/读清除/写后生效行为。C-CMIS、CMIS-LT、CMIS-FF、CMIS-VCS、Resource Module 和公司私有协议仍属于各自独立文档，不能用 CMIS 5.4 基础文档猜测。

## 6. 官方依据

- [OIF CMIS 5.4 PDF](https://www.oiforum.com/wp-content/uploads/OIF-CMIS-05.4.pdf)
- [OIF CMIS 5.4 Changebars](https://www.oiforum.com/wp-content/uploads/OIF-CMIS-05.4-changebars.pdf)
- [OIF Implementation Agreements](https://www.oiforum.com/technical-work/implementation-agreements-ias/)
- [OIF CMIS 5.x Errata](https://www.oiforum.com/technical-work/implementation-agreements-ias/)

规范 PDF 不提交到仓库。覆盖矩阵描述的是本项目的软件实现和测试边界，不能替代正式规范或硬件互操作认证。
