param (
    [string] $MiddlewareBaseUrl,
    [string] $MiddlewareFunctionsKey,
    [string] $SchemaPath
)

$headers = New-Object "System.Collections.Generic.Dictionary[[String],[String]]"
$headers.Add("Content-Type", 'application/json')
$headers.Add("x-functions-key", $MiddlewareFunctionsKey)

$response = Invoke-RestMethod -TimeoutSec 10000 "$($MiddlewareBaseUrl)events" -Method Post -Headers $headers -Body "$(get-content $SchemaPath)"
Write-Host $response