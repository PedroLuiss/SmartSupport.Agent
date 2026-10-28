using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartSupport.Agent.Models;
using SmartSupport.Agent.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;

namespace SmartSupport.Agent.Controllers;

[ApiController]
[Route("api/tickets")]
public sealed class TicketsController(
    SupportAgentService supportAgent,
    ILogger<TicketsController> logger) : ControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private const int MaxPdfPages = 100;
    private const int MaxTextLength = 20_000;

    [HttpPost("analyze")]
    [ProducesResponseType<AutomatedTicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> AnalyzeText(
        [FromBody] IncomingTicketDto request,
        CancellationToken cancellationToken) =>
        AnalyzeContentAsync(request.RawContent, cancellationToken);

    [HttpPost("analyze-file")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileSize + 64 * 1024)]
    [ProducesResponseType<AutomatedTicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AnalyzeFile(
        [FromForm(Name = "file")] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Archivo requerido",
                Detail = "Adjunta un archivo TXT o PDF no vacío.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (file.Length > MaxFileSize)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
            {
                Title = "Archivo demasiado grande",
                Detail = "El tamaño máximo permitido es 10 MB.",
                Status = StatusCodes.Status413PayloadTooLarge
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Formato no admitido",
                Detail = "Solo se admiten archivos .txt y .pdf.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        await using var stream = file.OpenReadStream();
        string content;
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var reader = new StreamReader(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                    detectEncodingFromByteOrderMarks: true,
                    leaveOpen: true);
                content = await reader.ReadToEndAsync(cancellationToken);
            }
            catch (DecoderFallbackException)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Codificación de texto inválida",
                    Detail = "El archivo TXT debe estar codificado en UTF-8.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }
        else
        {
            try
            {
                using var document = PdfDocument.Open(stream);
                if (document.NumberOfPages > MaxPdfPages)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "PDF demasiado extenso",
                        Detail = $"El máximo permitido es de {MaxPdfPages} páginas.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                content = string.Join('\n', document.GetPages().Select(page => page.Text));
            }
            catch (PdfDocumentFormatException exception)
            {
                logger.LogInformation(exception, "Se rechazó un PDF con formato inválido.");
                return BadRequest(new ProblemDetails
                {
                    Title = "PDF inválido",
                    Detail = "No se pudo leer el documento PDF.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "No se encontró texto",
                Detail = "El archivo no contiene texto legible. Los PDF escaneados requieren OCR.",
                Status = StatusCodes.Status422UnprocessableEntity
            });
        }

        if (content.Length > MaxTextLength)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Documento demasiado extenso",
                Detail = $"El texto extraído supera el límite de {MaxTextLength} caracteres.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return await AnalyzeContentAsync(content, cancellationToken);
    }

    private async Task<IActionResult> AnalyzeContentAsync(
        string content,
        CancellationToken cancellationToken)
    {
        if (!supportAgent.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Servicio de IA no configurado",
                Detail = "Define AISettings__ApiKey en el entorno para habilitar el análisis.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }

        try
        {
            return Ok(await supportAgent.ProcessTicketAsync(content, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Contenido inválido",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "No se pudo interpretar la respuesta de IA.");
            return Problem(
                title: "Respuesta de IA inválida",
                detail: "El modelo devolvió una respuesta que no cumple el formato esperado.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "No se pudo iniciar el análisis del ticket.");
            return Problem(
                title: "Servicio de IA no disponible",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
