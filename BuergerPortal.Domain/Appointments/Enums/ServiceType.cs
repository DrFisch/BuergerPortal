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
        AllgemeineBeratung = 1,

        DokumenteUndAusweise = 2,

        Meldeangelegenheiten = 3,

        Sonstiges = 4
    }
}
