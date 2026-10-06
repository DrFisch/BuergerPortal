using BuergerPortal.PostkorbSimulation.Auth;
using BuergerPortal.PostkorbSimulation.Data;
using BuergerPortal.PostkorbSimulation.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BuergerPortal.PostkorbSimulation.Controllers;

/// <summary>
/// "Mein BundID-Postfach (Simulation)": Nachrichten an das Postkorb-Handle der angemeldeten Person.
/// Das Handle kommt ausschließlich aus der BundID-Anmeldung (Sitzung), nie aus der URL.
/// </summary>
[Authorize]
public class PostfachController(PostkorbDbContext db, TimeProvider time) : Controller
{
    private const int MaxMessages = 200;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.GetPostkorbHandle() is not { } handle)
        {
            return await RestartLoginAsync(Url.Action(nameof(Index)));
        }

        var sessionLevel = User.GetTrustLevel();
        var messages = await db.Messages.AsNoTracking()
            .Where(m => m.MailboxUuid == handle)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(MaxMessages)
            .Select(m => new { m.Id, m.Title, m.Sender, m.Service, m.CreatedUtc, m.ReadUtc, m.StorkQaaLevel })
            .ToListAsync(ct);

        return View(new PostfachIndexViewModel
        {
            DisplayName = User.Identity?.Name ?? string.Empty,
            SessionLevel = sessionLevel,
            Messages = messages.Select(m => new PostfachListItem(m.Id, m.Title, m.Sender, m.Service,
                Anzeigezeit.FromUtc(m.CreatedUtc), m.ReadUtc == null, m.StorkQaaLevel,
                m.StorkQaaLevel > sessionLevel)).ToList(),
        });
    }

    [HttpGet("Postfach/Nachricht/{id:guid}")]
    public async Task<IActionResult> Nachricht(Guid id, CancellationToken ct)
    {
        if (User.GetPostkorbHandle() is not { } handle)
        {
            return await RestartLoginAsync(Url.Action(nameof(Nachricht), new { id }));
        }

        // Nur im eigenen Postfach suchen: fremde IDs liefern 404 und verraten nicht, ob es sie gibt.
        var message = await db.Messages.SingleOrDefaultAsync(m => m.Id == id && m.MailboxUuid == handle, ct);
        if (message == null)
        {
            return NotFound();
        }

        var sessionLevel = User.GetTrustLevel();
        var readable = message.StorkQaaLevel <= sessionLevel;

        // Als gelesen gilt eine Nachricht erst, wenn ihr Inhalt angezeigt wurde.
        if (readable && message.ReadUtc == null)
        {
            message.ReadUtc = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }

        return View(new PostfachMessageViewModel
        {
            Id = message.Id,
            Title = message.Title,
            Sender = message.Sender,
            Service = message.Service,
            ReplyAddress = message.ReplyAddress,
            Received = Anzeigezeit.FromUtc(message.CreatedUtc),
            FirstRead = message.ReadUtc is { } read ? Anzeigezeit.FromUtc(read) : null,
            StorkQaaLevel = message.StorkQaaLevel,
            SessionLevel = sessionLevel,
            Content = readable ? message.Content : null,
        });
    }

    // Sitzung ohne Postkorb-Handle sollte nicht vorkommen (der Login verlangt eines) – verwerfen und neu anmelden.
    private async Task<IActionResult> RestartLoginAsync(string? returnUrl)
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction("Login", "BundId", new { returnUrl });
    }
}
