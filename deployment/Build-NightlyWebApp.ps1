if ([Environment]::Is64BitOperatingSystem) {
    $pfiles = ${env:PROGRAMFILES(X86)}
} else {
    $pfiles = $env:PROGRAMFILES
}
$msbuild = $pfiles + '\MSBuild\14.0\Bin\MSBuild.exe'
if (!(Test-Path $msbuild)) {
    Write-Error -Message 'ERROR: Failed to locate MSBuild at ' + $msbuild
    exit 1
}
$solution = '..\PerformanceTest.sln'
if (!(Test-Path $solution)) {
    Write-Error -Message 'ERROR: Failed to locate solution file at ' + $solution
    exit 1
}
$config = '/p:Configuration=Release'
$platform = '/p:Platform="Any CPU"'
$env:errorLevel = 0
$proc = Start-Process $msbuild $solution,$config,$platform,'/t:"NightlyWebApp:Rebuild"','/p:DeployNightlyWebApp=true','/p:PublishProfile=DeploymentFolder' -NoNewWindow -PassThru
$handle = $proc.Handle #workaround for not-working otherwise exit code
$proc.WaitForExit()
if ($env:errorLevel -ne 0 -or $proc.ExitCode -ne 0) {
    Write-Error -Message 'BUILD FAILED'
    exit 1
} else {
    echo 'BUILD SUCCEEDED'
    exit 0
}