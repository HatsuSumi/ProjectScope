using System.Collections.Frozen;

namespace ProjectScope.Core;

public sealed class FileCatalog
{
    private readonly FrozenDictionary<string, FileTypeDefinition> _byExtension;
    private readonly FrozenDictionary<string, AssetTypeDefinition> _assetsByExtension;
    private readonly Dictionary<string, AssetTypeDefinition> _assetTypeByExtension;
    private readonly FrozenSet<string> _binaryExtensions;

    public FileCatalog()
    {
        var definitions = CreateFileTypes();
        var map = new Dictionary<string, FileTypeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        foreach (var extension in definition.Extensions)
            if (!map.TryAdd(extension, definition))
                throw new InvalidOperationException($"Duplicate extension mapping '{extension}'.");
        _byExtension = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        var assets = CreateAssets();
        var assetMap = new Dictionary<string, AssetTypeDefinition>(StringComparer.OrdinalIgnoreCase);
        var priorities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".fla"] = "adobe", [".flv"] = "flash", [".dat"] = "game_save", [".bin"] = "game_archive", [".3ds"] = "rom", [".iso"] = "rom", [".ase"] = "adobe"
        };
        foreach (var asset in assets)
        foreach (var extension in asset.Extensions)
        {
            if (assetMap.TryAdd(extension, asset)) continue;
            if (!priorities.TryGetValue(extension, out var preferred))
                throw new InvalidOperationException($"Asset extension '{extension}' has conflicting categories without an explicit priority.");
            if (asset.Name == preferred) assetMap[extension] = asset;
            if (assetMap[extension].Name != preferred)
                throw new InvalidOperationException($"Asset priority '{preferred}' for '{extension}' does not match its definitions.");
        }
        _assetsByExtension = assetMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _assetTypeByExtension = assetMap.ToDictionary(StringComparer.OrdinalIgnoreCase);
        _binaryExtensions = ".png .jpg .jpeg .gif .webp .ico .bmp .tiff .mp3 .wav .flac .mp4 .mkv .mov .avi .pdf .zip .7z .rar .gz .tar .woff .woff2 .ttf .otf .psd .jar .aar .wasm .node .bin .dex .jks .cer .dill".Split(' ').ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public FileTypeDefinition DetectTextType(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (name.StartsWith("license", StringComparison.Ordinal) || name.StartsWith("copying", StringComparison.Ordinal))
            return new("License", FrozenSet<string>.Empty, "License文件", false, CommentSyntax.None);
        if (_byExtension.TryGetValue(Path.GetExtension(name), out var definition)) return definition;
        return new("Other", FrozenSet<string>.Empty, "其他文件", false, CommentSyntax.None);
    }

    public AssetTypeDefinition DetectAssetType(string extension)
    {
        if (!_assetTypeByExtension.TryGetValue(extension, out var asset)) throw new KeyNotFoundException($"No asset category is registered for extension '{extension}'.");
        return asset;
    }
    public IReadOnlySet<string> AssetExtensions => _assetTypeByExtension.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);


    public bool TryDetectAsset(string path, out AssetTypeDefinition asset)
    {
        var name = Path.GetFileName(path);
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension is ".bak" or ".old" or ".orig" or ".tmp" or ".swp" or ".~")
        {
            asset = new("backup", "备份文件", FrozenSet<string>.Empty);
            return true;
        }
        if (name.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".motion3.json", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".physics3.json", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".pose3.json", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".cdi3.json", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".exp3.json", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".cubism.json", StringComparison.OrdinalIgnoreCase))
        {
            asset = new("live2d", "Live2D模型文件", FrozenSet<string>.Empty);
            return true;
        }
        return _assetsByExtension.TryGetValue(extension, out asset!);
    }

    public bool IsBinaryExtension(string path) => _binaryExtensions.Contains(Path.GetExtension(path));
    public string GetFileLabel(string type)
    {
        if (type == "Other") return "其他文件";
        if (type == "License") return "License文件";
        var definition = _byExtension.Values.FirstOrDefault(item => item.Name == type);
        if (definition is null) throw new ArgumentException($"Unknown file type '{type}'.", nameof(type));
        return definition.Label;
    }
    public string GetCodeLabel(string type) => type;

    private static IReadOnlyList<FileTypeDefinition> CreateFileTypes()
    {
        static IReadOnlySet<string> E(params string[] values) => values.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        var cl = CommentSyntax.CLike;
        return new FileTypeDefinition[]
        {
            new("JavaScript", E(".js", ".mjs", ".cjs"), "JavaScript文件", true, cl), new("TypeScript", E(".ts", ".tsx", ".mts", ".cts"), "TypeScript文件", true, cl),
            new("Dart", E(".dart"), "Dart文件", true, cl),
            new("Flatbuffers", E(".flat"), "FlatBuffers文件", false, CommentSyntax.None),
            new("Protobuf", E(".proto", ".pb", ".textproto"), "Protocol Buffers文件", false, CommentSyntax.None),
            new("SourceMap", E(".map"), "Source Map文件", false, CommentSyntax.None),
            new("Prisma", E(".prisma"), "Prisma Schema文件", false, CommentSyntax.None),
            new("CMake", E(".cmake", ".cmake.in"), "CMake构建脚本", false, CommentSyntax.None),
            new("Ninja", E(".ninja"), "Ninja构建文件", false, CommentSyntax.None),
            new("Gyp", E(".gyp", ".gypi"), "GYP构建文件", false, CommentSyntax.None),
            new("BNF", E(".bnf"), "BNF语法定义", false, CommentSyntax.None),
            new("Objective-C", E(".m", ".mm"), "Objective-C文件", true, cl),
            new("Kotlin", E(".kt", ".kts"), "Kotlin文件", true, cl),
            new("Swift", E(".swift"), "Swift文件", true, cl),
            new("Scala", E(".scala", ".sc"), "Scala文件", true, cl),
            new("Assembly", E(".s", ".asm"), "汇编文件", false, CommentSyntax.None),
            new("D", E(".d"), "D语言文件", false, CommentSyntax.None),
            new("Nix", E(".nix"), "Nix配置文件", false, CommentSyntax.None),
            new("Ruby", E(".rb", ".rake", ".gemspec"), "Ruby脚本", true, CommentSyntax.Hash),
            new("PHP", E(".php", ".phtml", ".php3", ".php4", ".php5", ".phps"), "PHP脚本", true, cl),
            new("Perl", E(".pl", ".pm", ".t"), "Perl脚本", true, CommentSyntax.Hash),
            new("R", E(".r", ".Rmd"), "R脚本", true, CommentSyntax.Hash),
            new("RenPy", E(".rpy", ".rpyc", ".rpym"), "Ren'Py脚本", false, CommentSyntax.Hash),
            new("RPG Maker", E(".rvdata2", ".rpgsave"), "RPG Maker数据", false, CommentSyntax.None),
            new("Kirikiri", E(".ks", ".tjs"), "Kirikiri", false, cl),
            new("ActionScript", E(".as"), "ActionScript", false, cl),
            new("Haxe", E(".hx"), "Haxe", false, cl),
            new("WebAssembly", E(".wat"), "WebAssembly", false, CommentSyntax.None),
            new("HTML", E(".html", ".htm", ".xhtml"), "HTML文件", true, CommentSyntax.Html), new("CSS", E(".css"), "CSS文件", true, cl), new("SCSS", E(".scss", ".sass"), "SCSS/Sass文件", true, cl), new("Less", E(".less"), "Less文件", true, cl),
            new("JSON", E(".json", ".json5", ".jsonc"), "JSON文件", true, CommentSyntax.None), new("YAML", E(".yml", ".yaml"), "YAML文件", true, CommentSyntax.Hash), new("XML", E(".xml", ".xsl", ".xslt", ".svg", ".xaml", ".axaml"), "XML文件", true, CommentSyntax.Html), new("TOML", E(".toml"), "TOML文件", true, CommentSyntax.Hash),
            new("INI", E(".ini", ".cfg", ".conf", ".editorconfig", ".properties", ".prefs", ".eslintrc", ".nycrc", ".babelrc", ".jshintrc", ".jscsrc", ".npmignore", ".gitignore", ".gitattributes", ".dockerignore", ".cursorindexingignore", ".flow", ".lintstagedrc", ".sample", ".example", ".template", ".env"), "INI/配置文件", true, CommentSyntax.Ini),
            new("Markdown", E(".md", ".markdown", ".mdown", ".mkd"), "Markdown文档", false, CommentSyntax.None), new("Text", E(".txt"), "文本文件", true, CommentSyntax.None),
            new("C", E(".c", ".h"), "C文件", true, cl), new("C++", E(".cc", ".cpp", ".cxx", ".hpp", ".hh", ".hxx", ".inl", ".inc"), "C++文件", true, cl), new("C#", E(".cs", ".csx"), "C#文件", true, cl), new("Java", E(".java", ".jav", ".jsp"), "Java文件", true, cl), new("Go", E(".go"), "Go文件", true, cl), new("Rust", E(".rs", ".rlib"), "Rust文件", true, cl), new("Python", E(".py", ".pyw", ".pyi"), "Python脚本", true, CommentSyntax.Python), new("SQL", E(".sql", ".ddl", ".dml"), "SQL脚本", true, CommentSyntax.Sql), new("Shell", E(".sh", ".bash", ".zsh", ".fish", ".ksh"), "Shell脚本", true, CommentSyntax.Hash),
            new("PowerShell", E(".ps1", ".psm1", ".psd1"), "PowerShell脚本", true, CommentSyntax.PowerShell), new("Lua", E(".lua"), "Lua脚本", true, CommentSyntax.Lua), new("批处理", E(".bat", ".cmd"), "批处理脚本", true, CommentSyntax.Batch), new("Shader", E(".shader", ".cg", ".cginc", ".hlsl", ".glsl", ".vert", ".frag", ".geom", ".comp", ".tesc", ".tese", ".vsh", ".fsh"), "着色器代码", true, cl), new("Godot", E(".gd", ".tscn", ".tres", ".godot"), "Godot文件", true, CommentSyntax.Hash), new("Unity", E(".unity", ".prefab", ".asset", ".meta", ".mat", ".controller", ".anim", ".mask"), "Unity工程文件", false, CommentSyntax.None), new("Unreal", E(".uproject", ".umap", ".uasset"), "Unreal工程文件", false, CommentSyntax.None)
        };
    }

    private static IReadOnlyList<AssetTypeDefinition> CreateAssets()
    {
        static IReadOnlySet<string> E(params string[] values) => values.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        return new AssetTypeDefinition[]
        {
            new("live2d", "Live2D模型文件", E(".moc3", ".moc")), new("spine", "Spine动画文件", E(".spine", ".skel", ".atlas")), new("adobe", "Adobe工程/素材文件", E(".psd", ".psb", ".psdt", ".abr", ".atn", ".aco", ".ase", ".asl", ".pat", ".grd", ".ai", ".ait", ".indd", ".idml", ".indt", ".aep", ".aet", ".prproj", ".sesx", ".lrcat", ".xd", ".fla", ".xfl")),
            new("art_source", "绘画/像素画源文件", E(".clip", ".cmc", ".sai", ".sai2", ".kra", ".krz", ".ase", ".aseprite", ".procreate", ".ora", ".mdp")), new("daw", "音乐工程(DAW)/乐谱", E(".flp", ".cpr", ".npr", ".als", ".logic", ".logicx", ".rpp", ".song", ".ptx", ".ptf", ".reason", ".mscz", ".mscx", ".sib", ".mus", ".musx", ".vsqx", ".vpr", ".ust", ".svp")), new("video_edit", "视频剪辑工程", E(".veg", ".veg-bak", ".drp", ".fcpxml", ".edl")), new("design", "策划/剧本/脑图", E(".xmind", ".mm", ".km", ".fountain", ".fdx", ".articy", ".twee", ".tw", ".drawio", ".axure", ".rp")),
            new("mobile_package", "移动端应用包", E(".apk", ".aab", ".xapk", ".obb", ".ipa", ".app", ".so", ".dex")), new("ios_resource", "iOS资源文件", E(".plist", ".xcconfig", ".storyboard", ".xib", ".entitlements", ".pbxproj", ".xcscheme", ".xcsettings", ".xcworkspacedata")), new("android_resource", "Android资源文件", E(".iml", ".ap_")), new("lock_file", "锁文件/依赖锁定", E(".lock")), new("rom", "游戏ROM/镜像", E(".nes", ".sfc", ".smc", ".gba", ".gbc", ".gb", ".nds", ".3ds", ".cia", ".nsp", ".xci", ".iso", ".wbfs", ".gcm", ".cso", ".n64", ".z64")), new("flash", "Flash文件", E(".swf", ".fla", ".flv")),
            new("game_save", "游戏存档文件", E(".sav", ".save", ".rpgsave", ".sol", ".dat", ".osr", ".srm", ".state")), new("audio_middleware", "音频中间件/库", E(".bnk", ".pck", ".acb", ".awb", ".fsb", ".fev", ".bank", ".wem", ".sab", ".sob")), new("game_archive", "游戏封包/归档", E(".pak", ".cpk", ".arc", ".bfa", ".bin", ".dat", ".cat", ".idx", ".assets", ".bundle", ".vpk", ".rgss3a", ".rgss2a", ".rgssad", ".xp3", ".npk", ".kpk", ".asar", ".bsp", ".wad")), new("texture", "纹理/贴图文件", E(".dds", ".ktx", ".ktx2", ".pvr", ".astc", ".pkm", ".atc", ".tex", ".mat", ".assetbundle", ".vtf", ".vmt")),
            new("image", "图片文件", E(".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".tiff", ".ico", ".svg", ".tga", ".exr", ".hdr", ".tif", ".heic", ".raw")), new("video", "视频文件", E(".mp4", ".mkv", ".mov", ".avi", ".webm", ".wmv", ".flv", ".m4v", ".ogv", ".ts", ".3gp")), new("audio", "音频文件", E(".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".opus", ".wma", ".mid", ".midi", ".aiff", ".caf", ".m4b")),
            new("model3d", "3D模型文件", E(".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds", ".blend", ".stl", ".ply", ".usd", ".usdz", ".abc", ".x", ".mqo", ".pmx", ".pmd", ".vmd", ".vpd", ".ma", ".mb", ".max", ".c4d")), new("office", "Office文档", E(".xlsx", ".xls", ".xlsm", ".xlsb", ".docx", ".doc", ".pptx", ".ppt", ".ppsx", ".pps")), new("pdf", "PDF文档", E(".pdf")), new("archive", "压缩包", E(".zip", ".7z", ".rar", ".gz", ".tar", ".bz2", ".xz", ".iso", ".img", ".dmg", ".cab", ".z")), new("font", "字体文件", E(".woff", ".woff2", ".ttf", ".otf", ".eot", ".ttc")), new("other_asset", "其他资源文件", E(".bin"))
        };
    }
}
