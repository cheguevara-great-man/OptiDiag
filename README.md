# OptiDiag

OptiDiag 是一套使用 C#、.NET 10 和 WPF 开发的光模块固件调试上位机。当前版本以纯软件方式运行，内置互相独立的 SFF-8472、CMIS 5.3 与 CMIS 5.4 可读写模拟模块；不需要任何真实 I²C 硬件即可演示协议自动检测、分页/Bank 访问和完整调试流程。

当前产品版本为 **v0.6.0**。本版重点增加系统化原始写入安全模型、主题/字号偏好、只读 GitHub 版本检查、Windows CI 发布验证和面向初学者的完整学习路线。

## 当前功能

- SFF-8472 Rev 12.5a 的 A0h/A2h 身份、兼容性、链路、能力、DDM、阈值、告警、状态和控制字段解析。
- SFF-8024 Rev 4.14 的 Identifier、Connector、Encoding 和 Extended Compliance 公共码表。
- 内部校准与外部校准算法、光功率单位换算、可选激光器温度/TEC 电流。
- A2h 56-74 增强功能声明、状态、信号完整性能力和可写控制位。
- Page 02h 的 RDT、RPM 状态、计数器、控制消息、安全位和用户数据。
- Page 03h 的光模块/回环模块高精度时延、压缩日期、q8.16/q8.8/q16.16 及实时修正公式。
- SFF-8690 Rev 1.5 能力、频率网格、通道/波长、误差、当前状态和锁存状态。
- RPM Page 20h-24h 重组为第二个 SFF-8472 模块完整解码；Page 25h 保留、Page 26h-27h 厂商数据原样显示。
- CMIS 5.3 基础规范的 Lower、Page 00h–04h、10h–1Dh、20h–2Fh、9Fh/A0h–AFh 动态采集、字段解析和访问权限。
- CMIS 5.4 正式版的版本化扩展：Page 0Ch/0Dh、60h/61h/62h/6Dh、系统化页面位图、最多 256 Lane、255 个 NAD Bank、300 GHz、相对功率阈值、固定极性、获取计数器、Media Lane Switching 和 VDM 采样占空比。
- CMIS 可调谐激光器、PRBS/BER/SNR 诊断、时延、Network Path、NAD、Host Lane Switching 和完整 VDM 关联解析。
- CMIS 5.3 基础规范定义的 48 个 CDB 命令目录、逐命令 LPL/EPL 合同、校验、执行状态机和常用回复语义解析。
- CMIS 5.4 的 52 个有效 CDB 命令视图：新增 0005h/0006h 与 010Bh–010Dh，保留 5.3 的 0281h 但在 5.4 中按规范隐藏；支持模块时间、激活影响与 Firmware Load Tag。
- CMIS 固件 Start/LPL 或 EPL 分块写入/Complete/失败 Abort 流程；Run 与 Commit 保持显式操作，不自动复位。
- CMIS 模块温度/电压、最多 32 通道的 Tx 光功率、偏置电流、Rx 光功率、通道告警、输出状态和数据通道状态。
- CMIS Bank Select + Page Select 原子选择、选择值读回校验，以及带 Bank 地址的原始寄存器读写。
- CC_BASE、CC_EXT、CC_DMI、Page 03h CC_CALIB 校验。
- 单页寄存器全景图，按区域和十六进制地址将全部采集字节铺成 16 列内存地图；支持逐格读取和受保护写入。
- 原始寄存器查看、区域和文本过滤，以及集中式安全写入：禁止选择器/只读/混合/保留字段，普通 RW 写前防陈旧检查并写后回读，高风险字段需要 `WRITE XX` 二次确认。
- 周期读取、温度/电压/偏置/TX/RX 趋势曲线和内存历史缓冲。
- 应用日志及逐事务 I²C 日志。
- 带 SHA-256 校验的 `.omodump` 保存、加载和逐字节比较。
- 监控历史和寄存器 CSV 导出。
- Dark、Light、HighContrast 主题和 11–18 号基础字号，偏好安全保存到 LocalAppData。
- 通过 GitHub 官方 API 只读检查版本，不后台下载、不自替换程序；用户从正式 Release 页面下载并核对 SHA-256。
- GitHub Actions 自动执行 Windows Release 构建、全部测试和自包含发布包生成。
- 软件/真实数据源选择；真实数据源的 SFF-8472/SFF-8436/SFF-8636/CMIS 协议探测规则已解耦，具体 USB-I²C 驱动待适配器型号确定后接入。

