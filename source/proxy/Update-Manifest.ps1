$ErrorActionPreference = 'Stop'
try {
    Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -Force
    $root = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'proxy'
    $manifestPath = Join-Path $root 'proxy-manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
    $manifest.ProxySHA256 = (Get-FileHash -LiteralPath (Join-Path $root 'amd_ags_x64.dll') -Algorithm SHA256).Hash
    $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    foreach ($name in @('proxy_ags.obj', 'proxy_ags.lib', 'proxy_ags.exp')) {
        $path = Join-Path $root $name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
    Write-Host 'Build complete. Package hash updated; no game installation was modified.'
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit 1
}
