@echo off

IF "%~1" == "" GOTO OnArgError
IF "%~2" == "" GOTO OnArgError
IF "%~3" == "" GOTO OnArgError

set openssl_path=c:\Program Files\OpenSSL-Win64\bin\
set srvname=%~1
set rootCertName=#RpcDevCA
set root_password=%~2
set password=%~3

echo(
"%openssl_path%openssl" genrsa -out %srvname%.key 2048

echo(
echo Generating sign request...
"%openssl_path%openssl" req -new -sha256 -key %srvname%.key -subj "/C=LV/ST=LV/O=SoftFx/CN=#RPC Dev %srvname%" -out %srvname%.csr -addext "subjectAltName=DNS:%srvname%"
rem "%openssl_path%openssl" req -in %srvname%.csr -noout -text

if '%ERRORLEVEL%' NEQ '0' goto Exit
	
echo(
echo Generating certificate...
echo subjectAltName=DNS:%srvname%>>san.ext.txt
"%openssl_path%openssl" x509 -req -in %srvname%.csr -CA %rootCertName%.crt -CAkey %rootCertName%.key -passin pass:%root_password% -CAcreateserial -out %srvname%.crt -days 3000 -sha256 -extfile san.ext.txt
rem "%openssl_path%openssl" x509 -in %srvname%.crt -text -noout
del san.ext.txt

if '%ERRORLEVEL%' NEQ '0' goto Exit

echo(
echo Exporting the certificate to PFX format...
"%openssl_path%openssl" pkcs12 -inkey %srvname%.key -in %srvname%.crt -export -out %srvname%.pfx -passout pass:%password%

goto Exit

:OnArgError
echo Usage:
echo    gen.server.cert.bat [server_name] [root_key_password] [password]

:Exit


