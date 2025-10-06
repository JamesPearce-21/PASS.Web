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
                new TextContentItem { Heading = model.Heading, Paragraph = "" },
                new TextContentItem { Paragraph = model.Paragraph1 },
                new TextContentItem { Paragraph = model.Paragraph2 },
                new TextContentItem { Paragraph = model.Paragraph3 },
                new TextContentItem { Paragraph = model.Paragraph4 }
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

        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            // Upload file and get SAS URL
            var sasUri = await _blobService.UploadFileWithSasUrlAsync("cms-content", file.FileName, file.OpenReadStream(), file.ContentType, 20);

            // Update your JSON automatically (example for first image in Balanceability)
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
            var jsonText = System.IO.File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);

            var balanceability = dict["Balanceability"].Deserialize<BalanceabilityWrapper>() ?? new BalanceabilityWrapper();
            if (balanceability.Sections == null || balanceability.Sections.Length == 0)
                balanceability.Sections = new[] { new BalanceabilitySection() };

            balanceability.Sections[0].ImageContent = new[]
            {
        new ImageContentItem
        {
            Src = sasUri.ToString(),
            Alt = file.FileName
        }
    };

            dict["Balanceability"] = JsonSerializer.SerializeToElement(balanceability);
            System.IO.File.WriteAllText(path, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));

            _contentService.Reload(); // ensure front-end sees the change immediately

            return Json(new { success = true, message = "File uploaded and JSON updated!", url = sasUri.ToString() });
        }


        [HttpPost]
        public IActionResult SaveBikeability([FromBody] BikeabilityUpdateModel model)
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
                var updatedSection = new BikeabilitySection
                {
                    TextContent = new[]
                    {
                new TextContentItem { Heading = model.Heading, Paragraph = "" },
                new TextContentItem { Paragraph = model.Paragraph1 },
                new TextContentItem { Paragraph = model.Paragraph2 },
                new TextContentItem { Paragraph = model.Paragraph3 },
                new TextContentItem { Paragraph = model.ListItem1 },
                new TextContentItem { Paragraph = model.ListItem2 },
                new TextContentItem { Paragraph = model.ListItem3 },
                new TextContentItem { Paragraph = model.Paragraph4 }
            },
                    ImageContent = new[]
                    {
                new ImageContentItem { Src = model.ImageSrc, Alt = model.ImageAlt }
            }
                };

                // Check if Bikeability key exists
                if (contentDict.ContainsKey("Bikeability"))
                {
                    // Deserialize existing sections
                    var bikeability = contentDict["Bikeability"].Deserialize<BikeabilityWrapper>() ?? new BikeabilityWrapper();

                    if (bikeability.Sections == null || bikeability.Sections.Length == 0)
                    {
                        // Create first section
                        bikeability.Sections = new[] { updatedSection };
                    }
                    else
                    {
                        // Update first section
                        bikeability.Sections[0] = updatedSection;
                    }

                    // Replace Bikeability
                    contentDict["Bikeability"] = JsonSerializer.SerializeToElement(bikeability);
                }
                else
                {
                    // Create new Bikeability with one section
                    var bikeability = new BikeabilityWrapper
                    {
                        Sections = new[] { updatedSection }
                    };
                    contentDict["Bikeability"] = JsonSerializer.SerializeToElement(bikeability);
                }

                // Write back to file
                var newJson = JsonSerializer.Serialize(contentDict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                System.IO.File.WriteAllText(path, newJson);

                _contentService.Reload(); // make sure this reloads the JSON in memory

                return Json(new { success = true, message = "Bikeability content saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving content: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadBikeabilityImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            // Upload file to Blob Storage and get SAS URL
            var sasUri = await _blobService.UploadFileWithSasUrlAsync("cms-content", file.FileName, file.OpenReadStream(), file.ContentType, 20);

            // Load existing JSON
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
            var jsonText = System.IO.File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);

            // Get existing Bikeability data or create new
            var bikeability = dict["Bikeability"].Deserialize<BikeabilityWrapper>() ?? new BikeabilityWrapper();
            if (bikeability.Sections == null || bikeability.Sections.Length == 0)
                bikeability.Sections = new[] { new BikeabilitySection() };

            // Update first section's image
            bikeability.Sections[0].ImageContent = new[]
            {
        new ImageContentItem
        {
            Src = sasUri.ToString(),
            Alt = file.FileName
        }
    };

            // Save back to dictionary
            dict["Bikeability"] = JsonSerializer.SerializeToElement(bikeability);

            // Write updated JSON to file
            System.IO.File.WriteAllText(path, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));

            // Reload content service
            _contentService.Reload();

            return Json(new { success = true, message = "File uploaded and JSON updated!", url = sasUri.ToString() });
        }


        [HttpPost]
        public IActionResult SaveContactUs([FromBody] ContactUsUpdateModel model)
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
                var updatedSection = new ContactUsSection
                {
                    TextContent = new[]
                    {
                new TextContentItem { Heading = model.Heading, Paragraph = "" },
                new TextContentItem { Paragraph = model.Paragraph1 },
                new TextContentItem { Paragraph = model.Paragraph2 },
                new TextContentItem { Paragraph = model.Paragraph3 },
                new TextContentItem { Paragraph = model.Paragraph4 }
            },
                    ImageContent = new[]
                    {
                new ImageContentItem { Src = model.ImageSrc, Alt = model.ImageAlt }
            }
                };

                // Check if ContactUs key exists
                if (contentDict.ContainsKey("ContactUs"))
                {
                    // Deserialize existing sections
                    var contactUs = contentDict["ContactUs"].Deserialize<ContactUsWrapper>() ?? new ContactUsWrapper();

                    if (contactUs.Sections == null || contactUs.Sections.Length == 0)
                    {
                        // Create first section
                        contactUs.Sections = new[] { updatedSection };
                    }
                    else
                    {
                        // Update first section
                        contactUs.Sections[0] = updatedSection;
                    }

                    // Replace ContactUs
                    contentDict["ContactUs"] = JsonSerializer.SerializeToElement(contactUs);
                }
                else
                {
                    // Create new ContactUs with one section
                    var contactUs = new ContactUsWrapper
                    {
                        Sections = new[] { updatedSection }
                    };
                    contentDict["ContactUs"] = JsonSerializer.SerializeToElement(contactUs);
                }

                // Write back to file
                var newJson = JsonSerializer.Serialize(contentDict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                System.IO.File.WriteAllText(path, newJson);

                _contentService.Reload(); // make sure this reloads the JSON in memory

                return Json(new { success = true, message = "ContactUs content saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving content: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadContactUsImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            // Upload file to Blob Storage and get SAS URL
            var sasUri = await _blobService.UploadFileWithSasUrlAsync("cms-content", file.FileName, file.OpenReadStream(), file.ContentType, 20);

            // Load existing JSON
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
            var jsonText = System.IO.File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);

            // Get existing ContactUs data or create new
            var contactUs = dict.ContainsKey("ContactUs")
                ? dict["ContactUs"].Deserialize<ContactUsWrapper>() ?? new ContactUsWrapper()
                : new ContactUsWrapper();

            if (contactUs.Sections == null || contactUs.Sections.Length == 0)
                contactUs.Sections = new[] { new ContactUsSection() };

            // Update first section's image
            contactUs.Sections[0].ImageContent = new[]
            {
        new ImageContentItem
        {
            Src = sasUri.ToString(),
            Alt = file.FileName
        }
    };

            // Save back to dictionary
            dict["ContactUs"] = JsonSerializer.SerializeToElement(contactUs);

            // Write updated JSON to file
            System.IO.File.WriteAllText(path, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));

            // Reload content service
            _contentService.Reload();

            return Json(new { success = true, message = "File uploaded and JSON updated!", url = sasUri.ToString() });
        }

        [HttpPost]
        public IActionResult SavePrivacyPolicy([FromBody] PrivacyPolicyUpdateModel model)
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
                var updatedSection = new PrivacyPolicySection
                {
                    TextContent = new[]
                    {
                new TextContentItem { Heading = model.Heading, Paragraph = "" },
                new TextContentItem { Paragraph = model.Paragraph1 }
            },
                    ImageContent = new[]
                    {
                new ImageContentItem { Src = model.ImageSrc, Alt = model.ImageAlt }
            }
                };

                // Check if PrivacyPolicy key exists
                if (contentDict.ContainsKey("PrivacyPolicy"))
                {
                    // Deserialize existing sections
                    var privacyPolicy = contentDict["PrivacyPolicy"].Deserialize<PrivacyPolicyWrapper>() ?? new PrivacyPolicyWrapper();

                    if (privacyPolicy.Sections == null || privacyPolicy.Sections.Length == 0)
                    {
                        // Create first section
                        privacyPolicy.Sections = new[] { updatedSection };
                    }
                    else
                    {
                        // Update first section
                        privacyPolicy.Sections[0] = updatedSection;
                    }

                    // Replace PrivacyPolicy
                    contentDict["PrivacyPolicy"] = JsonSerializer.SerializeToElement(privacyPolicy);
                }
                else
                {
                    // Create new PrivacyPolicy with one section
                    var privacyPolicy = new PrivacyPolicyWrapper
                    {
                        Sections = new[] { updatedSection }
                    };
                    contentDict["PrivacyPolicy"] = JsonSerializer.SerializeToElement(privacyPolicy);
                }

                // Write back to file
                var newJson = JsonSerializer.Serialize(contentDict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                System.IO.File.WriteAllText(path, newJson);

                _contentService.Reload(); // make sure this reloads the JSON in memory

                return Json(new { success = true, message = "PrivacyPolicy content saved successfully!" });
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

        public class BalanceabilitySection
        {
            public TextContentItem[] TextContent { get; set; }
            public ImageContentItem[] ImageContent { get; set; }
        }


        public class BalanceabilityUpdateModel
        {
            public string Heading { get; set; }
            public string Paragraph0 { get; set; }
            public string Paragraph1 { get; set; }
            public string Paragraph2 { get; set; }
            public string Paragraph3 { get; set; }
            public string Paragraph4 { get; set; }
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

        // Wrapper for all Bikeability sections
        public class BikeabilityWrapper
        {
            public BikeabilitySection[] Sections { get; set; }
        }

        // Model used when posting updates from the admin panel
        public class BikeabilityUpdateModel
        {
            public string Heading { get; set; }
            public string Paragraph1 { get; set; }
            public string Paragraph2 { get; set; }
            public string Paragraph3 { get; set; }
            public string ListItem1 { get; set; }
            public string ListItem2 { get; set; }
            public string ListItem3 { get; set; }
            public string Paragraph4 { get; set; }
            public string ImageSrc { get; set; }
            public string ImageAlt { get; set; }
        }

        // Single section in Bikeability JSON
        public class BikeabilitySection
        {
            public TextContentItem[] TextContent { get; set; }
            public ImageContentItem[] ImageContent { get; set; }
        }


        public class ContactUsWrapper
        {
            public ContactUsSection[] Sections { get; set; }
        }

        public class ContactUsSection
        {
            public TextContentItem[] TextContent { get; set; }
            public ImageContentItem[] ImageContent { get; set; }
        }

        public class ContactUsUpdateModel
        {
            public string Heading { get; set; }
            public string Paragraph1 { get; set; }
            public string Paragraph2 { get; set; }
            public string Paragraph3 { get; set; }
            public string Paragraph4 { get; set; }
            public string ImageSrc { get; set; }
            public string ImageAlt { get; set; }
        }

        // Privacy Policy

        public class PrivacyPolicyWrapper
        {
            public PrivacyPolicySection[] Sections { get; set; }
        }

        public class PrivacyPolicySection
        {
            public TextContentItem[] TextContent { get; set; }
            public ImageContentItem[] ImageContent { get; set; }
        }

        public class PrivacyPolicyUpdateModel
        {
            public string Heading { get; set; }
            public string Paragraph1 { get; set; }
            public string ImageSrc { get; set; }
            public string ImageAlt { get; set; }
        }
    }
}
