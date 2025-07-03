using Microsoft.AspNetCore.Mvc;
using PASS.Web.Models;
using System.Diagnostics;

namespace PASS.Web.Controllers
{
    public class StaffAreaController : Controller
    {

        private readonly ILogger<StaffAreaController> _logger;

        public StaffAreaController(ILogger<StaffAreaController> logger)
        {
            _logger = logger;
        }

        public IActionResult StaffHome()
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
