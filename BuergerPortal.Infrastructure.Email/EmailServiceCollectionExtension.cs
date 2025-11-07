using BuergerPortal.Application.Interfaces.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Infrastructure.Email
{
    public static class EmailServiceCollectionExtensions
    {
        public static IServiceCollection AddMailJetEmailSender(this IServiceCollection services, IConfiguration cfg)
        {
            services.Configure<MailJetOptions>(cfg.GetSection("MailJet"));
            services.AddScoped<IEmailSender, MailJetEmailSender>();
            return services;
        }
    }
}
