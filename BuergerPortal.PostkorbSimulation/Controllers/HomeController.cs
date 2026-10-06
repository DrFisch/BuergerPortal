using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.PostkorbSimulation.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => View();
    }
}
