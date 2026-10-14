# Roda os testes e, se passarem, gera um único Fragmo.exe em artifacts/publish.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet test
if ($LASTEXITCODE -ne 0) { Write-Error 'Testes falharam; nada foi publicado. / Tests failed; nothing was published.' }

dotnet publish src/Fragmo -c Release -r win-x64 -o artifacts/publish
if ($LASTEXITCODE -ne 0) { Write-Error 'Falha no dotnet publish. / dotnet publish failed.' }

$exe = Get-Item (Join-Path $PSScriptRoot 'artifacts/publish/Fragmo.exe')
'{0}  ({1:N1} MB)' -f $exe.FullName, ($exe.Length / 1MB)
