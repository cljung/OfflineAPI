$appsettings = (Get-Content "..\appsettings.Development.json" | ConvertFrom-json)

$ClientId = $appsettings.Authentication.Entra.ClientId
$ClientSecret = $appsettings.Authentication.Entra.ClientSecret
$Scope = "$($appsettings.Authentication.Entra.Audience)/.default"
$tenantId = $appsettings.Authentication.Entra.Authority.Split("/")[3]

$tokenEndpoint = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"

$body = @{ grant_type="client_credentials"; client_id=$ClientId; client_secret=$ClientSecret; scope=$Scope }
$TokenResponse = Invoke-RestMethod -Method Post -Uri $tokenEndpoint -ContentType "application/x-www-form-urlencoded" -Body $body

$AccessToken = $TokenResponse.access_token
Write-Host "Successfully authenticated with Entra! Access token received." -ForegroundColor Green
#Write-Host $AccessToken -ForegroundColor white

$Headers = @{ "Authorization"="Bearer $AccessToken"; "Accept"="application/json" }
    
$ApiResponse = Invoke-RestMethod -Uri "https://localhost:5001/api/management" -Method Get -Headers $Headers
$ApiResponse
