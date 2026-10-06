using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.PostkorbSimulation.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => View();

        public IActionResult Hinweise() => View();

        // Ziel von UseExceptionHandler außerhalb von Development: allgemeiner Text, keine technischen Details.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}
