using BuergerPortal.Application.Antraege.AntragReisepass.BusinessServices;
using BuergerPortal.Application.Antraege.AntragReisepass.DTOs;
using BuergerPortal.Application.Antraege.AntragReisepass.Validations;
using BuergerPortal.Application.Appointments.BusinessServices;
using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Appointments.Validation;
using BuergerPortal.Application.Interfaces.BusinessServices;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application
{
    public static class ApplicationDependencyInjections
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            //----------Business Services---------
            services.AddScoped<IReisepassAntragBusinessService, ReisepassAntragBusinessService>();
            services.AddScoped<IAppointmentBusinessService, AppointmentBusinessService>();

            //----------Validations---------
            services.AddScoped<IValidator<AppointmentCreateDto>, AppointmentCreateDtoValidator>();
            // Reisepass Antrag Validations
            services.AddScoped<IValidator<ReisepassStep1Dto>, ReisepassStep1Validator>();
            services.AddScoped<IValidator<ReisepassStep2Dto>, ReisepassStep2Validator>();


            return services;
        }
    }
}
