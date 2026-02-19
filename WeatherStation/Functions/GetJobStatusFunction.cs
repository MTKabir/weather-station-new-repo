using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using WeatherStation.Models;

namespace WeatherStation.Functions;

public class GetJobStatusFunction
{
    private readonly TableClient _tableClient;
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<GetJobStatusFunction> _logger;

    public GetJobStatusFunction(
        IConfiguration configuration,
        ILogger<GetJobStatusFunction> logger)
    {
        _logger = logger;

        var connectionString = configuration["StorageConnection"];
        var containerName = configuration["ImageContainerName"];

        _tableClient = new TableClient(connectionString, "JobStatusTable");
        _containerClient = new BlobContainerClient(connectionString, containerName);
    }

    [Function("GetJobStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs/{jobId}")]
        HttpRequestData req,
        string jobId)
    {
        _logger.LogInformation("Fetching status for job {JobId}", jobId);

        // 1️⃣ Get Job from Table
        JobEntity? job;
        try
        {
            var response = await _tableClient.GetEntityAsync<JobEntity>("job", jobId);
            job = response.Value;
        }
        catch
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Job not found");
            return notFound;
        }

        // 2️⃣ Get Blob URLs WITH SAS
        var imageUrls = new List<string>();

        await foreach (var blobItem in _containerClient.GetBlobsAsync(
            BlobTraits.None,
            BlobStates.None,
            $"{jobId}/",
            CancellationToken.None))
        {
            var blobClient = _containerClient.GetBlobClient(blobItem.Name);

            // Generate 1-hour read-only SAS
            var sasUri = blobClient.GenerateSasUri(
                BlobSasPermissions.Read,
                DateTimeOffset.UtcNow.AddHours(1));

            imageUrls.Add(sasUri.ToString());
        }

        imageUrls.Sort();

        // 3️⃣ Return result
        var responseOk = req.CreateResponse(HttpStatusCode.OK);

        await responseOk.WriteAsJsonAsync(new
        {
            jobId = job.RowKey,
            status = job.Status,
            completed = job.CompletedImages,
            total = job.TotalImages,
            images = imageUrls
        });

        return responseOk;
    }
}
