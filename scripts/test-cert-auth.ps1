$certBasePath = "$(pwd)\..\certs"
$certPath = "$certBasePath\OfflineAPI.pfx"
$certSecurePassword = ConvertTo-SecureString "OfflineAPI" -AsPlainText -Force

$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certPath, $certSecurePassword)
Write-Host "Certificate: Subject: $($cert.Subject), Thumpprint: $($cert.Thumbprint)" -ForegroundColor Green
    
$ApiResponse = Invoke-RestMethod -Uri "https://localhost:5001/api/management" -Method Get -Certificate $cert -SkipCertificateCheck 
$ApiResponse
