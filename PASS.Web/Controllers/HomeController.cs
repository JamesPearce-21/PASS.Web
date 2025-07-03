using Microsoft.AspNetCore.Mvc;
using PASS.Web.Models;
using System.Diagnostics;

namespace PASS.Web.Controllers
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

        public IActionResult Bikeability()
        {
            return View();
        }

        public IActionResult Balanceability()
        {
            return View();
        }

        public IActionResult TheTeam()
        {
            return View();
        }

        public IActionResult Memberships()
        {
            return View();
        }

        public IActionResult HomeLearning()
        {
            return View();
        }

        public IActionResult MembersArea()
        {
            return View();
        }

        public IActionResult TermsAndConditions()
        {
            return View();
        }

        public IActionResult PrivacyPolicy()
        {
            return View();
        }

        public IActionResult Legal()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult FAQ()
        {
            return View();
        }

        public IActionResult Careers()
        {
            return View();
        }

        public IActionResult Testamonials()
        {
            return View();
        }

        public IActionResult Events()
        {
            return View();
        }

        [Route("schools-we-work-with")]
        public IActionResult OtherSchools()
        {
            return View();
        }

        public IActionResult UnderConstruction()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
