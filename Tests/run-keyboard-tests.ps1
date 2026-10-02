param(
    [string]$Compiler = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe',
    [string]$PreviewPath = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$tempRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Temp\.agents'))
$testRoot = Join-Path $tempRoot ('omen-mon-keyboard-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'Bin\OmenMon.exe') -Destination $testRoot
    $fixture = New-Object System.Xml.XmlDocument
    $fixture.Load((Join-Path $projectRoot 'OmenMon.xml'))
    $config = $fixture.SelectSingleNode('/OmenMon/Config')
    $config.AppendChild($fixture.CreateElement('ReviewFixture')).InnerText = 'preserve me'
    $preset = $fixture.CreateElement('Preset')
    $preset.SetAttribute('Name', 'Fixture User Preset')
    $preset.InnerText = '7900FF:FF17D0:F97000:FFFFFF'
    $config.SelectSingleNode('ColorPresets').AppendChild($preset) | Out-Null
    $fixturePath = Join-Path $testRoot 'fixture.xml'
    $fixture.Save($fixturePath)
    foreach($suite in @('KeyboardLightingTests', 'PowerControlTests')) {
        # Give each suite its original independent configuration fixture.
        $fixture.Save($fixturePath)
        $testExe = Join-Path $testRoot ($suite + '.exe')
        & $Compiler /nologo /target:exe /platform:x64 "/out:$testExe" "/reference:$(Join-Path $testRoot 'OmenMon.exe')" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot ($suite + '.cs'))
        if($LASTEXITCODE -ne 0) { throw "$suite compilation failed." }
        if($suite -eq 'PowerControlTests' -and $PreviewPath) {
            & $testExe $fixturePath ([IO.Path]::GetFullPath($PreviewPath))
        } else { & $testExe $fixturePath }
        if($LASTEXITCODE -ne 0) { throw "$suite regression checks failed." }
    }
} finally {
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if(!$resolvedTestRoot.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Test cleanup path escaped the temporary workspace.'
    }
    Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
}
