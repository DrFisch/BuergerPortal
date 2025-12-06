using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Appointments.Abstractions
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