## 直接运行

最省事的方式是双击仓库根目录的 `run.cmd`。它会优先使用已经安装在用户目录中的 SDK，不依赖终端 PATH，也不会重复安装 .NET。

也可以在终端运行：

```powershell
dotnet run --project .\src\OptiDiag.App\OptiDiag.App.csproj
```

如果新开的终端还没有刷新用户 PATH，可以使用：

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project .\src\OptiDiag.App\OptiDiag.App.csproj
```

不需要安装 Visual Studio。开发只依赖 .NET 10 SDK；仓库也提供 VS Code 任务。自包含发布包中的 `OptiDiag.App.exe` 连 .NET 都不要求目标电脑预装。

构建和测试可以直接双击 `build.cmd`，或运行：

```powershell
dotnet build .\OptiDiag.sln
dotnet test .\OptiDiag.sln
```

## 阅读顺序

完整导航和练习路线见 [docs/README.md](docs/README.md)。首次使用建议优先阅读第 1、2、10、11 篇。

1. [项目方案与阶段边界](docs/00-项目方案与阶段边界.md)
2. [上位机与 I²C 入门](docs/01-上位机与I2C入门.md)
3. [SFF-8472 协议导读](docs/02-SFF-8472协议导读.md)
4. [代码架构与运行流程](docs/03-代码架构与运行流程.md)
5. [扩展协议和适配器](docs/04-扩展与开发指南.md)
6. [测试、调试和发布](docs/05-测试调试与发布.md)
7. [SFF-8472/SFF-8690 实现覆盖清单](docs/06-SFF-8472与SFF-8690实现覆盖清单.md)
8. [CMIS 协议与实现导读](docs/07-CMIS协议与实现导读.md)
9. [CMIS 5.3 覆盖矩阵](docs/08-CMIS-5.3覆盖矩阵.md)
10. [CMIS 5.4 覆盖矩阵与兼容说明](docs/09-CMIS-5.4覆盖矩阵.md)
11. [从零学习与实战手册](docs/10-从零学习与实战手册.md)
12. [安全写入与故障处理](docs/11-安全写入与故障处理.md)
13. [真实硬件接入指南](docs/12-真实硬件接入指南.md)
14. [质量保障与发布流程](docs/13-质量保障与发布流程.md)

## 设计边界

`OptiDiag.I2c.Abstractions` 不引用 WPF 或任何协议；`OptiDiag.Protocols.Sff8472` 与 `OptiDiag.Protocols.Cmis` 并行解析内存快照；`OptiDiag.App` 不直接执行 I²C 事务。原始写入统一经过 `RegisterWritePolicy` 与 `SafeRegisterWriter`。未来的真实 USB-I²C 适配器只需实现 `II2cAdapter`，未来协议只需实现 `IOpticalModuleProtocol`。

规范中的 A0h/A2h 是带读写位的 8 位名称，本项目的事务对象始终使用对应的 7 位地址 `0x50/0x51`。

## 规范来源

- [SNIA SFF-8472 Rev 12.5a](https://members.snia.org/document/dl/25916)
- [SNIA SFF-8690 Rev 1.5](https://members.snia.org/document/dl/25977)
- [SNIA SFF-8024 Rev 4.14](https://members.snia.org/document/dl/26423)
- [OIF CMIS 5.3](https://www.oiforum.com/wp-content/uploads/OIF-CMIS-05.3.pdf)
- [OIF CMIS 5.4](https://www.oiforum.com/wp-content/uploads/OIF-CMIS-05.4.pdf)
- [OIF Implementation Agreements（含 CMIS 5.x 勘误）](https://www.oiforum.com/technical-work/implementation-agreements-ias/)
- [NXP I²C-bus specification UM10204](https://www.nxp.com/docs/en/user-guide/UM10204.pdf)

规范 PDF 不复制进仓库。代码中的枚举、字段解释和换算逻辑用于实现互操作，开发时仍应以正式规范为准。
