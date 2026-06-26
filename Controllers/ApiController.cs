using Keycloak.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OfflineAPI.Controllers {
    [ApiController]
    //[Route("[controller]")]
    public class ApiController : ControllerBase {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiController> _log;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKeycloakTokenAcquisition _keycloak;
        string _clientId;
        string _clientSecret;
        public ApiController( IConfiguration configuration, ILogger<ApiController> log, IHttpClientFactory httpClientFactory, IKeycloakTokenAcquisition keycloak) {
            _configuration = configuration;
            _log = log;
            _httpClientFactory = httpClientFactory;
            _keycloak = keycloak;
            _clientId = _configuration["Authentication:Keycloak:ClientId"]!.ToString();
            _clientSecret = _configuration["Authentication:Keycloak:ClientSecret"]!.ToString();
        }
        private object CreateResponseBody( string? downstreamData ) {
            var identityName = User.Identity?.Name ?? "unknown";
            var authMethod = User.Identity?.AuthenticationType; 
            Claim? idClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            Claim? appIdClaim = User.Claims.FirstOrDefault(c => c.Type == "appid");
            Claim? issClaim = User.Claims.FirstOrDefault(c => c.Type == "iss");
            var clientCert = HttpContext.Connection.ClientCertificate;
            string certIssuer = clientCert?.Subject ?? "";
            string certThumbprint = clientCert?.Thumbprint ?? "";
            return new {
                Url = this.Request.Path.Value,
                AuthMethod = authMethod,
                iss = issClaim?.Value ?? "",
                User = identityName,
                id = idClaim?.Value ?? "",
                appid = appIdClaim?.Value ?? "",
                certIssuer = certIssuer,
                certThumbprint = certThumbprint,
                downstream = downstreamData
            };
        }
        private HttpClient CreateHttpClient( bool useCertificate ) {
            if ( useCertificate ) {
                return _httpClientFactory.CreateClient("CertBasedAuthClient");
            } else {
                return new HttpClient();
            }
        }
        [Authorize]
        [HttpGet("/api/whoami")]
        public IActionResult GetWhoAmI() {
            return Ok(CreateResponseBody(null));
        }

        [Authorize]
        [HttpGet("/api/management")]
        public async Task<IActionResult> Management([FromQuery] string auth = "keycloak") {            
            // if explicitly requesting downstream internal auth should be certificate - or incoming call uses cert - then use cert
            bool useCertificate = (auth == "cert" || auth == "certificate" || User.Identity?.AuthenticationType == "Cert");

            HttpClient client = CreateHttpClient(useCertificate);
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{this.Request.Scheme}://{this.Request.Host}/api/internal");
            if ( !useCertificate) {
                (string? accessToken, string? errorMessage) = await _keycloak.AcquireAccessToken( _clientId, _clientSecret );
                if ( !string.IsNullOrWhiteSpace(errorMessage)) {
                    return BadRequest(errorMessage);
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
            using HttpResponseMessage response = await client.SendAsync(request);
            string? data = string.Empty;
            if (response.IsSuccessStatusCode) {
                data = await response.Content.ReadAsStringAsync();
            } else {
                data = $"Failed with status code: {response.StatusCode}";
            }
            return Ok(CreateResponseBody(data));
        }

        [Authorize(Policy = "RequireCertOrKeycloak")]
        [Authorize]
        [HttpGet("/api/internal")]
        public IActionResult InternalOnly() {
            return Ok(CreateResponseBody(null));
        }
    }
}
