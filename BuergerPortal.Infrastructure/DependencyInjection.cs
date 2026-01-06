using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Application.Interfaces.UserEinstellungen;
using BuergerPortal.Infrastructure.Database.Repositories;
using BuergerPortal.Infrastructure.Persistence;
using BuergerPortal.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var cs = configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<PortalDbContext>(opt =>
                opt.UseSqlServer(cs));

            services.AddScoped<IAppointmentRepository, AppointmentRepository>();
            services.AddScoped<IReisepassRepository, ReisepassRepository>();
            services.AddScoped<ISperrmuellRepository, SperrmuellRepository>();
            services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
            services.AddScoped<IPoiRepository, PoiRepository>();

            return services;
        }
    }
}
