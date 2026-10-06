using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.PostkorbSimulation.Models;

/// <summary>
/// Eingehende Nachricht am REST-Endpunkt, nach dem Vorbild von ZBP "CreateMessage".
/// Abweichung: ein JSON-Objekt mit den Feldern direkt, statt "content" + Signatur in einer Hülle.
/// </summary>
public sealed class CreateMessageRequest : IValidatableObject
{
    [Required]
    public Guid? MailboxUuid { get; init; }

    [Required, StringLength(PostkorbMessage.MaxTitleLength)]
    public string? Title { get; init; }

    [Required, StringLength(PostkorbMessage.MaxContentLength)]
    public string? Content { get; init; }

    [Required, StringLength(PostkorbMessage.MaxNameLength)]
    public string? Sender { get; init; }

    [Required, StringLength(PostkorbMessage.MaxNameLength)]
    public string? Service { get; init; }

    [EmailAddress, StringLength(PostkorbMessage.MaxAddressLength)]
    public string? ReplyAddress { get; init; }

    // STORK-QAA-Level 1 bis 4; die BundID nutzt 1 (normal), 3 (substanziell) und 4 (hoch).
    [Range(1, 4)]
    public int StorkQaaLevel { get; init; } = 1;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MailboxUuid == Guid.Empty)
            yield return new ValidationResult("Das Postkorb-Handle darf nicht leer sein.", [nameof(MailboxUuid)]);
    }

    public PostkorbMessage ToMessage(DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        MailboxUuid = MailboxUuid!.Value,
        Title = Title!.Trim(),
        Content = Content!,
        Sender = Sender!.Trim(),
        Service = Service!.Trim(),
        ReplyAddress = string.IsNullOrWhiteSpace(ReplyAddress) ? null : ReplyAddress.Trim(),
        StorkQaaLevel = StorkQaaLevel,
        CreatedUtc = nowUtc,
    };
}
