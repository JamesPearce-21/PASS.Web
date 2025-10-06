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





        // Models:

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

    }
}
