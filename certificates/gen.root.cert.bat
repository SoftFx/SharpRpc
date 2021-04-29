@echo off

if "%~1" == "" goto OnArgError

set openssl_path=c:\Program Files\OpenSSL-Win64\bin\
set CertName=#RpcDevCA
set password=%~1

echo(
"%openssl_path%openssl" genrsa -des3 -out %CertName%.key -passout pass:%password% 4096 

if '%ERRORLEVEL%' NEQ '0' goto Exit

echo(
echo Generating certificate...
"%openssl_path%openssl" req -x509 -new -nodes -key %CertName%.key -sha256 -days 1024 -out %CertName%.crt -subj "/C=LV/ST=LV/O=SoftFx/CN=#RPC Development CA" -passin pass:%password%

if '%ERRORLEVEL%' NEQ '0' goto Exit

echo(
echo Exporting the certificate to PFX format...
"%openssl_path%openssl" pkcs12 -inkey %CertName%.key -in %CertName%.crt -export -out %CertName%.pfx -passin pass:%password% -passout pass:%password%

goto Exit

:OnArgError
echo Usage:
echo    gen.root.cert.bat [password]

:Exit
