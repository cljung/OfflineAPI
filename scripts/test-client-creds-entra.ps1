if (Test-Path -Path "../appsettings.Development.json") {
  $appsettings = (Get-Content "../appsettings.Development.json" | ConvertFrom-json)
} else {
  $appsettings = (Get-Content "../appsettings.json" | ConvertFrom-json)
}

# see if Keycloak is responding, else use cert based auth for downstream call
$kcdata = (Get-Content "../keycloak/data/kcidp-app.json" | ConvertFrom-json)
try {
  $statusCode = (Invoke-WebRequest -Uri "$($kcdata.clients[0].rootUrl):8080/realms/$($kcdata.realm)" -UseBasicParsing).StatusCode
} catch {
  $statusCode = 500
}
$auth = "keycloak"
if ( $statusCode -ne 200) {
  $auth = "cert" 
}

# acquire an Entra access token via client credentials
$ClientId = $appsettings.Authentication.Entra.ClientId
$ClientSecret = $appsettings.Authentication.Entra.ClientSecret
$Scope = "$($appsettings.Authentication.Entra.Audience)/.default"
$tenantId = $appsettings.Authentication.Entra.Authority.Split("/")[3]

$TokenResponse = Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" `
        -ContentType "application/x-www-form-urlencoded" `
        -Body @{ grant_type="client_credentials"; client_id=$ClientId; client_secret=$ClientSecret; scope=$Scope }
$AccessToken = $TokenResponse.access_token
Write-Host "Successfully authenticated with Entra! Access token received." -ForegroundColor Green
#Write-Host $AccessToken -ForegroundColor white

$Headers = @{ "Authorization"="Bearer $AccessToken"; "Accept"="application/json" }
    
$ApiResponse = Invoke-RestMethod -Uri "https://localhost:5001/api/management?auth=$auth" -Method Get -Headers $Headers -SkipCertificateCheck 
$ApiResponse
