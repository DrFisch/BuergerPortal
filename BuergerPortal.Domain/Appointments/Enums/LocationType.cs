using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Appointments.Enums
{
    public enum LocationType
    {
        [Display(Name = "Bürgeramt Mitte")]
        BuergermtMitte = 1,

        [Display(Name = "Bürgeramt Nord")]
        BuergermtNord = 2,

        [Display(Name = "Bürgeramt Süd")]
        BuergermtSued = 3
    }
}
