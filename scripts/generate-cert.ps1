# create the self-signed cert
New-PnPAzureCertificate -CommonName "localhost" -ValidYears 1 `
	-OutPfx "$(pwd)\certs\OfflineAPI.pfx" -OutCert "$(pwd)\certs\OfflineAPI.cer" `
	-CertificatePassword (ConvertTo-SecureString "OfflineAPI" -AsPlainText -Force)

