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

/// <summary>Einzelansicht einer Nachricht. Content ist null, solange das Niveau der Sitzung nicht reicht.</summary>
public sealed class PostfachMessageViewModel
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Sender { get; init; }
    public required string Service { get; init; }
    public string? ReplyAddress { get; init; }
    public required DateTime Received { get; init; }
    public DateTime? FirstRead { get; init; }
    public required int StorkQaaLevel { get; init; }
    public required int SessionLevel { get; init; }
    public string? Content { get; init; }
    public bool RequiresHigherLevel => StorkQaaLevel > SessionLevel;
}

public sealed class PostfachIndexViewModel
{
    public required string DisplayName { get; init; }
    public required int SessionLevel { get; init; }
    public required IReadOnlyList<PostfachListItem> Messages { get; init; }
    public int UnreadCount => Messages.Count(m => m.Unread);
}
