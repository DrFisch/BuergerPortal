namespace BuergerPortal.PostkorbSimulation.Models;

/// <summary>Eine Zeile in der Nachrichtenliste.</summary>
public sealed record PostfachListItem(
    Guid Id,
    string Title,
    string Sender,
    string Service,
    DateTime Received,
    bool Unread,
    int StorkQaaLevel,
    // Nachricht verlangt ein höheres Vertrauensniveau als die aktuelle Sitzung.
    bool RequiresHigherLevel);

public sealed class PostfachIndexViewModel
{
    public required string DisplayName { get; init; }
    public required int SessionLevel { get; init; }
    public required IReadOnlyList<PostfachListItem> Messages { get; init; }
    public int UnreadCount => Messages.Count(m => m.Unread);
}
