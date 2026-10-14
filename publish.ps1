# Roda os testes e, se passarem, gera um único Fragmo.exe em artifacts/publish
# e o instalador artifacts/Fragmo-Setup.exe (se o Inno Setup 6 estiver instalado).
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet test
if ($LASTEXITCODE -ne 0) { Write-Error 'Testes falharam; nada foi publicado. / Tests failed; nothing was published.' }

dotnet publish src/Fragmo -c Release -r win-x64 -o artifacts/publish
if ($LASTEXITCODE -ne 0) { Write-Error 'Falha no dotnet publish. / dotnet publish failed.' }

$exe = Get-Item (Join-Path $PSScriptRoot 'artifacts/publish/Fragmo.exe')
'{0}  ({1:N1} MB)' -f $exe.FullName, ($exe.Length / 1MB)

# Instalador: procura o compilador do Inno Setup 6 (winget install JRSoftware.InnoSetup).
$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning 'Inno Setup 6 não encontrado; instalador não gerado. / Inno Setup 6 not found; installer skipped.'
    return
}

$version = ([xml](Get-Content src/Fragmo/Fragmo.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
& $iscc /Q "/DAppVersion=$version" installer/Fragmo.iss
if ($LASTEXITCODE -ne 0) { Write-Error 'Falha ao gerar o instalador. / Installer build failed.' }

$setup = Get-Item (Join-Path $PSScriptRoot 'artifacts/Fragmo-Setup.exe')
'{0}  ({1:N1} MB)' -f $setup.FullName, ($setup.Length / 1MB)
