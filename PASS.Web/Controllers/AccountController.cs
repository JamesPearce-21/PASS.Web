using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using PASS.Web.Models;
using PASS.Web.Helpers;

namespace PASS.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            var accounts = _configuration.GetSection("UserAccounts").Get<List<UserAccount>>();

            var trimmedPassword = password.Trim();
            var hashedPassword = PasswordHelper.ComputeSha256Hash(trimmedPassword);

            var user = accounts.FirstOrDefault(u =>
                u.Username.Trim().Equals(username.Trim(), StringComparison.OrdinalIgnoreCase)
                && u.PasswordHash == hashedPassword);

            if (user != null)
            {
                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("Role", user.Role);

                return RedirectToAction("Index", "Members"); // serve MembersArea.cshtml
            }

            TempData["LoginError"] = "Invalid username or password";
            return RedirectToAction("Index", "Members");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
