using Duende.IdentityModel.Client;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Keycloak.Helpers; 
public interface IKeycloakTokenAcquisition {
    public Task<(string?, string?)> AcquireAccessToken(string clientId, string clientSecret);
}
public class KeycloakTokenAcquisition : IKeycloakTokenAcquisition {
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<KeycloakTokenAcquisition> _log;
    private string _authority;
    private DiscoveryDocumentResponse? _disco = null;
    public KeycloakTokenAcquisition( IConfiguration configuration, ILogger<KeycloakTokenAcquisition> log, IMemoryCache cache ) {
        _configuration = configuration;
        _cache = cache;
        _log = log;
        _authority = _configuration["Authentication:Keycloak:Authority"]!.ToString();
        _httpClient = new HttpClient();
    }
    private async Task LoadDiscoveryDocument() {
        if (_disco == null || (_disco != null && _disco.IsError)) {
            _log.LogTrace($"Loading discovery document: {_authority}");
            _disco = await _httpClient.GetDiscoveryDocumentAsync(_authority);
            if (_disco.IsError) {
                _log.LogError($"Failed loading discovery document: {_authority}. {_disco.Error}");
                throw new Exception(_disco.Error);
            }
        }
    }
    public static DateTimeOffset GetTokenExpiryTtl(string jwtToken) {
        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(jwtToken);
        return new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero);
    }
    public async Task<(string?, string?)> AcquireAccessToken( string clientId, string clientSecret ) {
        if ( _cache.TryGetValue(clientId, out string? token)) {
            _log.LogTrace($"Acquire access token from cache: {clientId}");
            return (token!, string.Empty);
        }
        await LoadDiscoveryDocument();
        var tokenResponse = await _httpClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest {
            Address = _disco!.TokenEndpoint,
            ClientId = clientId,
            ClientSecret = clientSecret
        });

        if ( tokenResponse.HttpStatusCode == System.Net.HttpStatusCode.OK) {
            _cache.Set(clientId, tokenResponse.AccessToken, KeycloakTokenAcquisition.GetTokenExpiryTtl(tokenResponse.AccessToken!));
            _log.LogTrace($"Caching acquired access token for: {clientId}");
            return (tokenResponse.AccessToken, string.Empty);
        } else {
            _log.LogError($"Failed acquiring access token for: {clientId}. {tokenResponse.ErrorDescription}");
            return (string.Empty, tokenResponse.ErrorDescription);
        }        
    }
}
