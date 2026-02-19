using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WeatherStation.Models;

namespace WeatherStation.Functions
{
    internal class JobStarterFunction
    {
        private readonly HttpClient _httpClient;
        private readonly QueueClient _imageQueueClient;
        private readonly TableClient _tableClient;
        private readonly ILogger<JobStarterFunction> _logger;
        private readonly IConfiguration _configuration;

        public JobStarterFunction(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<JobStarterFunction> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _logger = logger;

            var connectionString = configuration["StorageConnection"];

            // Queue
            _imageQueueClient = new QueueClient(connectionString, "image-process-queue");
            _imageQueueClient.CreateIfNotExists();

            // Table
            var tableServiceClient = new TableServiceClient(connectionString);
            _tableClient = tableServiceClient.GetTableClient("JobStatusTable");
            _tableClient.CreateIfNotExists();
        }

        [Function("JobStarter")]
        public async Task Run(
            [QueueTrigger("job-start-queue", Connection = "StorageConnection")]
            string queueMessage)
        {
            var startMessage = JsonSerializer.Deserialize<StartJobMessage>(queueMessage);

            if (startMessage == null)
            {
                _logger.LogError("Invalid start message");
                return;
            }

            _logger.LogInformation("Processing job {JobId}", startMessage.JobId);

            var apiUrl = _configuration["BuienradarApiUrl"];

            if (string.IsNullOrWhiteSpace(apiUrl))
                throw new InvalidOperationException("BuienradarApiUrl is not configured.");

            // Fetch weather data
            var response = await _httpClient.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);

            var stations = document
                .RootElement
                .GetProperty("actual")
                .GetProperty("stationmeasurements")
                .EnumerateArray()
                .Take(50)
                .ToList();

            var actualStationCount = stations.Count;

            _logger.LogInformation("Station count: {Count}", actualStationCount);

            // 🔥 UPDATE JOB ENTITY WITH REAL COUNT
            var jobEntityResponse = await _tableClient.GetEntityAsync<JobEntity>(
                "job",
                startMessage.JobId);

            var jobEntity = jobEntityResponse.Value;

            jobEntity.TotalImages = actualStationCount;
            jobEntity.Status = "Processing";

            await _tableClient.UpdateEntityAsync(
                jobEntity,
                jobEntity.ETag,
                TableUpdateMode.Replace);

            // 🔥 FAN-OUT
            foreach (var station in stations)
            {
                if (!station.TryGetProperty("stationname", out var nameElement))
                    continue;

                if (nameElement.ValueKind != JsonValueKind.String)
                    continue;

                var stationName = nameElement.GetString() ?? "Unknown";

                // Default temperature
                string temperature = "N/A";

                if (station.TryGetProperty("temperature", out var tempElement))
                {
                    if (tempElement.ValueKind == JsonValueKind.Number)
                    {
                        temperature = tempElement.GetDouble().ToString("F1");
                    }
                }

                var imageMessage = new ImageProcessMessage
                {
                    JobId = startMessage.JobId,
                    StationName = stationName,
                    Temperature = temperature
                };

                var imageJson = JsonSerializer.Serialize(imageMessage);
                var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(imageJson));

                await _imageQueueClient.SendMessageAsync(base64);
            }


            _logger.LogInformation("Fan-out completed for job {JobId}", startMessage.JobId);
        }
    }
}
