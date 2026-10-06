using BuergerPortal.PostkorbSimulation.Api;
using BuergerPortal.PostkorbSimulation.Data;
using BuergerPortal.PostkorbSimulation.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.PostkorbSimulation.Controllers;

/// <summary>
/// REST-Schnittstelle für Behörden-Dienste nach dem Vorbild von ZBP "CreateMessage".
/// Im Betrieb nur im internen Container-Netz erreichbar, nicht über den Reverse Proxy.
/// </summary>
[ApiController]
[Route("api/v1/messages")]
[RequireApiKey]
public class MessagesApiController(PostkorbDbContext db, TimeProvider time, ILogger<MessagesApiController> logger)
    : ControllerBase
{
    [HttpPost]
    // content darf 1 MB Zeichen lang sein; als UTF-8-JSON mit Escapes kann der Body größer werden.
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<CreateMessageResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateMessageResponse>> Create(CreateMessageRequest request, CancellationToken ct)
    {
        var message = request.ToMessage(time.GetUtcNow().UtcDateTime);
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);

        // Kein Inhalt und kein Postkorb-Handle im Log.
        logger.LogInformation("Nachricht {MessageId} von {Sender} ({Service}) angenommen.",
            message.Id, message.Sender, message.Service);
        return StatusCode(StatusCodes.Status201Created, new CreateMessageResponse(message.Id));
    }
}
