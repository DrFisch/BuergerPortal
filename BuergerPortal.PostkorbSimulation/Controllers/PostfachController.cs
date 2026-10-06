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
public class PostfachController(PostkorbDbContext db) : Controller
{
    private const int MaxMessages = 200;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.GetPostkorbHandle() is not { } handle)
        {
            // Sollte nicht vorkommen (Login verlangt ein Handle) – Sitzung verwerfen und neu anmelden.
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login", "BundId", new { returnUrl = Url.Action(nameof(Index)) });
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
}
