namespace ProjectScope.Core;

public interface IProjectScanner
{
    Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
}
