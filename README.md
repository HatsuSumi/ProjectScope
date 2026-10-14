# ProjectScope

跨平台桌面项目统计工具，用于分析项目文件、代码规模和资源占用，并生成清晰易读的统计报告。

## 功能

### 项目扫描

- 选择项目根目录并递归扫描文件
- 按文件类型统计文件数量
- 统计代码文件数量、有效代码行数和字符数
- 排除空行及注释后计算代码规模
- 支持输出项目规模统计，包括文件统计、代码规模、资源统计和项目目录树状结构
- 支持扫描进度显示和后台执行
- 支持取消正在进行的扫描

### 文件与资源识别

覆盖常见的软件工程、游戏开发、数字艺术、音频创作、产品设计和数据归档文件，包括：

- JavaScript、TypeScript、Python、C/C++、C#、Java、Go、Rust 等代码
- HTML、CSS、XML、JSON、YAML、TOML、SQL 等文本格式
- 图片、纹理、音频、视频和字体
- Adobe、Blender、Maya、3D 模型和绘画源文件
- Unity、Unreal、Godot、Live2D、Spine 等引擎或工具资源
- Office、PDF、压缩包、游戏封包和移动端应用包

### 扫描选项

- 默认忽略常见生成目录和依赖目录，例如 `.git`、`node_modules`、`build`、`dist`、`bin`、`obj` 等
- 可选择包含隐藏文件和目录
- 可选择取消默认忽略规则
- 可选择统计资源文件
- 可选择输出扩展名细分统计
- 可选择输出文件清单

### 报告与导出

- 桌面界面实时显示扫描日志
- 导出 JSON 机器可读数据
- 导出 Markdown 格式统计结果
- 导出文本日志
- 生成自包含 HTML 可视化报告
- 导出或复制项目目录树
- 支持直接打开生成的 HTML 报告

## 项目结构

```text
ProjectScope/
├── README.md
├── ProjectScope.sln
├── Directory.Build.props
├── Directory.Packages.props
└── src/
    ├── ProjectScope.App/             # Avalonia 桌面应用与 MVVM 界面
    ├── ProjectScope.Core/            # 统计模型、类型识别、注释处理与目录树
    ├── ProjectScope.Infrastructure/  # 文件扫描与报告导出
    └── ProjectScope.Tests/           # 核心统计和扫描测试
```

## 📊 项目规模

### 文件统计

- **总文件数**：25 个
  - C#文件：14 个
  - 其他文件：10 个
  - Markdown文档：1 个

### 代码规模

- **代码总行数**：876 行（不含空行、注释）
  - C#：876 行（100.0%）

- **字符总数**：50,862 字符（不含注释）
  - C#：50,862 字符（100.0%）

### 资源文件

- **资源文件总数**：0 个
- **资源文件总大小**：0 B

> 数据来源：以上所有项目规模数据均基于当前项目目录自动统计生成。

## 🏠 **开发环境**

本项目为居家开发，没有工作，也没有收入。在孤独和抑郁症的陪伴下，独自完成项目的设计、开发与维护。

## 开发

需要安装 .NET 8 SDK 或兼容的更高版本 SDK。

构建解决方案：

```bash
dotnet build ProjectScope.sln
```

运行桌面应用：

```bash
dotnet run --project src/ProjectScope.App/ProjectScope.App.csproj
```

运行测试：

```bash
dotnet test ProjectScope.sln
```

## 技术栈

- C#
- .NET 8
- Avalonia UI
- MVVM
- CommunityToolkit.Mvvm

## 许可证

暂未确定。
