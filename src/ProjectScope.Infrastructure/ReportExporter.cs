using System.Globalization;
using System.Text;
using System.Text.Json;
using ProjectScope.Core;

namespace ProjectScope.Infrastructure;

public enum ReportFormat { Markdown, Json, Text, Html, DirectoryTree }

public sealed class ReportExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Render(ScanResult result, ReportFormat format)
    {
        ArgumentNullException.ThrowIfNull(result);
        return format switch
        {
            ReportFormat.Markdown => Markdown(result),
            ReportFormat.Json => Json(result),
            ReportFormat.Text => Text(result),
            ReportFormat.Html => Html(result),
            ReportFormat.DirectoryTree => result.DirectoryTree,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported report format.")
        };
    }

    public async Task ExportAsync(ScanResult result, ReportFormat format, string outputPath, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        await File.WriteAllTextAsync(Path.GetFullPath(outputPath), Render(result, format), new UTF8Encoding(false), token);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return value.ToString(index == 0 ? "0" : "N2", CultureInfo.InvariantCulture) + " " + units[index];
    }

    private static string Markdown(ScanResult result)
    {
        var output = new StringBuilder("## 项目规模\n\n### 文件统计\n\n");
        output.AppendLine($"- **总文件数**：{result.TotalFiles:N0} 个");
        foreach (var type in result.FileCounts) output.AppendLine($"  - {type.Label}：{type.Count:N0} 个");
        output.AppendLine().AppendLine("### 代码规模").AppendLine();
        output.AppendLine($"- **代码总行数**：{result.TotalCodeLines:N0} 行（不含空行、注释）");
        foreach (var type in result.CodeTypes) output.AppendLine($"  - {type.Label}：{type.Lines:N0} 行（{type.LinePercentage:F1}%）");
        output.AppendLine().AppendLine($"- **字符总数**：{result.TotalCodeCharacters:N0} 字符（不含注释）");
        foreach (var type in result.CodeTypes) output.AppendLine($"  - {type.Label}：{type.Characters:N0} 字符（{type.CharacterPercentage:F1}%）");
        output.AppendLine().AppendLine("### 资源文件").AppendLine();
        output.AppendLine($"- **资源文件总数**：{result.TotalAssetFiles:N0} 个");
        output.AppendLine($"- **资源文件总大小**：{FormatBytes(result.TotalAssetBytes)}");
        foreach (var type in result.AssetTypes) output.AppendLine($"  - {type.Label}：{type.Files:N0} 个文件，{FormatBytes(type.Bytes)}");
        return output.ToString();
    }

    private static string Text(ScanResult result)
    {
        var output = new StringBuilder("项目规模\n\n文件统计\n\n");
        output.AppendLine($"总文件数：{result.TotalFiles:N0} 个");
        foreach (var type in result.FileCounts) output.AppendLine($"  {type.Label}：{type.Count:N0} 个");
        output.AppendLine().AppendLine("代码规模").AppendLine();
        output.AppendLine($"代码总行数：{result.TotalCodeLines:N0} 行（不含空行、注释）");
        foreach (var type in result.CodeTypes) output.AppendLine($"  {type.Label}：{type.Lines:N0} 行（{type.LinePercentage:F1}%）");
        output.AppendLine().AppendLine($"字符总数：{result.TotalCodeCharacters:N0} 字符（不含注释）");
        foreach (var type in result.CodeTypes) output.AppendLine($"  {type.Label}：{type.Characters:N0} 字符（{type.CharacterPercentage:F1}%）");
        output.AppendLine().AppendLine("资源文件").AppendLine();
        output.AppendLine($"资源文件总数：{result.TotalAssetFiles:N0} 个");
        output.AppendLine($"资源文件总大小：{FormatBytes(result.TotalAssetBytes)}");
        foreach (var type in result.AssetTypes) output.AppendLine($"  {type.Label}：{type.Files:N0} 个文件，{FormatBytes(type.Bytes)}");
        return output.ToString();
    }

    private static string Json(ScanResult result)
    {
        var rows = CodeRows(result);
        var linePercentages = PercentageAllocator.Allocate(rows.Select(item => item.Lines).ToArray());
        var charPercentages = PercentageAllocator.Allocate(rows.Select(item => item.Characters).ToArray());
        var payload = new
        {
            generated_at = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            root = result.RootPath,
            totals = new
            {
                all_files = result.TotalFiles,
                code_files = rows.Sum(item => item.Files),
                code_lines = rows.Sum(item => item.Lines),
                code_chars = rows.Sum(item => item.Characters),
                asset_files = result.TotalAssetFiles,
                asset_bytes = result.TotalAssetBytes
            },
            file_counts = result.FileCounts.Where(item => item.Count > 0).OrderByDescending(item => item.Count).ThenBy(item => item.Key, StringComparer.Ordinal).Select(item => new { key = item.Key, label = item.Label, count = item.Count }),
            code_lines = rows.Select((item, index) => new { key = item.Key, label = item.Label, count = item.Lines, percentage = linePercentages[index] }),
            code_chars = rows.Select((item, index) => new { key = item.Key, label = item.Label, count = item.Characters, percentage = charPercentages[index] })
        };
        return JsonSerializer.Serialize(payload, JsonOptions) + Environment.NewLine;
    }

    private static string Html(ScanResult result)
    {
        var rows = CodeRows(result);
        var payload = new
        {
            root = result.RootPath,
            total_files = result.TotalFiles,
            total_code_files = rows.Sum(item => item.Files),
            total_code_chars = rows.Sum(item => item.Characters),
            total_code_lines = rows.Sum(item => item.Lines),
            total_asset_files = result.TotalAssetFiles,
            total_asset_bytes = result.TotalAssetBytes,
            file_counts = result.FileCounts.Where(item => item.Count > 0).OrderByDescending(item => item.Count).ThenBy(item => item.Key, StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.Count),
            code_stats = rows.Select(item => new { name = item.Label, files = item.Files, lines = item.Lines, chars = item.Characters }),
            asset_stats = result.AssetTypes.OrderByDescending(item => item.Bytes).ThenBy(item => item.Key, StringComparer.Ordinal).Select(item => new { name = item.Label, files = item.Files, bytes = item.Bytes }),
            directory_tree = result.DirectoryTree
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default });
        return HtmlTemplate.Replace("__JSON_DATA__", json, StringComparison.Ordinal);
    }

    private static IReadOnlyList<CodeTypeCount> CodeRows(ScanResult result) => result.CodeTypes
        .Where(item => item.Lines > 0 || item.Characters > 0 || item.Files > 0)
        .OrderByDescending(item => item.Lines)
        .ThenBy(item => item.Key, StringComparer.Ordinal)
        .ToArray();

    private const string HtmlTemplate = """"
<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>项目代码统计报告</title>
<script src="https://cdn.jsdelivr.net/npm/echarts@5.4.3/dist/echarts.min.js"></script>
<style>
:root{--bg:#f4f6f8;--card:#fff;--text:#2c3e50;--accent:#3498db}*{box-sizing:border-box}body{font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif;background:var(--bg);color:var(--text);margin:0;padding:20px}.container{max-width:1200px;margin:0 auto}h1{text-align:center;margin-bottom:30px}.dashboard{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:20px;margin-bottom:30px}.card,.chart-box{background:var(--card);padding:20px;border-radius:8px;box-shadow:0 2px 12px rgba(0,0,0,.05)}.card{text-align:center}.card h3{margin:0 0 10px;font-size:14px;color:#7f8c8d;text-transform:uppercase}.card .num{font-size:32px;font-weight:bold;color:var(--accent)}.card .sub{font-size:12px;color:#95a5a6;margin-top:5px}.charts-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(500px,1fr));gap:20px}.chart-box[style]{grid-column:1/-1}.chart-header{display:flex;justify-content:space-between;align-items:center;margin-bottom:15px}.chart-title{font-size:18px;font-weight:bold;margin:0}.chart-controls select{padding:4px 8px;border-radius:4px;border:1px solid #ddd}.chart-container{height:400px;width:100%}.tree-box{margin-top:20px;background:var(--card);padding:20px;border-radius:8px;box-shadow:0 2px 12px rgba(0,0,0,.05)}.tree-box pre{margin:0;overflow:auto;font-family:"Cascadia Mono",Consolas,"SFMono-Regular",monospace;font-size:13px;line-height:1.55;white-space:pre;color:var(--text)}@media(max-width:600px){.charts-grid{grid-template-columns:1fr}.chart-box{min-width:0}.chart-container{height:320px}}
</style>
</head>
<body><div class="container"><h1>项目代码统计报告</h1><p style="text-align:center;color:#666">路径: <code id="root-path"></code></p>
<div class="dashboard"><div class="card"><h3>总文件数</h3><div class="num" id="total-files">-</div><div class="sub">含代码与资源</div></div><div class="card"><h3>代码总行数</h3><div class="num" id="total-lines">-</div><div class="sub">不含空行/注释</div></div><div class="card"><h3>代码字符数</h3><div class="num" id="total-chars">-</div></div><div class="card"><h3>资源总大小</h3><div class="num" id="total-asset-size">-</div><div class="sub" id="total-asset-files">-</div></div></div>
<div class="charts-grid"><div class="chart-box"><div class="chart-header"><h2 class="chart-title">代码量分布 (行数)</h2><div class="chart-controls"><select onchange="renderCodeChart(this.value)"><option value="bar">柱状图 (Bar)</option><option value="pie">饼图 (Pie)</option></select></div></div><div id="chart-code" class="chart-container"></div></div>
<div class="chart-box"><div class="chart-header"><h2 class="chart-title">文件类型分布</h2><div class="chart-controls"><select onchange="renderFileChart(this.value)"><option value="pie">饼图 (Pie)</option><option value="bar">柱状图 (Bar)</option></select></div></div><div id="chart-file" class="chart-container"></div></div>
<div class="chart-box" style="grid-column:1/-1"><div class="chart-header"><h2 class="chart-title">资源类型分布 (大小)</h2><div class="chart-controls"><select onchange="renderAssetChart(this.value)"><option value="bar">横向柱状图 (Bar)</option><option value="pie">饼图 (Pie)</option></select></div></div><div id="chart-asset" class="chart-container"></div></div></div>
<div class="tree-box"><div class="chart-header"><h2 class="chart-title">项目结构</h2></div><pre id="directory-tree"></pre></div>
<script>
const DATA=__JSON_DATA__;function fmtBytes(bytes){if(bytes===0)return'0 B';const k=1024,sizes=['B','KB','MB','GB','TB'],i=Math.floor(Math.log(bytes)/Math.log(k));return parseFloat((bytes/Math.pow(k,i)).toFixed(2))+' '+sizes[i]}function fmtInt(n){return n.toLocaleString()}document.getElementById('root-path').textContent=DATA.root;document.getElementById('directory-tree').textContent=DATA.directory_tree||'';document.getElementById('total-files').textContent=fmtInt(DATA.total_files);document.getElementById('total-lines').textContent=fmtInt(DATA.total_code_lines);document.getElementById('total-chars').textContent=fmtInt(DATA.total_code_chars);document.getElementById('total-asset-size').textContent=fmtBytes(DATA.total_asset_bytes);document.getElementById('total-asset-files').textContent=fmtInt(DATA.total_asset_files)+' 个文件';let codeChart,fileChart,assetChart;function initChart(id,option){const chart=echarts.init(document.getElementById(id));chart.setOption(option);window.addEventListener('resize',()=>chart.resize());return chart}function renderCodeChart(type){const data=DATA.code_stats.map(x=>({name:x.name,value:x.lines})).sort((a,b)=>b.value-a.value);const option=type==='pie'?{tooltip:{trigger:'item',formatter:'{b}: {c}行 ({d}%)'},series:[{type:'pie',radius:['40%','70%'],data,itemStyle:{borderRadius:5,borderColor:'#fff',borderWidth:2}}]}:{tooltip:{trigger:'axis',axisPointer:{type:'shadow'}},grid:{left:'3%',right:'4%',bottom:'3%',containLabel:true},xAxis:{type:'category',data:data.map(x=>x.name),axisLabel:{rotate:45}},yAxis:{type:'value'},series:[{type:'bar',data:data.map(x=>x.value),itemStyle:{color:'#3498db'}}]};if(codeChart)codeChart.dispose();codeChart=initChart('chart-code',option)}function renderFileChart(type){const data=Object.entries(DATA.file_counts).map(([k,v])=>({name:k,value:v})).sort((a,b)=>b.value-a.value),top=data.slice(0,15),others=data.slice(15).reduce((a,x)=>a+x.value,0);if(others>0)top.push({name:'其他',value:others});const option=type==='pie'?{tooltip:{trigger:'item'},series:[{type:'pie',radius:'65%',data:top}]}:{tooltip:{trigger:'axis'},xAxis:{type:'category',data:top.map(x=>x.name),axisLabel:{rotate:45}},yAxis:{type:'value'},series:[{type:'bar',data:top.map(x=>x.value),itemStyle:{color:'#2ecc71'}}]};if(fileChart)fileChart.dispose();fileChart=initChart('chart-file',option)}function renderAssetChart(type){const data=DATA.asset_stats.map(x=>({name:x.name,value:x.bytes,files:x.files})).sort((a,b)=>b.value-a.value);const option=type==='pie'?{tooltip:{trigger:'item',formatter:p=>`${p.name}<br/>大小: ${fmtBytes(p.value)}<br/>文件数: ${p.data.files}`},series:[{type:'pie',radius:['30%','70%'],roseType:'area',data}]}:{tooltip:{trigger:'axis',formatter:p=>`${p[0].name}<br/>大小: ${fmtBytes(p[0].value)}`},grid:{left:'3%',right:'4%',bottom:'3%',containLabel:true},xAxis:{type:'value',axisLabel:{formatter:v=>fmtBytes(v)}},yAxis:{type:'category',data:data.map(x=>x.name)},series:[{type:'bar',data:data.map(x=>x.value),itemStyle:{color:'#e74c3c'},label:{show:true,position:'right',formatter:p=>fmtBytes(p.value)}}]};if(assetChart)assetChart.dispose();assetChart=initChart('chart-asset',option)}renderCodeChart('bar');renderFileChart('pie');renderAssetChart('bar');
</script></body></html>
"""";
}
