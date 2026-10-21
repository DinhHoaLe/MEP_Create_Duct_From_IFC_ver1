$ErrorActionPreference = 'Stop'
Add-Type -Path "$PSScriptRoot/../src/Services/IfcSourcePathResolver.cs"
$resolver = [AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object {
    $_.GetType('IFCInfo.IfcSourcePathResolver')
} | Where-Object { $_ } | Select-Object -First 1
$folder = Join-Path ([IO.Path]::GetTempPath()) ('ifc-path-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $folder | Out-Null
$checks = 0
function Assert-Path($actual, $expected, $label) {
    if ($actual -ne $expected) { throw "$label`: expected $expected, got $actual" }
    $script:checks++
}
try {
    $source = Join-Path $folder 'Project3.ifc'
    $cache = "$source.RVT"
    $hostModel = Join-Path $folder 'Host.rvt'
    [IO.File]::WriteAllText($source, 'fixture')
    Assert-Path ($resolver::Resolve('Project3.ifc', $cache, '', $hostModel, 'Project3.ifc')) $source 'Filename metadata beside link'
    Assert-Path ($resolver::Resolve('Z:\old\Project3.ifc', $cache, '', $hostModel, 'Project3.ifc')) $source 'Moved IFC with stale metadata'
    Assert-Path ($resolver::Resolve('', $cache, '', '', 'Project3.ifc.RVT')) $source 'Derive IFC from cache'
    Assert-Path ($resolver::Resolve('', '', $cache, '', 'Project3.ifc')) $source 'Linked document fallback'
    Assert-Path ($resolver::Resolve('Project3.ifc', '', '', $hostModel, 'Project3.ifc')) $source 'Host folder fallback'
    $other = Join-Path $folder 'Original.ifc'
    [IO.File]::WriteAllText($other, 'fixture')
    Assert-Path ($resolver::Resolve($other, $cache, '', '', 'Project3.ifc')) $other 'Preserve existing absolute metadata'
    Assert-Path ($resolver::Resolve('Original.ifc', $cache, '', '', 'Project3.ifc')) $other 'Prefer named original beside link'
    $subfolder = Join-Path $folder 'sources'
    New-Item -ItemType Directory -Path $subfolder | Out-Null
    $relative = Join-Path $subfolder 'Relative.ifc'
    [IO.File]::WriteAllText($relative, 'fixture')
    Assert-Path ($resolver::Resolve('sources\Relative.ifc', $cache, '', '', 'Project3.ifc')) $relative 'Relative subfolder metadata'
    $missing = Join-Path $folder 'Missing.ifc'
    Assert-Path ($resolver::Resolve('Missing.ifc', "$missing.RVT", '', '', 'Missing.ifc')) $missing 'Missing IFC keeps useful picker location'
    Assert-Path ($resolver::Resolve('Project3.ifc', '', '', '', 'Project3.ifc')) '' 'Do not resolve against process working directory'
    Assert-Path ($resolver::Resolve('', 'RSN://server/model', '', '', 'Model')) '' 'Unsupported server path'
    Write-Output "PASS: $checks IFC source path checks."
} finally {
    # This directory was created uniquely by this test, under the OS temp directory.
    Remove-Item -LiteralPath $folder -Recurse -Force
}
