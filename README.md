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
- 支持将当前扫描配置保存为项目根目录下的 `.projectscope.json`
- 支持从任意 `.projectscope.json` 文件加载扫描配置，不会自动开始扫描

### 文件与资源识别

覆盖常见的软件工程、游戏开发、数字艺术、音频创作、产品设计和数据归档文件，包括：

- JavaScript、TypeScript、Python、C/C++、C#、Java、Go、Rust 等代码
- HTML、CSS、XML、JSON、YAML、TOML、SQL 等文本格式
- 图片、纹理、音频、视频和字体
- Adobe、Blender、Maya、3D 模型和绘画源文件
- Unity、Unreal、Godot、Live2D、Spine 等引擎或工具资源
- Office、PDF、压缩包、游戏封包和移动端应用包

### 扫描选项

- 可选择包含隐藏文件和目录
- 可选择统计资源文件
- 每次扫描自动生成扩展名细分统计和文件清单
- “忽略目录”提供常见生成目录复选框，默认勾选，可逐项取消；目录名匹配项目内各层级的同名目录
- 自定义忽略路径每行一条，例如 `/A/B/example.xxx`、`/A/C/example.xxx` 或 `/A/cache/`；路径相对于项目根目录，支持文件和整个目录，也支持反斜杠
- 正则规则以 `regex:` 开头，可与普通路径混用；匹配对象为带 `/` 前缀、使用 `/` 分隔的项目相对路径。例如 `regex:^/A/(B|C)/example\.xxx$` 匹配这两个目录中的指定文件，`regex:\.g\.dart$` 匹配生成的 Dart 文件。正则匹配到目录时会跳过整个目录；Windows 下匹配不区分大小写

### 报告与导出

- 桌面界面实时显示扫描日志
- 导出 JSON 机器可读数据
- 导出 Markdown 格式统计结果
- 导出 TXT 文本日志
- 生成自包含 HTML 可视化报告，图表运行时内嵌，无需网络连接
- 导出或复制项目目录树
- 支持直接打开生成的 HTML 报告

### 配置文件

项目级扫描配置使用 `.projectscope.json` 保存，包括项目根目录、忽略目录、自定义忽略规则、资源统计、隐藏文件和空行/注释选项。配置文件使用 JSON 格式，便于跨平台携带、查看和版本管理。

## 项目结构

```text
ProjectScope/
├── README.md
├── ProjectScope.sln
├── Directory.Build.props
├── Directory.Packages.props
├── vendor/
│   └── echarts.min.js                 # 离线 HTML 报告使用的图表运行时
└── src/
    ├── ProjectScope.App/             # Avalonia 桌面应用与 MVVM 界面
    ├── ProjectScope.Core/            # 统计模型、类型识别、注释处理与目录树
    ├── ProjectScope.Infrastructure/  # 文件扫描、配置存储与报告导出
    └── ProjectScope.Tests/           # 核心统计、扫描和配置测试
```

## 项目规模

### 文件统计

- **总文件数**：28 个
  - C#文件：15 个
  - XML文件：2 个
  - JavaScript文件：1 个
  - Markdown文档：1 个
  - License文件：1 个
  - 其他文件：8 个

### 代码规模

- **代码总行数**：2,114 行（不含空行、注释）
  - C#：1,654 行（78.2%）
  - XML：448 行（21.2%）
  - JavaScript：12 行（0.6%）

- **字符总数**：1,143,974 字符（不含注释）
  - C#：92,377 字符（8.1%）
  - XML：29,222 字符（2.5%）
  - JavaScript：1,022,375 字符（89.4%）

### 资源文件

- **资源文件总数**：10 个
- **资源文件总大小**：39.20 MB
  - 压缩包：1 个文件，38.78 MB
  - 图片文件：6 个文件，361.43 KB
  - 其他资源文件：3 个文件，71.94 KB

> 数据来源：以上所有项目规模数据均基于当前项目目录自动统计生成。

## 开发环境

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

本项目采用 [MIT License](LICENSE)。
