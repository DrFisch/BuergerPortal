using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Appointments.Enums
{
    public enum ServiceType
    {
        [Display(Name = "Allgemeine Beratung")]
        AllgemeineBeratung = 1,

        [Display(Name = "Dokumente und Ausweise")]
        DokumenteUndAusweise = 2,

        [Display(Name = "Meldeangelegenheiten")]
        Meldeangelegenheiten = 3,

        [Display(Name = "Rückfrage zum Antrag")]
        AntragRueckfrage = 4,

        [Display(Name = "Sonstiges")]
        Sonstiges = 5
    }
}
