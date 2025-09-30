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

            if (!string.IsNullOrEmpty(username) && (role == "Admin" || role == "Staff"))
            {
                ViewBag.IsAdmin = role == "Admin";
                ViewBag.Username = username;
                return View("StaffHome");
            }

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
                && (u.Role == "Admin" || u.Role == "Staff"));

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
            return View(files);
        }

        [HttpPost]
        public IActionResult SaveSection(string sectionKey, [FromBody] SectionWrapper wrapper)
        {
            if (wrapper == null) return Json(new { success = false, message = "No data to save." });

            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
            var jsonText = System.IO.File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, SectionWrapper>>(jsonText) ?? new();

            var oldWrapper = dict.ContainsKey(sectionKey)
                ? dict[sectionKey]
                : new SectionWrapper();

            // Preserve existing Src if no new file uploaded
            for (int i = 0; i < wrapper.Sections.Length; i++)
            {
                for (int j = 0; j < wrapper.Sections[i].ImageContent.Length; j++)
                {
                    if (string.IsNullOrEmpty(wrapper.Sections[i].ImageContent[j].Src))
                    {
                        wrapper.Sections[i].ImageContent[j].Src = oldWrapper.Sections.ElementAtOrDefault(i)?
                            .ImageContent.ElementAtOrDefault(j)?.Src;
                    }
                }
            }

            // Now safe to overwrite
            for (int i = 0; i < wrapper.Sections.Length; i++)
            {
                for (int j = 0; j < wrapper.Sections[i].ImageContent.Length; j++)
                {
                    if (string.IsNullOrEmpty(wrapper.Sections[i].ImageContent[j].Src))
                    {
                        wrapper.Sections[i].ImageContent[j].Src = oldWrapper.Sections.ElementAtOrDefault(i)?
                            .ImageContent.ElementAtOrDefault(j)?.Src;
                    }
                }
            }
            dict[sectionKey] = wrapper;

            System.IO.File.WriteAllText(path, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));

            _contentService.Reload();
            return Json(new { success = true, message = $"{sectionKey} saved successfully!" });
        }



        [HttpPost]
        public async Task<IActionResult> UploadSectionImage(string sectionKey, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            var sasUri = await _blobService.UploadFileWithSasUrlAsync("cms-content", file.FileName, file.OpenReadStream(), file.ContentType, 20);

            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
            var jsonText = System.IO.File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText) ?? new();

            var wrapper = dict.ContainsKey(sectionKey)
                ? JsonSerializer.Deserialize<SectionWrapper>(dict[sectionKey].GetRawText())
                : new SectionWrapper();

            if (wrapper.Sections.Length == 0) wrapper.Sections = new Section[] { new Section() };
            wrapper.Sections[0].ImageContent = new ImageContentItem[] { new ImageContentItem { Src = sasUri.ToString(), Alt = file.FileName } };

            System.IO.File.WriteAllText(path, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
            _contentService.Reload();

            return Json(new { success = true, url = sasUri.ToString(), message = "Image uploaded!" });
        }

        // Models
        public class GenericWrapper
        {
            public List<GenericSection> Sections { get; set; } = new();
        }

        public class GenericSection
        {
            public List<TextContentItem> TextContent { get; set; } = new();
            public List<ImageContentItem> ImageContent { get; set; } = new();
        }
    }
}
