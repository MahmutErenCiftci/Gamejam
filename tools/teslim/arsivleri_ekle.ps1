$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput = Join-Path $taskRoot 'docs/teslim'
$taskMediaRoot = [IO.Path]::GetFullPath((Join-Path $taskOutput 'ai_gorseller'))
[IO.Directory]::CreateDirectory($taskMediaRoot) | Out-Null
$taskSources = @(
    @{ Kind = 'Konsept ve görsel üretimler'; Group = 'konseptler'; Path = 'C:/Users/mahmu/Downloads/WhatsApp Unknown 2026-10-18 at 16.44.38.zip' },
    @{ Kind = 'Karakter ve yön animasyonları'; Group = 'karakterler'; Path = 'C:/Users/mahmu/Downloads/karakterler.zip' }
)
$taskRows = [Collections.Generic.List[object]]::new()
$taskArchiveRows = [Collections.Generic.List[object]]::new()
$taskIndex = 0
foreach ($taskSource in $taskSources) {
    $taskArchiveHash = (Get-FileHash -LiteralPath $taskSource.Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $taskArchiveRows.Add([ordered]@{ name = [IO.Path]::GetFileName($taskSource.Path); sha256 = $taskArchiveHash; group = $taskSource.Group; declaration = 'Kullanıcı bu arşivi oyuna ilişkin AI üretimleri olarak sağladı; araç/model/prompt bilgisi ayrıca teyit edilecek.' })
    $taskArchive = [IO.Compression.ZipFile]::OpenRead($taskSource.Path)
    try {
        foreach ($taskEntry in $taskArchive.Entries) {
            if ($taskEntry.FullName.EndsWith('/')) { continue }
            if ([IO.Path]::GetExtension($taskEntry.FullName).ToLowerInvariant() -notin @('.jpg','.jpeg','.png','.gif','.webp','.bmp')) { continue }
            $taskTarget = [IO.Path]::GetFullPath((Join-Path (Join-Path $taskMediaRoot $taskSource.Group) $taskEntry.FullName))
            if (-not $taskTarget.StartsWith($taskMediaRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Arşiv girdisi hedef klasör dışında.' }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskTarget)) | Out-Null
            if (Test-Path -LiteralPath $taskTarget) { throw "Hedef zaten var: $taskTarget" }
            $taskStream = $taskEntry.Open()
            try {
                $taskOutputStream = [IO.File]::Create($taskTarget)
                try { $taskStream.CopyTo($taskOutputStream) } finally { $taskOutputStream.Dispose() }
            } finally { $taskStream.Dispose() }
            $taskIndex++
            $taskHash = (Get-FileHash -LiteralPath $taskTarget -Algorithm SHA256).Hash.ToLowerInvariant()
            $taskRows.Add([ordered]@{
                id = ('AI-{0:D3}' -f $taskIndex)
                archive = [IO.Path]::GetFileName($taskSource.Path)
                entry = $taskEntry.FullName
                category = $taskSource.Kind
                file = $taskTarget.Substring($taskRoot.Length + 1).Replace('\','/')
                bytes = $taskEntry.Length
                sha256 = $taskHash
                provider = 'TEYİT BEKLİYOR'
                prompt = 'Orijinal prompt bu arşivde bulunmuyor; ekipten istenecek.'
                declaration = 'AI üretimi: kullanıcı beyanı. Kesin oyunda kullanım: dosya karşılaştırması/ekip teyidi gerekiyor.'
            })
        }
    } finally { $taskArchive.Dispose() }
}
$taskManifest = [ordered]@{ createdAtUtc = [DateTime]::UtcNow.ToString('o'); archives = $taskArchiveRows.ToArray(); files = $taskRows.ToArray() }
$taskManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'AI_ARSIV_KAYITLARI.json') -Encoding utf8
[ordered]@{ archives = $taskArchiveRows.Count; files = $taskRows.Count; target = $taskMediaRoot } | ConvertTo-Json
