$kcdata = (Get-Content "../keycloak/data/kcidp-app.json" | ConvertFrom-json)

$KeycloakBaseUrl = "$($kcdata.clients[0].rootUrl):8080"
$RealmName       = $kcdata.realm
$ClientId        = $kcdata.clients[0].clientId
$ClientSecret    = $kcdata.clients[0].secret
$TokenUri = "$KeycloakBaseUrl/realms/$RealmName/protocol/openid-connect/token"

$RequestBody = @{ grant_type = "client_credentials"; client_id = $ClientId; client_secret = $ClientSecret}

$TokenResponse = Invoke-RestMethod -Uri $TokenUri -Method Post -ContentType "application/x-www-form-urlencoded" -Body $RequestBody
$AccessToken = $TokenResponse.access_token
Write-Host "Successfully authenticated with Keycloak! Access token received." -ForegroundColor Green
#Write-Host $AccessToken -ForegroundColor white

$Headers = @{ "Authorization" = "Bearer $AccessToken"; "Accept" = "application/json" }
    
$ApiResponse = Invoke-RestMethod -Uri "https://localhost:5001/api/management" -Method Get -Headers $Headers
$ApiResponse
