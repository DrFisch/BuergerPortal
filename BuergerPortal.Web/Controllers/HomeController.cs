using System.Diagnostics;
using BuergerPortal.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Datenschutz()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        [HttpGet("/home/testuser")]
        public IActionResult TestUser()
        {
            var lines = User.Claims.Select(c => $"{c.Type} = {c.Value}");
            return Content(string.Join("\n", lines));
        }
    }
}
