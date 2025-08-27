using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace PASS.Web.Controllers
{
    public class MembersController : Controller
    {
        public IActionResult Index()
        {
            // Check if user is logged in via session
            var username = HttpContext.Session.GetString("Username");
            ViewBag.IsLoggedIn = !string.IsNullOrEmpty(username);

            // Render the same view
            return View("~/Views/Members/MembersArea.cshtml");
        }
    }
}
