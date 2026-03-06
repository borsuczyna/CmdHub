param(
    [Parameter(Mandatory = $true)]
    [string]$FrontendRoot,
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,
    [Parameter(Mandatory = $true)]
    [string]$GeneratedSource
)

$distPath = Join-Path $FrontendRoot "dist"
if (-not (Test-Path $distPath)) {
    throw "React dist folder not found at '$distPath'."
}

if (Test-Path $ArchivePath) {
    Remove-Item $ArchivePath -Force
}

Compress-Archive -Path (Join-Path $distPath "*") -DestinationPath $ArchivePath -CompressionLevel Optimal

$bytes = [System.IO.File]::ReadAllBytes($ArchivePath)
$base64 = [System.Convert]::ToBase64String($bytes)

$chunkSize = 120
$base64Chunks = for ($i = 0; $i -lt $base64.Length; $i += $chunkSize) {
    $length = [Math]::Min($chunkSize, $base64.Length - $i)
    $base64.Substring($i, $length)
}

$chunkLiterals = ($base64Chunks | ForEach-Object { '            "' + $_ + '"' }) -join ",`r`n"

$source = @"
namespace CmdHub.Services
{
    internal static class FrontendBundleData
    {
        private static readonly string ArchiveBase64 = string.Concat(
$chunkLiterals
        );

        internal static readonly byte[] ArchiveBytes = System.Convert.FromBase64String(ArchiveBase64);
    }
}
"@

$directory = [System.IO.Path]::GetDirectoryName($GeneratedSource)
if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path $directory)) {
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
}

[System.IO.File]::WriteAllText($GeneratedSource, $source, [System.Text.Encoding]::UTF8)
