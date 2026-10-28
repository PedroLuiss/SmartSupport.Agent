# SmartSupport.Agent

Web API en .NET 10 que usa un modelo de IA para analizar reportes de soporte y facturación. Extrae datos clave, clasifica urgencia y sentimiento, propone una respuesta al cliente y, cuando hay información suficiente, genera un borrador de asiento contable.

## Requisitos

- .NET 10 SDK
- Una API key de OpenAI

## Ejecutar

Configura la clave como variable de entorno; no la guardes en `appsettings.json` ni en el repositorio.

```powershell
$env:AISettings__ApiKey = "tu-api-key"
dotnet run
```

La aplicación lee el modelo de `AISettings:ModelName` (por defecto `gpt-4o-mini`). Sin una API key, la API responde `503 Service Unavailable` en los endpoints de análisis.

## Endpoints

### Analizar texto

`POST /api/tickets/analyze`

```json
{
  "rawContent": "Soy Ana López. No puedo descargar la factura INV-2048 por $1,250 USD y necesito resolverlo hoy."
}
```

### Analizar un archivo

`POST /api/tickets/analyze-file` con `multipart/form-data` y un campo `file`. Admite archivos `.txt` en UTF-8 y PDF con texto seleccionable. Los PDF escaneados no incluyen texto extraíble y requieren OCR, que no está incluido. El límite es 10 MB por archivo, 100 páginas por PDF y 20.000 caracteres extraídos.

La colección [SmartSupport.Agent.http](./SmartSupport.Agent.http) contiene ejemplos listos para ejecutar desde Visual Studio Code o Visual Studio.

## Respuesta

El análisis devuelve el nombre del cliente, categoría, urgencia, sentimiento, resumen, datos extraídos y un borrador de respuesta. `suggestedAccountingEntry` será `null` si el reporte no contiene información contable suficiente; si se genera, es solo una sugerencia que debe revisar una persona. La API no envía respuestas ni registra asientos automáticamente.

Los documentos se procesan en memoria y no se guardan en disco. El endpoint de OpenAPI está habilitado en el entorno de desarrollo.
