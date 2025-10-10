using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using PASS.Web.Models;
using PASS.Web.Helpers;
using PASS.Web.Services;
using System.Text.Json;
using static PASS.Web.Controllers.StaffAreaController;

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

            var hashedPassword = PasswordHelper.HashPassword(password.Trim());

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
                new TextContentItem { Paragraph = model.Paragraph4 },
                new TextContentItem { Paragraph = model.Paragraph5 }
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

        // Training:

        [HttpPost]
        public IActionResult SaveTraining([FromBody] TrainingUpdateModel model)
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
                var updatedSection = new TrainingSection
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
                new ImageContentItem { Src = model.ImageSrc, Alt = model.ImageAlt },
                new ImageContentItem { Src = model.ImageSrc1, Alt = model.ImageAlt1 }
            },
                    DownloadableContent = new[]
                    {
                new DownloadableContentItem { Src = model.DownloadableSrc, Alt = model.DownloadableAlt }
            }
                };

                // Check if Training key exists
                if (contentDict.ContainsKey("Training"))
                {
                    // Deserialize existing sections
                    var training = contentDict["Training"].Deserialize<TrainingWrapper>() ?? new TrainingWrapper();

                    if (training.Sections == null || training.Sections.Length == 0)
                    {
                        // Create first section
                        training.Sections = new[] { updatedSection };
                    }
                    else
                    {
                        // Update first section
                        training.Sections[0] = updatedSection;
                    }

                    // Replace Training
                    contentDict["Training"] = JsonSerializer.SerializeToElement(training);
                }
                else
                {
                    // Create new Training with one section
                    var training = new TrainingWrapper
                    {
                        Sections = new[] { updatedSection }
                    };
                    contentDict["Training"] = JsonSerializer.SerializeToElement(training);
                }

                // Write back to file
                var newJson = JsonSerializer.Serialize(contentDict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                System.IO.File.WriteAllText(path, newJson);

                _contentService.Reload(); // make sure this reloads the JSON in memory

                return Json(new { success = true, message = "Training content saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving content: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadTrainingImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            // Upload file to Blob Storage and get SAS URL
            var sasUri = await _blobService.UploadFileWithSasUrlAsync(
                "cms-content",
                file.FileName,
                file.OpenReadStream(),
                file.ContentType,
                20
            );

            // Load existing JSON
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
            var jsonText = System.IO.File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);

            // Get existing Training data or create new
            var training = dict.ContainsKey("Training")
                ? dict["Training"].Deserialize<TrainingWrapper>() ?? new TrainingWrapper()
                : new TrainingWrapper();

            if (training.Sections == null || training.Sections.Length == 0)
                training.Sections = new[] { new TrainingSection() };

            // Update first section's image
            training.Sections[0].ImageContent = new[]
            {
        new ImageContentItem
        {
            Src = sasUri.ToString(),
            Alt = file.FileName
        }
    };

            // Save back to dictionary
            dict["Training"] = JsonSerializer.SerializeToElement(training);

            // Write updated JSON to file
            System.IO.File.WriteAllText(
                path,
                JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true })
            );

            // Reload content service
            _contentService.Reload();

            return Json(new { success = true, message = "File uploaded and JSON updated!", url = sasUri.ToString() });
        }

        [HttpPost]
        public async Task<IActionResult> UploadTrainingImage1(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            // Upload file to Blob Storage and get SAS URL
            var sasUri = await _blobService.UploadFileWithSasUrlAsync(
                "cms-content",
                file.FileName,
                file.OpenReadStream(),
                file.ContentType,
                20
            );

            // Load existing JSON
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
            var jsonText = System.IO.File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);

            // Get existing Training data or create new
            var training = dict.ContainsKey("Training")
                ? dict["Training"].Deserialize<TrainingWrapper>() ?? new TrainingWrapper()
                : new TrainingWrapper();

            if (training.Sections == null || training.Sections.Length == 0)
                training.Sections = new[] { new TrainingSection() };

            // Update first section's image
            training.Sections[0].ImageContent[1] = new ImageContentItem
            {
                Src = sasUri.ToString(),
                Alt = file.FileName
            };

            // Save back to dictionary
            dict["Training"] = JsonSerializer.SerializeToElement(training);

            // Write updated JSON to file
            System.IO.File.WriteAllText(
                path,
                JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true })
            );

            // Reload content service
            _contentService.Reload();

            return Json(new { success = true, message = "File uploaded and JSON updated!", url = sasUri.ToString() });
        }

        [HttpPost]
        public async Task<IActionResult> UploadTrainingDocument(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            // Upload file to Blob Storage and get SAS URL
            var sasUri = await _blobService.UploadFileWithSasUrlAsync(
                "cms-content",
                file.FileName,
                file.OpenReadStream(),
                file.ContentType,
                20
            );

            // Load existing JSON
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
            var jsonText = System.IO.File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);

            // Get existing Training data or create new
            var training = dict.ContainsKey("Training")
                ? dict["Training"].Deserialize<TrainingWrapper>() ?? new TrainingWrapper()
                : new TrainingWrapper();

            if (training.Sections == null || training.Sections.Length == 0)
                training.Sections = new[] { new TrainingSection() };

            // Update first section's image
            training.Sections[0].DownloadableContent = new[]
            {
        new DownloadableContentItem
        {
            Src = sasUri.ToString(),
            Alt = file.FileName
        }
    };


            // Save back to dictionary
            dict["Training"] = JsonSerializer.SerializeToElement(training);

            // Write updated JSON to file
            System.IO.File.WriteAllText(
                path,
                JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true })
            );

            // Reload content service
            _contentService.Reload();

            return Json(new { success = true, message = "File uploaded and JSON updated!", url = sasUri.ToString() });
        }


        // The Team:

        [HttpPost]
        public IActionResult SaveTheTeam([FromBody] TheTeamUpdateModel model)
        {
            if (model == null || model.Members == null || !model.Members.Any())
                return Json(new { success = false, message = "Invalid data." });

            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");

                // Load existing JSON
                var json = System.IO.File.ReadAllText(path);
                var contentDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                                  ?? new Dictionary<string, JsonElement>();

                // Get existing TheTeam wrapper or create new
                var theTeam = contentDict.ContainsKey("TheTeam")
                    ? contentDict["TheTeam"].Deserialize<TheTeamWrapper>() ?? new TheTeamWrapper()
                    : new TheTeamWrapper();

                if (theTeam.Sections == null || theTeam.Sections.Length == 0)
                {
                    theTeam.Sections = new[]
                    {
        new TheTeamSection
        {
            TextContent = Array.Empty<TextContentItem>(),  // initialize as empty array
            ImageContent = Array.Empty<ImageContentItem>() // initialize as empty array
        }
    };
                }

                var section = theTeam.Sections[0];

                // Preserve the first "intro" item if it exists
                TextContentItem intro = section.TextContent?.FirstOrDefault();
                var newMembers = model.Members
    .Select(m => new TextContentItem
    {
        Heading = m.Heading,
        Paragraph = m.Paragraph?.Replace(" - ", " — ") // <-- replace dash with em dash
    })
    .ToList();

                if (intro != null)
                {
                    section.TextContent = new List<TextContentItem> { intro }
                        .Concat(newMembers)
                        .ToArray();  // convert to array
                }
                else
                {
                    section.TextContent = newMembers.ToArray();
                }

                // Save back
                contentDict["TheTeam"] = JsonSerializer.SerializeToElement(theTeam);

                var newJson = JsonSerializer.Serialize(contentDict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                System.IO.File.WriteAllText(path, newJson);

                _contentService.Reload();

                return Json(new { success = true, message = "Team members saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving content: " + ex.Message });
            }
        }

        //Memberships:

        [HttpPost]
        public IActionResult SaveMemberships([FromBody] MembershipsUpdateModel model)
        {
            if (model == null)
                return Json(new { success = false, message = "Invalid data." });

            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
                var jsonText = System.IO.File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText)
                           ?? new Dictionary<string, JsonElement>();

                // Get existing or create new
                var memberships = dict.ContainsKey("Memberships")
                    ? dict["Memberships"].Deserialize<MembershipsWrapper>() ?? new MembershipsWrapper()
                    : new MembershipsWrapper();

                if (memberships.Sections == null || memberships.Sections.Length == 0)
                    memberships.Sections = new[] { new MembershipsSection() };

                var section = memberships.Sections[0];

                // Initialize arrays if null
                section.TextContent ??= new TextContentItem[12];
                section.ImageContent ??= new ImageContentItem[5];
                section.DownloadableContent ??= new DownloadableContentItem[1];

                // TextContent by index
                section.TextContent[0] = new TextContentItem { Heading = model.Heading, Paragraph = "" };
                section.TextContent[1] = new TextContentItem { Paragraph = model.Paragraph1 };
                section.TextContent[2] = new TextContentItem { Paragraph = model.Paragraph2 };
                section.TextContent[3] = new TextContentItem { Paragraph = model.Paragraph3 };
                section.TextContent[4] = new TextContentItem { Paragraph = model.Paragraph4 };
                section.TextContent[5] = new TextContentItem { Paragraph = model.Paragraph5 };
                section.TextContent[6] = new TextContentItem { Heading = model.BronzeHeading, Paragraph = model.BronzeParagraph };
                section.TextContent[7] = new TextContentItem { Heading = model.SilverHeading, Paragraph = model.SilverParagraph };
                section.TextContent[8] = new TextContentItem { Heading = model.GoldHeading, Paragraph = model.GoldParagraph };
                section.TextContent[9] = new TextContentItem { Heading = model.CPDHeading, Paragraph = model.CPDParagraph };
                section.TextContent[10] = new TextContentItem { Heading = model.CompetitionsHeading, Paragraph = model.CompetitionsParagraph };
                section.TextContent[11] = new TextContentItem { Heading = model.GetInTouchHeading, Paragraph = model.GetInTouchParagraph };

                // ImageContent
                section.ImageContent[0] = new ImageContentItem { Src = model.BronzeImageSrc, Alt = model.BronzeImageAlt };
                section.ImageContent[1] = new ImageContentItem { Src = model.SilverImageSrc, Alt = model.SilverImageAlt };
                section.ImageContent[2] = new ImageContentItem { Src = model.GoldImageSrc, Alt = model.GoldImageAlt };
                section.ImageContent[3] = new ImageContentItem { Src = model.CPDImageSrc, Alt = model.CPDImageAlt };
                section.ImageContent[4] = new ImageContentItem { Src = model.CompetitionsImageSrc, Alt = model.CompetitionsImageAlt };

                // DownloadableContent
                section.DownloadableContent[0] = new DownloadableContentItem
                {
                    Src = model.PackagesDocumentSrc,
                    Alt = model.PackagesDocumentAlt
                };

                dict["Memberships"] = JsonSerializer.SerializeToElement(memberships);

                var newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                System.IO.File.WriteAllText(path, newJson);
                _contentService.Reload();

                return Json(new { success = true, message = "Memberships content saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving content: " + ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> UploadMembershipImage(IFormFile file, int packageIndex)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            var sasUri = await _blobService.UploadFileWithSasUrlAsync(
                "cms-content",
                file.FileName,
                file.OpenReadStream(),
                file.ContentType,
                20
            );

            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
            var jsonText = System.IO.File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);

            var memberships = dict.ContainsKey("Memberships")
                ? dict["Memberships"].Deserialize<MembershipsWrapper>() ?? new MembershipsWrapper()
                : new MembershipsWrapper();

            if (memberships.Sections == null || memberships.Sections.Length == 0)
                memberships.Sections = new[] { new MembershipsSection() };

            // Update the specific package image
            if (memberships.Sections[0].ImageContent.Length <= packageIndex)
            {
                var images = memberships.Sections[0].ImageContent.ToList();
                while (images.Count <= packageIndex)
                    images.Add(new ImageContentItem());
                memberships.Sections[0].ImageContent = images.ToArray();
            }

            memberships.Sections[0].ImageContent[packageIndex] = new ImageContentItem
            {
                Src = sasUri.ToString(),
                Alt = file.FileName
            };

            dict["Memberships"] = JsonSerializer.SerializeToElement(memberships);

            System.IO.File.WriteAllText(
                path,
                JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true })
            );

            _contentService.Reload();

            return Json(new { success = true, message = "File uploaded and JSON updated!", url = sasUri.ToString() });
        }

        [HttpPost]
        public async Task<IActionResult> UploadMembershipsDocument(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            var sasUri = await _blobService.UploadFileWithSasUrlAsync(
                "cms-content",
                file.FileName,
                file.OpenReadStream(),
                file.ContentType,
                20
            );

            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
            var jsonText = System.IO.File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);

            var memberships = dict.ContainsKey("Memberships")
                ? dict["Memberships"].Deserialize<MembershipsWrapper>() ?? new MembershipsWrapper()
                : new MembershipsWrapper();

            if (memberships.Sections == null || memberships.Sections.Length == 0)
                memberships.Sections = new[] { new MembershipsSection() };

            memberships.Sections[0].DownloadableContent = new[]
            {
        new DownloadableContentItem
        {
            Src = sasUri.ToString(),
            Alt = file.FileName
        }
    };

            dict["Memberships"] = JsonSerializer.SerializeToElement(memberships);

            System.IO.File.WriteAllText(
                path,
                JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true })
            );

            _contentService.Reload();

            return Json(new { success = true, message = "File uploaded and JSON updated!", url = sasUri.ToString() });
        }

        //Staff Information:

        [HttpPost]
        public async Task<IActionResult> UploadStaffDocument(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            try
            {
                // Upload file to blob storage
                var sasUri = await _blobService.UploadFileWithSasUrlAsync(
                    "cms-content",
                    file.FileName,
                    file.OpenReadStream(),
                    file.ContentType,
                    20
                );

                // Load JSON
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
                var json = System.IO.File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                           ?? new Dictionary<string, JsonElement>();

                // Get StaffDocs section
                var staffWrapper = dict.ContainsKey("StaffDocs")
                    ? dict["StaffDocs"].Deserialize<StaffWrapper>() ?? new StaffWrapper()
                    : new StaffWrapper();

                if (staffWrapper.Sections == null || staffWrapper.Sections.Length == 0)
                    staffWrapper.Sections = new[] { new StaffSection() };

                // Add new document
                staffWrapper.Sections[0].Documents.Add(new StaffDocumentItem
                {
                    Src = sasUri.ToString(),
                    Alt = file.FileName
                });

                // Save back
                dict["StaffDocs"] = JsonSerializer.SerializeToElement(staffWrapper);
                System.IO.File.WriteAllText(path, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));

                _contentService.Reload();

                return Json(new { success = true, message = "Document uploaded successfully!", url = sasUri.ToString() });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error uploading document: {ex.Message}" });
            }
        }

        [HttpPost]
        public IActionResult SaveStaffDocuments([FromBody] StaffDocumentsUpdateModel model)
        {
            if (model == null)
                return Json(new { success = false, message = "Invalid data." });

            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");

                // Load existing JSON
                var jsonText = System.IO.File.ReadAllText(path);
                var contentDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText)
                                  ?? new Dictionary<string, JsonElement>();

                // Build new section from posted model
                var updatedSection = new StaffDocsSection
                {
                    Documents = model.Documents?.Select(d => new DownloadableContentItem
                    {
                        Src = d.Src,
                        Alt = d.Alt
                    }).ToArray() ?? Array.Empty<DownloadableContentItem>()
                };

                // Wrap it
                var wrapper = new StaffDocsWrapper
                {
                    Sections = new[] { updatedSection }
                };

                // Replace or add the StaffDocs key
                contentDict["StaffDocs"] = JsonSerializer.SerializeToElement(wrapper);

                // Write back to disk
                var newJson = JsonSerializer.Serialize(contentDict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                System.IO.File.WriteAllText(path, newJson);

                // Refresh in-memory copy
                _contentService.Reload();

                return Json(new { success = true, message = "Staff documents saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving documents: " + ex.Message });
            }
        }




        [HttpPost]
        public IActionResult SaveStaffInfo([FromBody] StaffInfoUpdateModel model)
        {
            if (model == null)
                return Json(new { success = false, message = "Invalid data." });

            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");

                // Load existing JSON
                var json = System.IO.File.ReadAllText(path);
                var contentDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new Dictionary<string, JsonElement>();

                // Prepare updated section
                var updatedSection = new StaffInfoSection
                {
                    TextContent = new[]
                    {
                new TextContentItem { Heading = "Staff Information", Paragraph = model.Paragraph }
            }
                };

                // Check if StaffInfo key exists
                if (contentDict.ContainsKey("StaffInfo"))
                {
                    var staffInfo = contentDict["StaffInfo"].Deserialize<StaffInfoWrapper>() ?? new StaffInfoWrapper();

                    if (staffInfo.Sections == null || staffInfo.Sections.Length == 0)
                    {
                        staffInfo.Sections = new[] { updatedSection };
                    }
                    else
                    {
                        staffInfo.Sections[0] = updatedSection;
                    }

                    contentDict["StaffInfo"] = JsonSerializer.SerializeToElement(staffInfo);
                }
                else
                {
                    var staffInfo = new StaffInfoWrapper { Sections = new[] { updatedSection } };
                    contentDict["StaffInfo"] = JsonSerializer.SerializeToElement(staffInfo);
                }

                // Write back to file
                var newJson = JsonSerializer.Serialize(contentDict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                System.IO.File.WriteAllText(path, newJson);

                return Json(new { success = true, message = "Staff information saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving content: " + ex.Message });
            }
        }


        // Events:

        [HttpPost]
        public IActionResult SaveEvents([FromBody] EventsUpdateModel model)
        {
            if (model == null || model.Events == null)
                return Json(new { success = false, message = "Invalid data." });

            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
                if (!System.IO.File.Exists(path))
                    return Json(new { success = false, message = "Content file not found." });

                var jsonText = System.IO.File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText)
                            ?? new Dictionary<string, JsonElement>();

                // Ensure Events section exists
                if (!dict.TryGetValue("Events", out var eventsElement))
                {
                    dict["Events"] = JsonSerializer.SerializeToElement(new
                    {
                        Sections = new object[] { new { TextContent = new object[] { } } }
                    });
                }

                var existingEvents = dict["Events"].Deserialize<EventsWrapper>()
                                     ?? new EventsWrapper
                                     {
                                         Sections = new List<EventSection>
                                         {
                                     new EventSection { TextContent = new List<EventItem>() }
                                         }
                                     };

                var introItems = existingEvents.Sections[0].TextContent
                                  .Take(4)
                                  .ToList();

                var now = DateTime.UtcNow; // use UTC to avoid timezone inconsistencies

                var newEventItems = model.Events.Select(e =>
                {
                    DateTime startDate, endDate;
                    DateTime.TryParse(e.StartDate, out startDate);
                    DateTime.TryParse(e.EndDate, out endDate);

                    string status;

                    if (startDate == default || endDate == default)
                    {
                        status = "Coming Soon"; // fallback if dates are invalid
                    }
                    else if (now < startDate)
                    {
                        status = "Coming Soon";
                    }
                    else if (now > endDate)
                    {
                        status = "Closed";
                    }
                    else
                    {
                        status = "Open";
                    }

                    return new EventItem
                    {
                        Heading = e.Heading,
                        Paragraph = e.Paragraph,
                        Status = status, // auto-determined
                        BookingUrl = e.BookingUrl,
                        StartDate = e.StartDate,
                        EndDate = e.EndDate
                    };
                }).ToList();

                existingEvents.Sections[0].TextContent = introItems.Concat(newEventItems).ToList();

                dict["Events"] = JsonSerializer.SerializeToElement(existingEvents);

                var newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(path, newJson);

                _contentService.Reload();

                return Json(new { success = true, message = "Events saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving events: " + ex.Message });
            }
        }



        // Members Area:

        [HttpPost]
        public async Task<IActionResult> UploadMembersDocument(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            try
            {
                // Upload file to Blob Storage and get SAS URL
                var sasUri = await _blobService.UploadFileWithSasUrlAsync(
                    "cms-content",
                    file.FileName,
                    file.OpenReadStream(),
                    file.ContentType,
                    20
                );

                return Json(new { success = true, message = "File uploaded!", url = sasUri.ToString() });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error uploading file: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult SaveMembersArea([FromBody] MembersAreaUpdateModel model)
        {
            if (model == null)
                return Json(new { success = false, message = "Invalid data." });

            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");

                // Load full JSON
                var json = System.IO.File.ReadAllText(path);
                var contentDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (contentDict == null || !contentDict.ContainsKey("MembersArea"))
                    return Json(new { success = false, message = "MembersArea section not found in JSON." });

                // Deserialize MembersArea as a modifiable object
                var membersAreaNode = contentDict["MembersArea"].Deserialize<Dictionary<string, object>>()
                    ?? new Dictionary<string, object>();

                // Get existing Sections array
                if (!membersAreaNode.TryGetValue("Sections", out var sectionsObj))
                    return Json(new { success = false, message = "MembersArea.Sections not found." });

                var sections = JsonSerializer.Deserialize<List<JsonElement>>(sectionsObj.ToString() ?? "[]")
                    ?? new List<JsonElement>();

                // Ensure Sections has at least 3 elements
                while (sections.Count < 3)
                    sections.Add(JsonDocument.Parse("{}").RootElement);

                // Build the new third section (MembersContent)
                var newMembersContent = new
                {
                    Key = "MembersContent",
                    TextContent = new[]
                    {
                new
                {
                    Heading = model.Intro?.Heading ?? "",
                    Paragraph = model.Intro?.Paragraph ?? ""
                }
            },
                    Schemes = model.Schemes?.Select(s => new
                    {
                        YearGroup = s.YearGroup ?? "",
                        Documents = (s.Documents ?? new List<DocumentDto>())
    .Select(d => new
    {
        Title = d.Title ?? "",
        Url = d.Url ?? ""
    })
    .ToList()

                    }).ToList()
                };

                // Replace Sections[2]
                sections[2] = JsonSerializer.SerializeToElement(newMembersContent);

                // Put back into MembersArea
                membersAreaNode["Sections"] = sections;

                // Replace MembersArea in root dict
                contentDict["MembersArea"] = JsonSerializer.SerializeToElement(membersAreaNode);

                // Save back to disk
                var newJson = JsonSerializer.Serialize(contentDict, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                System.IO.File.WriteAllText(path, newJson);

                _contentService.Reload();
                return Json(new { success = true, message = "Members area updated." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving MembersArea: " + ex.Message });
            }
        }


        // Hero:
        [HttpPost]
        public IActionResult SaveHero([FromBody] HeroUpdateModel model)
        {
            if (model == null)
                return Json(new { success = false, message = "Invalid data." });

            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");

                // Load existing JSON
                var jsonText = System.IO.File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText)
                           ?? new Dictionary<string, JsonElement>();

                // Load or create Hero wrapper
                var heroWrapper = dict.ContainsKey("Hero")
                    ? dict["Hero"].Deserialize<HeroWrapper>() ?? new HeroWrapper()
                    : new HeroWrapper();

                // Ensure all sections exist (0-5)
                if (heroWrapper.Sections == null) heroWrapper.Sections = new HeroSection[6];
                for (int i = 0; i < 6; i++)
                {
                    if (heroWrapper.Sections[i] == null) heroWrapper.Sections[i] = new HeroSection();
                }

                // --- Section 0: Text & Image ---
                var section0 = heroWrapper.Sections[0];
                section0.TextContent ??= new TextContentItem[1];
                section0.TextContent[0] = new TextContentItem
                {
                    Heading = model.HeroHeading ?? section0.TextContent[0]?.Heading,
                    Paragraph = model.HeroParagraph ?? section0.TextContent[0]?.Paragraph
                };
                section0.ImageContent ??= new ImageContentItem[1];
                section0.ImageContent[0] = new ImageContentItem
                {
                    Src = model.HeroImageSrc ?? section0.ImageContent[0]?.Src,
                    Alt = model.HeroImageAlt ?? section0.ImageContent[0]?.Alt
                };

                // --- Section 1: Course Grid ---
                var section1 = heroWrapper.Sections[1];
                if (model.CourseGrid != null)
                {
                    section1.CourseGrid = model.CourseGrid
                        .Select(c => new CourseGridItem
                        {
                            Icon = c.Icon,
                            Heading = c.Heading,
                            Paragraph = c.Paragraph,
                            LinkUrl = c.LinkUrl,
                            LinkText = c.LinkText
                        }).ToArray();
                }

                // --- Section 2: Mission ---
                var section2 = heroWrapper.Sections[2];
                section2.Mission ??= new MissionSection();
                section2.Mission.Heading = model.MissionHeading ?? section2.Mission.Heading;
                section2.Mission.SubHeading = model.MissionSubHeading ?? section2.Mission.SubHeading;
                section2.Mission.Paragraph = model.MissionParagraph ?? section2.Mission.Paragraph;

                // --- Section 3: Impact Stats ---
                var section3 = heroWrapper.Sections[3];
                if (model.ImpactStats != null)
                {
                    section3.ImpactStats = model.ImpactStats
                        .Select(s => new ImpactStatItem { Value = s.Value, Label = s.Label })
                        .ToArray();
                }

                // --- Section 4: Testimonials ---
                var section4 = heroWrapper.Sections[4];
                if (model.Testimonials != null)
                {
                    section4.Testimonials = model.Testimonials
                        .Select(t => new TestimonialItem { Quote = t.Quote, Author = t.Author })
                        .ToArray();
                }

                // --- Section 5: Membership Accordion ---
                var section5 = heroWrapper.Sections[5];
                section5.MembershipAccordion ??= new MembershipAccordionSection();
                section5.MembershipAccordion.Heading = model.MembershipHeading ?? section5.MembershipAccordion.Heading;
                section5.MembershipAccordion.Schools = model.MembershipSchools?.ToArray() ?? section5.MembershipAccordion.Schools;
                section5.MembershipAccordion.Credentials = model.MembershipCredentials?.ToArray() ?? section5.MembershipAccordion.Credentials;

                // Save back to JSON
                dict["Hero"] = JsonSerializer.SerializeToElement(heroWrapper);

                var newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                System.IO.File.WriteAllText(path, newJson);
                _contentService.Reload();

                return Json(new { success = true, message = "Hero content saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving content: " + ex.Message });
            }
        }



        [HttpPost]
        public async Task<IActionResult> UploadHeroImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file selected." });

            try
            {
                // Upload file (Blob storage or local, depending on your setup)
                var sasUri = await _blobService.UploadFileWithSasUrlAsync(
                    "cms-content",
                    file.FileName,
                    file.OpenReadStream(),
                    file.ContentType,
                    20
                );

                // Update Hero JSON
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
                var jsonText = System.IO.File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText)
                           ?? new Dictionary<string, JsonElement>();

                var heroWrapper = dict.ContainsKey("Hero")
                    ? dict["Hero"].Deserialize<HeroWrapper>() ?? new HeroWrapper()
                    : new HeroWrapper();

                if (heroWrapper.Sections == null || heroWrapper.Sections.Length == 0)
                    heroWrapper.Sections = new[] { new HeroSection() };

                var section0 = heroWrapper.Sections[0];
                section0.ImageContent ??= new ImageContentItem[1];

                section0.ImageContent[0] = new ImageContentItem
                {
                    Src = sasUri.ToString(),
                    Alt = file.FileName
                };

                dict["Hero"] = JsonSerializer.SerializeToElement(heroWrapper);

                var newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                System.IO.File.WriteAllText(path, newJson);
                _contentService.Reload();

                return Json(new { success = true, message = "Hero image uploaded successfully!", url = sasUri.ToString() });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error uploading image: " + ex.Message });
            }
        }

        // PASSWORDS:
        [HttpPost]
        public IActionResult ChangeUserPassword([FromBody] ChangePasswordModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.NewPassword))
                return Json(new { success = false, message = "Invalid input." });

            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                var jsonText = System.IO.File.ReadAllText(path);
                var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText)
                           ?? new Dictionary<string, JsonElement>();

                if (!json.TryGetValue("UserAccounts", out var usersElement))
                    return Json(new { success = false, message = "UserAccounts section not found." });

                var users = JsonSerializer.Deserialize<List<UserAccount>>(usersElement.GetRawText());

                var user = users.FirstOrDefault(u =>
                    string.Equals(u.Username, model.Username, StringComparison.OrdinalIgnoreCase));

                if (user == null)
                    return Json(new { success = false, message = "User not found." });

                // Hash new password
                user.PasswordHash = PasswordHelper.HashPassword(model.NewPassword);

                // Re-serialize and overwrite appsettings.json
                json["UserAccounts"] = JsonSerializer.SerializeToElement(users);

                var newJson = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(path, newJson);

                return Json(new { success = true, message = "Password updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating password: " + ex.Message });
            }
        }




        // Models:

        //PASSWORDS:
        public class ChangePasswordModel
        {
            public string Username { get; set; }
            public string NewPassword { get; set; }
        }

        public class UserAccount
        {
            public string Username { get; set; }
            public string PasswordHash { get; set; }
            public string Role { get; set; }
        }


        // Root wrapper for the Hero section
        // Root wrapper
        public class HeroWrapper
        {
            public HeroSection[] Sections { get; set; }
        }

        // Each Section
        public class HeroSection
        {
            public TextContentItem[] TextContent { get; set; }
            public ImageContentItem[] ImageContent { get; set; }
            public CourseGridItem[] CourseGrid { get; set; }
            public MissionSection Mission { get; set; }
            public ImpactStatItem[] ImpactStats { get; set; }
            public TestimonialItem[] Testimonials { get; set; }
            public MembershipAccordionSection MembershipAccordion { get; set; }
        }

        // Course Grid
        public class CourseGridItem
        {
            public string Icon { get; set; }
            public string Heading { get; set; }
            public string Paragraph { get; set; }
            public string LinkUrl { get; set; }
            public string LinkText { get; set; }
        }

        // Mission Section
        public class MissionSection
        {
            public string Heading { get; set; }
            public string SubHeading { get; set; }
            public string Paragraph { get; set; }
        }

        // Impact Stats
        public class ImpactStatItem
        {
            public string Value { get; set; }
            public string Label { get; set; }
        }

        // Testimonials
        public class TestimonialItem
        {
            public string Quote { get; set; }
            public string Author { get; set; }
        }

        // Membership Accordion
        public class MembershipAccordionSection
        {
            public string Heading { get; set; }
            public string[] Schools { get; set; }
            public string[] Credentials { get; set; }
        }

        // Update model for SaveHero
        public class HeroUpdateModel
        {
            public string HeroHeading { get; set; }
            public string HeroParagraph { get; set; }
            public string HeroImageSrc { get; set; }
            public string HeroImageAlt { get; set; }

            public List<CourseGridItem> CourseGrid { get; set; }

            public string MissionHeading { get; set; }
            public string MissionSubHeading { get; set; }
            public string MissionParagraph { get; set; }

            public List<ImpactStatItem> ImpactStats { get; set; }
            public List<TestimonialItem> Testimonials { get; set; }

            public string MembershipHeading { get; set; }
            public List<string> MembershipSchools { get; set; }
            public List<string> MembershipCredentials { get; set; }
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

        public class DownloadableContentItem
        {
            public string Src { get; set; }
            public string Alt { get; set; }
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
            public string Paragraph5 { get; set; }
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

        // Training

        public class TrainingWrapper
        {
            public TrainingSection[] Sections { get; set; }
        }

        public class TrainingSection
        {
            public TextContentItem[] TextContent { get; set; }
            public ImageContentItem[] ImageContent { get; set; }
            public DownloadableContentItem[] DownloadableContent { get; set; }
        }

        public class TrainingUpdateModel
        {
            public string Heading { get; set; }
            public string Paragraph1 { get; set; }
            public string Paragraph2 { get; set; }
            public string Paragraph3 { get; set; }
            public string Paragraph4 { get; set; }
            public string ImageSrc { get; set; }
            public string ImageAlt { get; set; }
            public string ImageSrc1 { get; set; }
            public string ImageAlt1 { get; set; }
            public string DownloadableSrc { get; set; }
            public string DownloadableAlt { get; set; }
        }

        //The Team:

        public class TheTeamWrapper
        {
            public TheTeamSection[] Sections { get; set; }
        }

        public class TheTeamSection
        {
            public TextContentItem[] TextContent { get; set; }
            public ImageContentItem[] ImageContent { get; set; }
        }
        public class TheTeamUpdateModel
        {
            public List<TeamMember> Members { get; set; }
            public List<ImageContentItem>? Images { get; set; }
        }

        public class TeamMember
        {
            public string Heading { get; set; }   // Team member name
            public string Paragraph { get; set; } // Job title + description
        }


        // Memberships:

        public class MembershipsWrapper
        {
            public MembershipsSection[] Sections { get; set; }
        }

        public class MembershipsSection
        {
            public TextContentItem[] TextContent { get; set; }
            public ImageContentItem[] ImageContent { get; set; }
            public DownloadableContentItem[] DownloadableContent { get; set; }
        }

        public class MembershipsUpdateModel
        {
            // Intro
            public string Heading { get; set; }
            public string Paragraph1 { get; set; }
            public string Paragraph2 { get; set; }
            public string Paragraph3 { get; set; }
            public string Paragraph4 { get; set; }
            public string Paragraph5 { get; set; }
            public string Paragraph6 { get; set; }

            // Packages
            public string BronzeHeading { get; set; }
            public string BronzeParagraph { get; set; }
            public string SilverHeading { get; set; }
            public string SilverParagraph { get; set; }
            public string GoldHeading { get; set; }
            public string GoldParagraph { get; set; }
            public string CPDHeading { get; set; }
            public string CPDParagraph { get; set; }
            public string CompetitionsHeading { get; set; }
            public string CompetitionsParagraph { get; set; }
            public string GetInTouchHeading { get; set; }
            public string GetInTouchParagraph { get; set; }

            // Images
            public string BronzeImageSrc { get; set; }
            public string BronzeImageAlt { get; set; }
            public string SilverImageSrc { get; set; }
            public string SilverImageAlt { get; set; }
            public string GoldImageSrc { get; set; }
            public string GoldImageAlt { get; set; }
            public string CPDImageSrc { get; set; }
            public string CPDImageAlt { get; set; }
            public string CompetitionsImageSrc { get; set; }
            public string CompetitionsImageAlt { get; set; }

            // Downloadable
            public string PackagesDocumentSrc { get; set; }
            public string PackagesDocumentAlt { get; set; }
        }

        //Staff Area:
        public class StaffDocumentItem
        {
            public string Src { get; set; }
            public string Alt { get; set; }
        }

        public class StaffSection
        {
            public List<StaffDocumentItem> Documents { get; set; } = new List<StaffDocumentItem>();
            public string Heading { get; set; }
            public string Paragraph { get; set; }
            public string ImageSrc { get; set; }
            public string ImageAlt { get; set; }
        }

        public class StaffWrapper
        {
            public StaffSection[] Sections { get; set; } = new StaffSection[0];
        }

        public class SaveStaffDocumentsModel
        {
            public List<StaffDocumentItem> Documents { get; set; }
        }

        public class SaveStaffInfoModel
        {
            public string Heading { get; set; }
            public string Paragraph { get; set; }
            public string ImageSrc { get; set; }
            public string ImageAlt { get; set; }
        }

        public class StaffDocumentsUpdateModel
        {
            public List<DocumentItem> Documents { get; set; } = new List<DocumentItem>();
        }

        public class DocumentItem
        {
            public string Src { get; set; }      // The document URL
            public string Alt { get; set; }      // Display name / label
        }

        public class StaffInfoUpdateModel
        {
            public string Paragraph { get; set; }   // The single editable info paragraph
        }

        public class StaffDocsWrapper
        {
            public StaffDocsSection[] Sections { get; set; }
        }

        public class StaffDocsSection
        {
            public DownloadableContentItem[] Documents { get; set; }
        }

        public class StaffInfoWrapper
        {
            public StaffInfoSection[] Sections { get; set; }
        }

        public class StaffInfoSection
        {
            public TextContentItem[] TextContent { get; set; }
        }

        // Events

        public class EventItemModel
        {
            public string Heading { get; set; }
            public string Paragraph { get; set; }
            public string Status { get; set; }    // e.g., "Coming Soon", "Open", "Closed"
            public string BookingUrl { get; set; }
            public string StartDate { get; set; } // new
            public string EndDate { get; set; }   // new
        }

        public class EventsUpdateModel
        {
            public List<EventItemModel> Events { get; set; } = new();
        }
        public class EventItem
        {
            public string Heading { get; set; }
            public string Paragraph { get; set; }
            public string Status { get; set; }
            public string BookingUrl { get; set; }
            public string StartDate { get; set; } // new
            public string EndDate { get; set; }   // new
        }

        public class EventSection
        {
            public List<EventItem> TextContent { get; set; }
        }

        public class EventsWrapper
        {
            public List<EventSection> Sections { get; set; }
        }

        public class MembersAreaUpdateModel
        {
            public IntroDto Intro { get; set; }
            public List<SchemeDto> Schemes { get; set; } = new();
        }

        public class IntroDto
        {
            public string Heading { get; set; }
            public string Paragraph { get; set; }
        }

        public class SchemeDto
        {
            public string YearGroup { get; set; }
            public List<DocumentDto> Documents { get; set; } = new();
        }

        public class DocumentDto
        {
            public string Title { get; set; }
            public string Url { get; set; }
        }


    }
}
