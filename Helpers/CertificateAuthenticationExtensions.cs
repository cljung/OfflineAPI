using Microsoft.AspNetCore.Authentication.Certificate;
using OfflineAPI.Helpers;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace OfflineAPI;
public class TrustedCertificateStore {
    public static X509Certificate2Collection? store { get; set; }
    public static X509Certificate2? clientCert { get; set; }
}

public static class CertificateAuthenticationExtensions {
    private static ILogger<Program> _log;
    public static WebApplicationBuilder AddCertificateBasedAuthentication(this WebApplicationBuilder builder) {
        // to have Kestrel accept certificates + self-signed
        builder.WebHost.ConfigureKestrel(options => {
            options.ConfigureHttpsDefaults(httpsOptions => {
                httpsOptions.ClientCertificateMode = Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.AllowCertificate;
                httpsOptions.ClientCertificateValidation = (certificate, chain, sslPolicyErrors) => { return true; };
            });
        });

        // to handle if you're behind a reverse proxy, like nginx, ngrok, etc
        builder.Services.AddCertificateForwarding(options => {
            options.CertificateHeader = "X-Client-Cert";
            options.HeaderConverter = (headerValue) => {
                return X509CertificateLoader.LoadCertificate(Convert.FromBase64String(headerValue));
            };
        });

        using var serviceProvider = builder.Services.BuildServiceProvider();
        _log = serviceProvider.GetRequiredService<ILogger<Program>>();

        // load all trusted/valid certificates that clients can use
        try {
            // certs we accept from incoming calls (we don't need to know the cert passphrase and therefor we use the .cer file(s)
            string[] validCerts = builder.Configuration.GetSection("Authentication:Certificate:ValidCertsPaths").Get<string[]>() ?? [];
            TrustedCertificateStore.store = LoadValidCertificates( validCerts, builder.Environment.ContentRootPath);
            // cert we use when making outgoing API calls. Here we need to know the passprase and therefor we use a .pfx file
            string certPath = builder.Configuration["Authentication:Certificate:CertPath"]!.ToString();
            if (!certPath.Contains(":\\")) {
                certPath = Path.Combine(builder.Environment.ContentRootPath, certPath);
            }
            string certPassphrase = builder.Configuration["Authentication:Certificate:CertPassphrase"]!.ToString();
            SecureString secureString = new SecureString();
            certPassphrase.ToCharArray().ToList().ForEach(p => secureString.AppendChar(p));
            TrustedCertificateStore.clientCert = new X509Certificate2(certPath, secureString);
        } catch (Exception ex) {
            _log.LogError($"Failed to load certificates. Use CER files for ValidCertsPaths and PFX for CertPath. {ex.Message}");
        }

        builder.Services.AddHttpClient("CertBasedAuthClient")
            .ConfigurePrimaryHttpMessageHandler(() => {
                var handler = new SocketsHttpHandler();
                handler.SslOptions.ClientCertificates = new X509Certificate2Collection();
                handler.SslOptions.ClientCertificates.Add(TrustedCertificateStore.clientCert);
                handler.SslOptions.RemoteCertificateValidationCallback = (sender, c, ch, e) => true;
                return handler;
            });

        return builder;
    }
    private static bool ValidateClientCertificate(CertificateValidatedContext context) {
        X509Certificate2 clientCert = context.ClientCertificate;
        bool ok = false;
        foreach (var allowedCert in TrustedCertificateStore.store!) {
            if (clientCert.Issuer.Equals(allowedCert.Issuer, StringComparison.OrdinalIgnoreCase)
                && clientCert.Thumbprint.Equals(allowedCert.Thumbprint, StringComparison.OrdinalIgnoreCase)) {
                var claims = new[] {
                            new Claim(ClaimTypes.NameIdentifier, clientCert.Thumbprint, ClaimValueTypes.String, context.Options.ClaimsIssuer),
                            new Claim(ClaimTypes.Name, clientCert.Subject, ClaimValueTypes.String, context.Options.ClaimsIssuer),
                            new Claim(ClaimTypes.Role, "ApiCert", ClaimValueTypes.String, context.Options.ClaimsIssuer)
                        };
                context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                context.Success();
                ok = true;
            }
        }
        if (!ok) {
            string msg = "Certificate is " + (ok ? "" : "not ") + $"trusted. Subject: {context.ClientCertificate.Subject}, Thumbprint: {context.ClientCertificate.Thumbprint}";
            context.Fail(msg);
        }
        return ok;
    }

    // Load all valid certificates defined in config
    private static X509Certificate2Collection LoadValidCertificates(string[] validCertsPaths, string contentRootPath) {
        X509Certificate2Collection store = new X509Certificate2Collection();
        foreach (var certPath in validCertsPaths) {
            string path = certPath;
            // LoadCertificateFromFile doesn't like relative paths
            if (!path.Contains(":\\")) {
                path = Path.Combine(contentRootPath, certPath);
            }
            X509Certificate2 cert = X509CertificateLoader.LoadCertificateFromFile(path);
            store.Add(cert!);
        }
        return store;
    }
    public static CertificateAuthenticationOptions AddCertificateAuthenticationOptions(this CertificateAuthenticationOptions options) {
        options.AllowedCertificateTypes = CertificateTypes.All;                 // both chained and self-signed
        options.ChainTrustValidationMode = X509ChainTrustMode.CustomRootTrust;  // we have our own cert store
        options.CustomTrustStore = TrustedCertificateStore.store!;              // our cert store
        options.RevocationMode = X509RevocationMode.NoCheck;                    // we don't check for revocation due to self-signed
        options.ValidateCertificateUse = false;                                 // due to self-signed
        options.Events = new CertificateAuthenticationEvents {
            OnCertificateValidated = context => {
                bool valid = ValidateClientCertificate(context);
                string msg = "Certificate is " + (valid ? "" : "not ") + $"trusted. Subject: {context.ClientCertificate.Subject}, Thumbprint: {context.ClientCertificate.Thumbprint}";
                _log.LogInformation(msg);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context => {
                string errmsg = $"Authentication failed during certificate evaluation. {context.Exception.Message}";
                _log.LogInformation(errmsg);
                context.Fail(errmsg);
                return Task.CompletedTask;
            }
        };
        return options;
    }
} // cls
