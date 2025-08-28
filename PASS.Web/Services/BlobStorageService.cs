using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace PASS.Web.Services
{
    public class BlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;

        public BlobStorageService(IConfiguration config)
        {
            var connectionString = config.GetConnectionString("AzureBlobStorage");
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        // --- Existing Upload (private, no SAS) ---
        public async Task UploadFileAsync(string containerName, string fileName, Stream fileStream, string contentType)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            var blobClient = containerClient.GetBlobClient(fileName);
            await blobClient.UploadAsync(fileStream, new BlobHttpHeaders { ContentType = contentType });
        }

        // --- New Upload with SAS URL ---
        public async Task<Uri> UploadFileWithSasUrlAsync(string containerName, string fileName, Stream fileStream, string contentType, int expiryYears = 20)
        {
            // 1️⃣ Get the container client
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            // 2️⃣ Get the blob client
            var blobClient = containerClient.GetBlobClient(fileName);

            // 3️⃣ Delete existing blob if it exists
            if (await blobClient.ExistsAsync())
            {
                await blobClient.DeleteAsync();
            }

            // 4️⃣ Upload the new file
            await blobClient.UploadAsync(fileStream, new BlobHttpHeaders { ContentType = contentType });

            // 5️⃣ Generate SAS URL (read-only, 20 years by default)
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = fileName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddYears(expiryYears)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);

            return sasUri;
        }



        // --- Download file as Stream ---
        public async Task<Stream?> DownloadFileAsync(string containerName, string fileName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (await blobClient.ExistsAsync())
            {
                var response = await blobClient.DownloadAsync();
                return response.Value.Content;
            }
            return null;
        }

        // --- Delete a file ---
        public async Task DeleteFileAsync(string containerName, string fileName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            await blobClient.DeleteIfExistsAsync();
        }

        // --- List all file names in a container ---
        public async Task<List<string>> ListFilesAsync(string containerName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var files = new List<string>();

            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                files.Add(blobItem.Name);
            }

            return files;
        }
    }
}
