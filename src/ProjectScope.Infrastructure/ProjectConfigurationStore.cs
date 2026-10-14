using System.Text.Json;
using ProjectScope.Core;

namespace ProjectScope.Infrastructure;

public sealed class ProjectConfigurationStore
{
    public async Task SaveAsync(string path, ProjectConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(configuration);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null) Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(fullPath, json + Environment.NewLine, cancellationToken);
    }

    public async Task<ProjectConfiguration> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = await File.ReadAllTextAsync(Path.GetFullPath(path), cancellationToken);
        return JsonSerializer.Deserialize<ProjectConfiguration>(json)
            ?? throw new InvalidDataException("配置文件内容为空。");
    }
}
