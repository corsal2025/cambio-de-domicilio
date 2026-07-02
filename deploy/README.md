# Despliegue en producción (Windows, PC actual)

Runbook completo, en orden. Los pasos 1 y 2 son el único bloqueo real hoy —
todo lo demás ya está preparado y probado.

## 1. Azure AD (bloqueante — requiere TI del municipio)

Coordinar el registro de la app siguiendo los pasos de la sección
"Registro de la app en Azure AD" del `README.md` raíz. Obtener:
- `TenantId`
- `ClientId`
- `ClientSecret`

Sin esto, el servicio arranca pero cada ciclo de sondeo falla la autenticación
contra Graph (no se cae el proceso, pero tampoco procesa correos reales).

## 2. Directorio real de comunas

Completar `data/comunas.csv` (no versionado, contiene datos de contacto reales)
con el formato de `data/comunas.example.csv`.

## 3. Configurar secretos

Copiar `src/OutlookComunaRouter/appsettings.Example.json` a
`publish/appsettings.json` (sobrescribiendo el que generó `dotnet publish`) y
completar `TenantId`, `ClientId`, `ClientSecret`, y verificar `MailboxAddress`.

**Alternativa recomendada para no dejar el secreto en texto plano en el disco**:
usar variables de entorno (`Router__ClientSecret`, etc. — `Microsoft.Extensions.Configuration`
las lee automáticamente por el `__` como separador de sección) definidas como
variables de entorno de sistema, en vez de escribirlas en el JSON.

## 4. Publicar

Desde la raíz del repo:
```powershell
dotnet publish src/OutlookComunaRouter -c Release -o publish
```

## 5. Copiar los datos de runtime junto al publicado

El ejecutable corre con `publish/` como directorio de trabajo (ver
`install-task.ps1`), y las rutas en `appsettings.json` (`data/router.db`,
`data/comunas.csv`, `data/reporte.csv`) son relativas a ese directorio:
```powershell
Copy-Item -Recurse -Force ..\data publish\data
```
(o edita las rutas en `publish/appsettings.json` para que sean absolutas).

## 6. Instalar la tarea programada

Como Administrador:
```powershell
.\deploy\install-task.ps1
```

## 7. Iniciar y verificar

```powershell
Start-ScheduledTask -TaskName OutlookComunaRouter
Get-ScheduledTask -TaskName OutlookComunaRouter | Get-ScheduledTaskInfo
```

Confirmar que `data/reporte.csv` se crea/actualiza tras el primer ciclo
(hasta `PollIntervalMinutes` minutos después de iniciar).

## Desinstalar

```powershell
.\deploy\uninstall-task.ps1
```
