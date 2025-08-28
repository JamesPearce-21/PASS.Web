using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using PASS.Web.Models;
using PASS.Web.Helpers;
using PASS.Web.Services;
using System.Text.Json;

namespace PASS.Web.Controllers
{
    public class StaffAreaController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly BlobStorageService _blobService;
        private readonly IWebHostEnvironment _env;
        private readonly ContentService _contentService;

        public StaffAreaController(IConfiguration configuration, BlobStorageService blobService, IWebHostEnvironment env, ContentService contentService)
        {
            _configuration = configuration;
            _blobService = blobService;
            _env = env;
            _contentService = contentService;
        }

        public IActionResult Index()
        {
            var username = HttpContext.Session.GetString("Username");
            var role = HttpContext.Session.GetString("Role");

            // If logged in as staff or admin
            if (!string.IsNullOrEmpty(username) && (role == "Admin" || role == "Staff"))
            {
                bool isAdmin = role == "Admin";
                ViewBag.IsAdmin = isAdmin; // pass to view
                ViewBag.Username = username;
                return View("StaffHome");
            }

            // Otherwise show login form
            return View("StaffLogin");
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            var accounts = _configuration.GetSection("UserAccounts").Get<List<UserAccount>>();

            var hashedPassword = PasswordHelper.ComputeSha256Hash(password.Trim());

            var user = accounts.FirstOrDefault(u =>
                u.Username.Equals(username, System.StringComparison.OrdinalIgnoreCase)
                && u.PasswordHash == hashedPassword
                && (u.Role == "Admin" || u.Role == "Staff")); // allow both roles

            if (user != null)
            {
                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("Role", user.Role);

                return RedirectToAction("Index");
            }

            TempData["LoginError"] = "Invalid username or password";
            return RedirectToAction("Index");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file != null)
            {
                using var stream = file.OpenReadStream();
                await _blobService.UploadFileAsync("staff-content", file.FileName, stream, file.ContentType);
            }
            return RedirectToAction("StaffHome");
        }

        [HttpGet]
        public async Task<IActionResult> ListFiles()
        {
            var files = await _blobService.ListFilesAsync("staff-content");
            return View(files); // Make a view to show them
        }


        [HttpPost]
        public IActionResult SaveBalanceability([FromBody] BalanceabilityUpdateModel model)
        {
            if (model == null)
                return Json(new { success = false, message = "Invalid data." });

            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");

                // Load existing JSON
                var json = System.IO.File.ReadAllText(path);
                var contentDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new Dictionary<string, JsonElement>();

                // Prepare updated section
                var updatedSection = new BalanceabilitySection
                {
                    TextContent = new[]
                    {
                new TextContentItem { Heading = model.Heading, Paragraph = model.Paragraph0 },
                new TextContentItem { Paragraph = model.Paragraph1 },
                new TextContentItem { Paragraph = model.Paragraph2 },
                new TextContentItem { Paragraph = model.Paragraph3 }
            },
                    ImageContent = new[]
                    {
                new ImageContentItem { Src = model.ImageSrc, Alt = model.ImageAlt }
            }
                };

                // Check if Balanceability key exists
                if (contentDict.ContainsKey("Balanceability"))
                {
                    // Deserialize existing sections
                    var balanceability = contentDict["Balanceability"].Deserialize<BalanceabilityWrapper>() ?? new BalanceabilityWrapper();

                    if (balanceability.Sections == null || balanceability.Sections.Length == 0)
                    {
                        // Create first section
                        balanceability.Sections = new[] { updatedSection };
                    }
                    else
                    {
                        // Update first section
                        balanceability.Sections[0] = updatedSection;
                    }

                    // Replace Balanceability
                    contentDict["Balanceability"] = JsonSerializer.SerializeToElement(balanceability);
                }
                else
                {
                    // Create new Balanceability with one section
                    var balanceability = new BalanceabilityWrapper
                    {
                        Sections = new[] { updatedSection }
                    };
                    contentDict["Balanceability"] = JsonSerializer.SerializeToElement(balanceability);
                }

                // Write back to file
                var newJson = JsonSerializer.Serialize(contentDict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                System.IO.File.WriteAllText(path, newJson);

                _contentService.Reload(); // implement this method

                return Json(new { success = true, message = "Balanceability content saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving content: " + ex.Message });
            }
        }

        // Wrapper to match JSON structure with Sections array
        public class BalanceabilityWrapper
        {
            public BalanceabilitySection[] Sections { get; set; }
        }


        public class BalanceabilityUpdateModel
        {
            public string Heading { get; set; }
            public string Paragraph0 { get; set; }
            public string Paragraph1 { get; set; }
            public string Paragraph2 { get; set; }
            public string Paragraph3 { get; set; }
            public string ImageSrc { get; set; }
            public string ImageAlt { get; set; }
        }


        public class TextContentItem
        {
            public string? Heading { get; set; }
            public string Paragraph { get; set; }
        }

        public class ImageContentItem
        {
            public string Src { get; set; }
            public string Alt { get; set; }
        }

        public class BalanceabilitySection
        {
            public TextContentItem[] TextContent { get; set; }
            public ImageContentItem[] ImageContent { get; set; }
        }
    }


}
