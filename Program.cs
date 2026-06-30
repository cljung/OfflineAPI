using Keycloak.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using OfflineAPI;
using OfflineAPI.Helpers;
using System.Runtime.ConstrainedExecution;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<ForwardedHeadersOptions>(options => {
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
});

builder.Services.AddMemoryCache();

builder.AddCertificateBasedAuthentication();

builder.Services.AddAuthentication()
    .AddJwtBearer("Entra", options => {
        options.Authority = builder.Configuration["Authentication:Entra:Authority"]!.ToString();
        options.Audience = builder.Configuration["Authentication:Entra:Audience"]!.ToString();
    })
    .AddJwtBearer("Keycloak", options => {
        options.Authority = builder.Configuration["Authentication:Keycloak:Authority"]!.ToString();
        options.Audience = builder.Configuration["Authentication:Keycloak:Audience"]!.ToString();
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>("Authentication:Keycloak:RequireHttpsMetadata");
    })
    .AddCertificate("Cert", options => {
        options.AddCertificateAuthenticationOptions();
    });

builder.Services.AddTransient<IClaimsTransformation, UnifiedClaimsTransformer>();
builder.Services.AddSingleton<IKeycloakTokenAcquisition, KeycloakTokenAcquisition>();

builder.Services.AddAuthorization(options => {
    options.DefaultPolicy = new AuthorizationPolicyBuilder( "Entra", "Keycloak", "Cert" ).RequireAuthenticatedUser().Build();
    options.AddPolicy("RequireCertOrKeycloak", policy => {
        policy.AddAuthenticationSchemes("Keycloak", "Cert");
        policy.RequireAssertion(context => {
            bool isKeycloakUser = context.User.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "uma_protection");
            bool isCertUser = context.User.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "ApiCert");
            return isKeycloakUser || isCertUser;
        });
    });
});

var app = builder.Build();

var keycloak = app.Services.GetService<IKeycloakTokenAcquisition>();
var logger = app.Services.GetService<ILogger<Program>>();
try {
    await keycloak!.LoadDiscoveryDocument();
} catch {
    logger!.LogWarning( $"No Keycloak authentication is possible until authority is available." );
}

app.UseHttpsRedirection();
app.UseCertificateForwarding();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
