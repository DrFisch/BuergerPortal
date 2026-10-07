using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.PostkorbSimulation.Controllers
{
    public class HomeController : Controller
    {
        // Startadresse https://bundid.<domain>/postfach/ führt direkt ins Postfach (ohne Sitzung über die BundID-Anmeldung) –
        // gleiche Adresse wie beim Postfach im Simulator, das Portal verlinkt in beiden Varianten dieselbe URL.
        // Mit ?anmeldung=… (abgebrochen, abgemeldet) zeigt die Seite den Hinweis.
        public IActionResult Index(string? anmeldung) =>
            string.IsNullOrEmpty(anmeldung) ? RedirectToAction("Index", "Postfach") : View();

        public IActionResult Hinweise() => View();

        // Ziel von UseExceptionHandler außerhalb von Development: allgemeiner Text, keine technischen Details.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}
