# Despliegue en producción (Windows, PC actual)

Runbook completo, en orden.

> ⚡ **Publicación rápida**: usa `.\deploy\publish.ps1 -DevCert -InstallTask -Shortcut -AddUser operador`
> para ejecutar los pasos 4–10 de una sola vez (ver sección [Script unificado](#script-unificado-publishps1) abajo).



## 1. Credenciales EWS (no requiere TI ni Azure AD)

El buzón vive en Exchange Server 2016 on-premise (`mail.munivalpo.cl`), no en
Exchange Online — Microsoft Graph no puede alcanzarlo, y por eso no hace
falta ningún registro de aplicación en Azure AD. Basta con la contraseña de
la propia cuenta de Active Directory del buzón (`servervalpo\cambiodedomicilio`
o equivalente).

## 2. Directorio real de comunas

Completar `data/comunas.csv` (no versionado, contiene datos de contacto reales)
con el formato de `data/comunas.example.csv`.

## 3. Configurar secretos

Copiar `src/CambioDeDomicilio/appsettings.Example.json` a
`publish/appsettings.json` (sobrescribiendo el que generó `dotnet publish`) y
completar `Router:Ews:Username` / `Router:Ews:Password`, y verificar `MailboxAddress`.

**Alternativa recomendada para no dejar el secreto en texto plano en el disco**:
usar variables de entorno (`Router__Ews__Password`, etc. — `Microsoft.Extensions.Configuration`
las lee automáticamente por el `__` como separador de sección) definidas como
variables de entorno de sistema, en vez de escribirlas en el JSON.

## 4. Certificado HTTPS para el dashboard

El dashboard web solo sirve datos por HTTPS (el puerto HTTP solo redirige,
nunca entrega contenido) — esto protege tanto las contraseñas de login como
los nombres/RUTs que viajan por la red municipal.

En el equipo donde corre el servicio:
```powershell
dotnet dev-certs https --trust
```

Para que los colegas que entren desde otro PC de la red no vean advertencia
de certificado no confiable, hay que exportar el certificado e instalarlo en
el almacén "Entidades de certificación raíz de confianza" de cada PC que
vaya a mirar el dashboard:
```powershell
dotnet dev-certs https --export-path .\dev-cert.pfx --password <clave-temporal>
# copiar dev-cert.pfx al otro PC e importarlo con certmgr.msc, o:
Import-PfxCertificate -FilePath .\dev-cert.pfx -CertStoreLocation Cert:\LocalMachine\Root -Password (ConvertTo-SecureString "<clave-temporal>" -AsPlainText -Force)
```

## 5. Crear el primer usuario del dashboard

```powershell
cd publish
.\CambioDeDomicilio.exe --add-user operador
```
Pide la contraseña por consola (no se muestra en pantalla). Repetir con
`--add-user <nombre>` por cada colega que necesite acceso; `--remove-user <nombre>`
para dar de baja a alguien.

## 6. Publicar

Desde la raíz del repo:
```powershell
dotnet publish src/CambioDeDomicilio -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## 7. Copiar los datos de runtime junto al publicado

El ejecutable corre con `publish/` como directorio de trabajo (ver
`install-task.ps1`), y las rutas en `appsettings.json` (`data/router.db`,
`data/comunas.csv`, `data/reporte.csv`) son relativas a ese directorio:
```powershell
Copy-Item -Recurse -Force ..\data publish\data
```
(o edita las rutas en `publish/appsettings.json` para que sean absolutas).

## 8. Instalar la tarea programada

Como Administrador:
```powershell
.\deploy\install-task.ps1
```

## 9. Iniciar y verificar

```powershell
Start-ScheduledTask -TaskName CambioDeDomicilio
Get-ScheduledTask -TaskName CambioDeDomicilio | Get-ScheduledTaskInfo
```

Confirmar que `data/reporte.csv` se crea/actualiza tras el primer ciclo
(hasta `PollIntervalMinutes` minutos después de iniciar), y que
`https://localhost:5001` (o `https://<nombre-del-pc>:5001` desde otro equipo
de la red) muestra la pantalla de ingreso del dashboard.

## 10. Acceso directo de escritorio (opcional)

Para que el operador tenga un ícono que abra el dashboard directamente:
```powershell
.\deploy\create-desktop-shortcut.ps1
```
Crea "CambioDeDomicilio - Dashboard" en el Escritorio: al hacer doble clic,
inicia el proceso si no está corriendo y abre el dashboard en el navegador.

## Desinstalar

```powershell
.\deploy\uninstall-task.ps1
```

## Copiar la instalación a otro PC

Con el publish self-contained (`--self-contained -p:PublishSingleFile=true`),
basta con copiar la carpeta `publish/` completa (incluida `data/`) al otro
equipo y ejecutar `CambioDeDomicilio.exe` — no requiere tener el runtime de
.NET instalado. El certificado HTTPS local sigue siendo necesario ahí también
si ese equipo va a servir el dashboard (paso 4).

## Script unificado: publish.ps1

El script [`deploy/publish.ps1`](publish.ps1) ejecuta los pasos 4–10 en un solo comando:

**Uso básico** (solo publica):
```powershell
.\deploy\publish.ps1
```

**Despliegue completo** (como Administrador):
```powershell
.\deploy\publish.ps1 -DevCert -InstallTask -Shortcut -AddUser operador
```

**Segundo PC** (solo copia datos + certificado):
```powershell
.\deploy\publish.ps1 -ConfigOnly -DevCert
```

| Parámetro | Qué hace |
|---|---|
| `-DevCert` | Instala el certificado HTTPS de desarrollo (paso 4) |
| `-AddUser nombre` | Crea un usuario del dashboard (paso 5) |
| `-InstallTask` | Registra la tarea programada (paso 8, requiere Admin) |
| `-Shortcut` | Crea acceso directo en escritorio (paso 10) |
| `-ConfigOnly` | Solo copia datos y configuración, sin publicar |
| `-PublishDir ruta` | Directorio de salida (default: `./publish`)
