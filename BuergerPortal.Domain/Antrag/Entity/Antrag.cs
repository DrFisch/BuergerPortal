using BuergerPortal.Domain.Antrag.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Antrag.Entity
{
    public abstract class Antrag
    {
        public Guid Id { get; set; }
        public Guid ApplicantUserId { get; set; } = default!;
        public AntragTyp Typ { get; protected set; }
        public AntragStatus Status { get; set; } = AntragStatus.Entwurf;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedUtc { get; set; }

        // Postkorb-Handle aus der BundID-Anmeldung beim Einreichen – damit Statusmeldungen später ins BundID-Postfach
        // gehen können, auch wenn die Person dann nicht angemeldet ist (wie ein Fachverfahren es zum Vorgang speichert).
        public string? PostkorbHandle { get; set; }
        public byte[] RowVersion { get; set; } = default!;
    }
}
