param([Parameter(ValueFromRemainingArguments = $true)][string[]]$TestArguments)
$ErrorActionPreference = 'Stop'
Remove-Item Env:NO_COLOR -ErrorAction SilentlyContinue
Set-Location $PSScriptRoot
$runtime = Join-Path $PSScriptRoot '.runtime'
New-Item -ItemType Directory -Force $runtime | Out-Null
function Check-Exit { if ($LASTEXITCODE -ne 0) { throw "Command failed: $LASTEXITCODE" } }
dotnet build Fixture/Store.Browser.Fixture.csproj -p:NuGetAudit=false --nologo *> "$runtime/fixture-build.log"
Check-Exit
$fixture = Join-Path $PSScriptRoot 'Fixture/bin/Debug/net9.0/Store.Browser.Fixture.dll'
$output = & dotnet $fixture init
Check-Exit
$seed = $output | Where-Object { $_.StartsWith('{') } | Select-Object -Last 1
if (!$seed) { throw 'Fixture did not return seed data' }
[IO.File]::WriteAllText("$runtime/seed.json", $seed, [Text.UTF8Encoding]::new($false))
$data = $seed | ConvertFrom-Json
$env:ConnectionStrings__DefaultConnection = $data.connection
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5187'
$env:Minio__Endpoint = '127.0.0.1:9190'
$env:Minio__AccessKey = 'browser-test-only'
$env:Minio__SecretKey = 'browser-test-secret-only'
$env:Minio__BucketName = 'browser-' + $data.run
$env:Minio__UseSSL = 'false'
$env:Orders__ExpirationIntervalSeconds = '5'
$env:MINIO_ROOT_USER = $env:Minio__AccessKey
$env:MINIO_ROOT_PASSWORD = $env:Minio__SecretKey
$env:STORE_BROWSER_FIXTURE = $fixture
$app = $null
$minio = $null
try {
    $env:STORE_BROWSER_MINIO = '0'
    foreach ($port in @(5187, 9190, 9191)) {
        if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Test port $port already occupied; refusing to use another server." }
    }
    if (Test-Path "$runtime/minio.exe") {
        $minio = Start-Process "$runtime/minio.exe" -ArgumentList @('server', "$runtime/minio-data", '--address', '127.0.0.1:9190', '--console-address', '127.0.0.1:9191') -WindowStyle Hidden -PassThru -RedirectStandardOutput "$runtime/minio.log" -RedirectStandardError "$runtime/minio-error.log"
        $env:STORE_BROWSER_MINIO = '1'
    } else { Write-Warning 'MinIO binary unavailable: upload/retrieval test will be skipped.' }
    $app = Start-Process dotnet -ArgumentList @('run', '--no-build', '--no-launch-profile', '--project', '../../EndPoint.Site/EndPoint.Site.csproj') -WindowStyle Hidden -PassThru -RedirectStandardOutput "$runtime/app.log" -RedirectStandardError "$runtime/app-error.log"
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try {
            if ($minio) { $health = Invoke-WebRequest 'http://127.0.0.1:9190/minio/health/live' -UseBasicParsing }
            $response = Invoke-WebRequest 'http://127.0.0.1:5187/Authentication/Login' -UseBasicParsing
            if ((!$minio -or $health.StatusCode -eq 200) -and $response.StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (!$ready) { throw 'Isolated application or MinIO failed readiness; inspect .runtime logs.' }
    & npx playwright test @TestArguments
    $testExit = $LASTEXITCODE
} finally {
    if ($app -and !$app.HasExited) { & taskkill /PID $app.Id /T /F | Out-Null }
    if ($minio -and !$minio.HasExited) { Stop-Process -Id $minio.Id }
}
exit $testExit
