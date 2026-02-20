using Azure.Storage.Blobs;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WeatherStationNew.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;


namespace WeatherStationNew.Functions
{
    public class ImageProcessorFunction
    {
        private readonly HttpClient _httpClient;
        private readonly BlobContainerClient _containerClient;
        private readonly TableClient _tableClient;
        private readonly ILogger<ImageProcessorFunction> _logger;

        public ImageProcessorFunction(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ImageProcessorFunction> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;

            var connectionString = configuration["StorageConnection"];
            var containerName = configuration["ImageContainerName"];

            // Blob setup
            var blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            _containerClient.CreateIfNotExists();

            // Table setup
            _tableClient = new TableClient(connectionString, "JobStatusTable");
            _tableClient.CreateIfNotExists();
        }

    [Function("ImageProcessor")]
    public async Task Run(
    [QueueTrigger("image-process-queue", Connection = "StorageConnection")]
    string queueMessage)
        {
            var message = JsonSerializer.Deserialize<ImageProcessMessage>(queueMessage);

            if (message == null)
            {
                _logger.LogError("Invalid image message.");
                return;
            }

            _logger.LogInformation("Processing image for station {Station}", message.StationName);

            // 1️⃣ Download placeholder image
            var imageUrl = "https://picsum.photos/600/400";
            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);

            // 2️⃣ Load image into ImageSharp
            using var image = Image.Load<Rgba32>(imageBytes);

            // 3️⃣ Create font
            var font = SystemFonts.CreateFont(SystemFonts.Families.First().Name, 36);

            // 4️⃣ Prepare overlay text
            var text = $"{message.StationName}\nTemp: {message.Temperature} °C";

            // 5️⃣ Draw text on image
            image.Mutate(ctx =>
            {
                ctx.DrawText(
                    text,
                    font,
                    Color.White,
                    new PointF(20, 20));
            });

            // 6️⃣ Upload modified image to Blob
            var blobName = $"{message.JobId}/{Guid.NewGuid()}.jpg";
            var blobClient = _containerClient.GetBlobClient(blobName);

            using var outputStream = new MemoryStream();
            image.SaveAsJpeg(outputStream);
            outputStream.Position = 0;

            await blobClient.UploadAsync(outputStream, overwrite: true);

            _logger.LogInformation("Uploaded image {BlobName}", blobName);

            // 7️⃣ Update job progress safely
            await UpdateJobProgressAsync(message.JobId);
        }

        private async Task UpdateJobProgressAsync(string jobId)
        {
            const int maxRetries = 10;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await _tableClient.GetEntityAsync<JobEntity>("job", jobId);
                    var entity = response.Value;

                    entity.CompletedImages++;

                    if (entity.CompletedImages >= entity.TotalImages)
                    {
                        entity.Status = "Completed";
                    }

                    await _tableClient.UpdateEntityAsync(
                        entity,
                        entity.ETag,
                        TableUpdateMode.Replace);

                    _logger.LogInformation(
                        "Updated progress: {Completed}/{Total}",
                        entity.CompletedImages,
                        entity.TotalImages);

                    return; // SUCCESS
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 412)
                {
                    _logger.LogWarning(
                        "ETag conflict detected. Retry {Attempt}/{MaxRetries}",
                        attempt,
                        maxRetries);

                    await Task.Delay(50);
                }
            }

            _logger.LogError("Failed to update job progress after max retries.");
        }

    }
}
