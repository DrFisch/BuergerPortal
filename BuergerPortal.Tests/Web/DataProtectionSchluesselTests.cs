using BuergerPortal.Web.Controllers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BuergerPortal.Tests.Web
{
    /// <summary>
    /// X4: Mit Zertifikat liegen die Data-Protection-Schlüssel des Portals verschlüsselt im Schlüsselordner; ein neuer Start
    /// (andere Instanz, gleicher Ordner) entschlüsselt mit demselben Zertifikat.
    /// </summary>
    public class DataProtectionSchluesselTests
    {
        private sealed class PortalFactory(string keysPath, string? certificatePath) : WebApplicationFactory<HomeController>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:BaseUrl"] = "http://api.invalid/",
                    ["Authentication:Authority"] = "http://auth.invalid/",
                    ["Authentication:ClientId"] = "test",
                    ["Authentication:ClientSecret"] = "test",
                }));
                // Program.cs liest diese Werte schon vor Build(); UseSetting wirkt ab dem Erzeugen des Builders
                builder.UseSetting("DataProtection:KeysPath", keysPath);
                if (certificatePath != null)
                {
                    builder.UseSetting("DataProtection:CertificatePath", certificatePath);
                    builder.UseSetting("DataProtection:CertificatePassword", "test-passwort");
                }
            }
        }

        private static string NeuesZertifikat(string ordner)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=bpsim Data Protection Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            var pfad = Path.Combine(ordner, "dataprotection.pfx");
            File.WriteAllBytes(pfad, certificate.Export(X509ContentType.Pfx, "test-passwort"));
            return pfad;
        }

        [Fact]
        public void Schluessel_mit_Zertifikat_verschluesselt_und_nach_Neustart_lesbar()
        {
            var ordner = Directory.CreateTempSubdirectory("bpsim-dp-").FullName;
            try
            {
                var zertifikat = NeuesZertifikat(ordner);
                var schluessel = Path.Combine(ordner, "keys");
                string geschuetzt;
                using (var erster = new PortalFactory(schluessel, zertifikat))
                {
                    geschuetzt = erster.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("geheim");
                }

                var xml = string.Concat(Directory.GetFiles(schluessel, "key-*.xml").Select(File.ReadAllText));
                Assert.Contains("encryptedSecret", xml);
                Assert.DoesNotContain("<masterKey", xml);   // Schlüsselmaterial nur im verschlüsselten Teil

                using var zweiter = new PortalFactory(schluessel, zertifikat);
                Assert.Equal("geheim", zweiter.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Unprotect(geschuetzt));
            }
            finally
            {
                Directory.Delete(ordner, recursive: true);
            }
        }

        [Fact]
        public void Ohne_Zertifikat_wie_bisher_unverschluesselt()
        {
            var ordner = Directory.CreateTempSubdirectory("bpsim-dp-").FullName;
            try
            {
                var schluessel = Path.Combine(ordner, "keys");
                using (var portal = new PortalFactory(schluessel, null))
                {
                    portal.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("x");
                }

                Assert.DoesNotContain("encryptedSecret", string.Concat(Directory.GetFiles(schluessel, "key-*.xml").Select(File.ReadAllText)));
            }
            finally
            {
                Directory.Delete(ordner, recursive: true);
            }
        }
    }
}
