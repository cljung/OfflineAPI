using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace OfflineAPI.Helpers;

public class UnifiedClaimsTransformer : IClaimsTransformation {
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal) {
        var clone = principal.Clone();
        var newIdentity = clone.Identity as ClaimsIdentity;
        if (newIdentity == null) return Task.FromResult(principal);
        var issuerClaim = principal.FindFirst("iss")?.Value ?? "";
        if (issuerClaim.Contains("microsoftonline.com") || issuerClaim.Contains("windows.net")) {
            MapEntraClaims(principal, newIdentity);
        } else if (issuerClaim.Contains("realms")) { // Common indicator for Keycloak
            MapKeycloakClaims(principal, newIdentity);
        }
        return Task.FromResult(clone);
    }

    private void AddScopeClaims(ClaimsPrincipal principal, ClaimsIdentity newIdentity, string scopeClaimName) {
        var scpClaim = principal.FindFirst(scopeClaimName)?.Value;
        if (!string.IsNullOrEmpty(scpClaim)) {
            var scopes = scpClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var scope in scopes) {
                newIdentity.AddClaim(new Claim("scope", scope));
            }
        }
    }
    private void MapEntraClaims(ClaimsPrincipal principal, ClaimsIdentity newIdentity) {
        AddScopeClaims(principal, newIdentity, "scp");
    }

    private void MapKeycloakClaims(ClaimsPrincipal principal, ClaimsIdentity newIdentity) {
        var realmAccessJson = principal.FindFirst("realm_access")?.Value;
        // transform Keycloak realms JSON to roles (like Entra)
        /*
         "realm_access": {
            "roles": [
              "default-roles-kcrealm",
              "offline_access",
              "uma_authorization", 
              ...
            ]
          }
         */
        if (!string.IsNullOrEmpty(realmAccessJson)) {
            try {
                using var doc = JsonDocument.Parse(realmAccessJson);
                if (doc.RootElement.TryGetProperty("roles", out var rolesElement) &&
                    rolesElement.ValueKind == JsonValueKind.Array) {
                    foreach (var role in rolesElement.EnumerateArray()) {
                        var roleName = role.GetString();
                        if (!string.IsNullOrEmpty(roleName)) {
                            newIdentity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                        }
                    }
                }
            } catch (JsonException) { /* Handle or log parsing errors */ }
        }
        AddScopeClaims(principal, newIdentity, "scope");
    }
}
