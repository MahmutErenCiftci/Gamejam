$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskTargetRoot=[IO.Path]::GetFullPath((Join-Path $taskRoot 'docs/teslim/berk_gorseller'))
[IO.Directory]::CreateDirectory($taskTargetRoot) | Out-Null
$taskZipPath='C:/Users/mahmu/Downloads/Berk_Gorseller.zip'
$taskZip=[IO.Compression.ZipFile]::OpenRead($taskZipPath)
$taskRows=[Collections.Generic.List[object]]::new()
try {
foreach($taskEntry in $taskZip.Entries){
if($taskEntry.Length -eq 0){continue}
$taskEntryName=$taskEntry.FullName.Replace('\','/')
$taskDestination=[IO.Path]::GetFullPath((Join-Path $taskTargetRoot $taskEntryName))
if(-not $taskDestination.StartsWith($taskTargetRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Archive entry outside target'}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskDestination)) | Out-Null
$taskStream=$taskEntry.Open()
$taskMemory=[IO.MemoryStream]::new()
try {$taskStream.CopyTo($taskMemory);$taskBytes=$taskMemory.ToArray()} finally {$taskStream.Dispose();$taskMemory.Dispose()}
$taskHashAlgorithm=[Security.Cryptography.SHA256]::Create()
try {$taskOriginalHash=[BitConverter]::ToString($taskHashAlgorithm.ComputeHash($taskBytes)).Replace('-','').ToLowerInvariant()}finally{$taskHashAlgorithm.Dispose()}
if(Test-Path -LiteralPath $taskDestination){if((Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskOriginalHash){throw 'Existing copy mismatch'}}else{[IO.File]::WriteAllBytes($taskDestination,$taskBytes)}
$taskCopyHash=(Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash.ToLowerInvariant()
if($taskCopyHash -ne $taskOriginalHash){throw 'Copy hash mismatch'}
$taskRows.Add([ordered]@{id=('BERK-{0:D3}' -f ($taskRows.Count+1));entry=$taskEntryName;file=$taskDestination.Substring($taskRoot.Length+1).Replace('\','/');bytes=$taskEntry.Length;sha256=$taskCopyHash})
}
} finally {$taskZip.Dispose()}
[ordered]@{archive='Berk_Gorseller.zip';archiveSha256=(Get-FileHash -LiteralPath $taskZipPath -Algorithm SHA256).Hash.ToLowerInvariant();source='User-provided Berk visual/source appendix';files=$taskRows.ToArray()} | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 (Join-Path $taskRoot 'docs/teslim/BERK_ARSIV_KAYITLARI.json')
Write-Output ('PASS: '+$taskRows.Count+' files copied and hash checked.')
