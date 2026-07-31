# OptiDiag

OptiDiag 是一套使用 C#、.NET 10 和 WPF 开发的光模块固件调试上位机。当前版本以纯软件方式运行，内置一个可读写、监控值会随时间变化的 SFF-8472 模拟模块；可以在界面上切换纯 SFF-8472、SFF-8690 可调谐扩展和 RPM 远端第二模块，不需要任何真实 I²C 硬件。

## 当前功能

- SFF-8472 Rev 12.5a 的 A0h/A2h 身份、兼容性、链路、能力、DDM、阈值、告警、状态和控制字段解析。
- SFF-8024 Rev 4.14 的 Identifier、Connector、Encoding 和 Extended Compliance 公共码表。
- 内部校准与外部校准算法、光功率单位换算、可选激光器温度/TEC 电流。
- A2h 56-74 增强功能声明、状态、信号完整性能力和可写控制位。
- Page 02h 的 RDT、RPM 状态、计数器、控制消息、安全位和用户数据。
- Page 03h 的光模块/回环模块高精度时延、压缩日期、q8.16/q8.8/q16.16 及实时修正公式。
- SFF-8690 Rev 1.5 能力、频率网格、通道/波长、误差、当前状态和锁存状态。
- RPM Page 20h-24h 重组为第二个 SFF-8472 模块完整解码；Page 25h 保留、Page 26h-27h 厂商数据原样显示。
- CC_BASE、CC_EXT、CC_DMI、Page 03h CC_CALIB 校验。
- 原始寄存器查看、区域和文本过滤、受保护的单字节写入。
- 周期读取、温度/电压/偏置/TX/RX 趋势曲线和内存历史缓冲。
- 应用日志及逐事务 I²C 日志。
- 带 SHA-256 校验的 `.omodump` 保存、加载和逐字节比较。
- 监控历史和寄存器 CSV 导出。
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

1. [项目方案与阶段边界](docs/00-项目方案与阶段边界.md)
2. [上位机与 I²C 入门](docs/01-上位机与I2C入门.md)
3. [SFF-8472 协议导读](docs/02-SFF-8472协议导读.md)
4. [代码架构与运行流程](docs/03-代码架构与运行流程.md)
5. [扩展协议和适配器](docs/04-扩展与开发指南.md)
6. [测试、调试和发布](docs/05-测试调试与发布.md)
7. [SFF-8472/SFF-8690 实现覆盖清单](docs/06-SFF-8472与SFF-8690实现覆盖清单.md)

## 设计边界

`OptiDiag.I2c.Abstractions` 不引用 WPF 或任何协议；`OptiDiag.Protocols.Sff8472` 只解析内存快照；`OptiDiag.App` 不直接执行 I²C 事务。未来的真实 USB-I²C 适配器只需实现 `II2cAdapter`，未来协议只需实现 `IOpticalModuleProtocol`。

规范中的 A0h/A2h 是带读写位的 8 位名称，本项目的事务对象始终使用对应的 7 位地址 `0x50/0x51`。

## 规范来源

- [SNIA SFF-8472 Rev 12.5a](https://members.snia.org/document/dl/25916)
- [SNIA SFF-8690 Rev 1.5](https://members.snia.org/document/dl/25977)
- [SNIA SFF-8024 Rev 4.14](https://members.snia.org/document/dl/26423)
- [NXP I²C-bus specification UM10204](https://www.nxp.com/docs/en/user-guide/UM10204.pdf)

规范 PDF 不复制进仓库。代码中的枚举、字段解释和换算逻辑用于实现互操作，开发时仍应以正式规范为准。
