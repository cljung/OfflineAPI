# OfflineAPI

Sample for showing aspnet webapi using a mix of Entra ID, Keycloak and certificate based authentication

## Project Folders

| Folder | Description |
|------|--------|
| [certs](certs) | Trusted certificates. CER files are used to validate incoming calls. PFX file is used for authenticating outgoing calls |
| [keycloak](keycloak) | scripts for starting )and creating)  and stopping keycloak docker container. |
| [keycloak\data](keycloak\data) | import configuration with the one clientId used for testing |
| [scripts](scripts) | Powershell scripts for testing |
| [Controllers](Controllers) | API controller |
| [Helpers](Helpers) | Helper classes for Keycloak and Certificate based authentication |

## Project Files

| Files | Description |
|------|--------|
| certs\OfflineAPI.cer | Trusted certificates for incoming calls. Generated with scripts\generate-cert.ps1 |
| certs\OfflineAPI.pfx | Certificates used for authenticating outgoing calls. Generated with scripts\generate-cert.ps1 |
| [keycloack\start-keycloak-docker.sh](keycloack\start-keycloak-docker.sh) | bash script to create and start a docker container using Keycloak. It also imports the kcidp-app.json file with configuration |
| [keycloack\stop-keycloak-docker.sh](keycloack\stops-keycloak-docker.sh) | bash script to stop and remove the docker container using Keycloak. |
| [keycloack\data\kcidp-app.json](keycloack\data\kcidp-app.json) | Configuration file to import to Keycloak. Contains an app and som claims mapping to make the access token look more like Entra |
| [scripts\generate-cert.ps1](scripts\generate-cert.ps1) | Powershell scripts for generating a self-signed certificate. Run it from the OfflineAPI folder |
| [scripts\test-cert-auth.ps1](scripts\test-cert-auth.ps1) | Powershell scripts for calling /api/management using certificate based authentication. Run it from the scripts folder |
| [scripts\test-client-creds-keycloak.ps1](scripts\test-client-creds-keycloak.ps1) | Powershell scripts for calling /api/management using Keycloak access token. Run it from the scripts folder |
| [scripts\test-client-creds-entra.ps1](scripts\test-client-creds-entra.ps1) | Powershell scripts for calling /api/management using Entra ID access token. Run it from the scripts folder |
| [Controllers\ApiController.cs](Controllers\ApiController.cs) | API controller containing /api/management and /api/internal endpoints |
| [Helpers\CertificateAuthenticationExtensions.cs](Helpers\CertificateAuthenticationExtensions.cs) | Helper class to setup certificate based authentication in Program.cs |
| [Helpers\KeycloakHelper.cs](Helpers\KeycloakHelper.cs) | Helper class to acquire access tokens from Keycloak |
| [Helpers\UnifiedClaimsTransformer.cs](Helpers\UnifiedClaimsTransformer.cs) | Helper class to show how you can transform Keycloak claims to look like Entra |
| [Program.cs](Program.cs) | Startup file |
| [appsettings.json](appsettings.json) | Configuration file |

# appsettings.json

The appsettings.json file contains a section each for Entra ID, Keycloak and Certificates. 
For the Entra section, only `Authority` and `Audience` are really used for validating the incoming JWT access token. 
The `ClientId` and `ClientSecret` values are only used by the [scripts\test-client-creds-entra.ps1](scripts\test-client-creds-entra.ps1) script for client credentials authentication. 

The Keycloak data must match values in [keycloack\data\kcidp-app.json](keycloack\data\kcidp-app.json). 

The Certificates section has a list of certificates in `ValidCertsPaths` that are trusted for incoming calls. It is a list since you can have more than one during cert rotation.
The `CertPath` holds the PFX certificate that should be used to authenticate outgoing calls, ie when /api/management calls /api/internal.

```JSON
  "Authentication": {
    "Entra": {
      "Authority": "https://sts.windows.net/<tenantId>/",
      "Audience": "api://<appId>",
      "ClientId": "<really only needed for test scrips>",
      "ClientSecret": "..."
    },
    "Keycloak": {
      "Authority": "http://localhost:8080/realms/kcrealm",
      "Audience": "api://kcidp-app",
      "ClientId": "kcidp-app",
      "ClientSecret": "ykCeD49CI1R1CxUpQx7rCCnC62ZqQLXv",
      "RequireHttpsMetadata": false
    },
    "Certificate": {
      "ValidCertsPaths": [
        "certs\\OfflineAPI.cer"
      ],
      "CertPath": "certs\\OfflineAPI.pfx",
      "CertPassphrase": "OfflineAPI"
    }
  }
```

## Testing

To test locally, do the followind:

1. If you want to use Keycloak, make sure you have Docker Desktop installed and that it is working inside WSL (Settings > Resources > Enable integration with my default WSL distro).
1. Run ./start-keycloak-docker.sh. Give it a few minutes and check that the admin console responds at `http://localhost:8080`. Userid and password is in the bash script
1. Update your Entra ID tenant ID in the appsettings.json file (guid after sts.windows.net in `Authority`)
1. Create an App registration in Entra ID. Copy the AppID and update appsettings.json `ClientID` and `Audience`.
1. Run the OfflineAPI 
1. Open a powershell command prompt, cd into scripts, and test run all of the test scripts


