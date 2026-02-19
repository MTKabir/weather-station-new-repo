using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using WeatherStation.Models;
using WeatherStation.Services;

namespace WeatherStation.Functions;

public class StartJobFunction
{
    private readonly JobStatusService _jobStatusService;
    private readonly QueueClient _queueClient;
    private readonly ILogger<StartJobFunction> _logger;

    public StartJobFunction(
        JobStatusService jobStatusService,
        IConfiguration configuration,
        ILogger<StartJobFunction> logger)
    {
        _jobStatusService = jobStatusService;
        _logger = logger;

        var connectionString = configuration["StorageConnection"];
        _queueClient = new QueueClient(connectionString, "job-start-queue");
        _queueClient.CreateIfNotExists();
    }

    [Function("StartJob")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "jobs")]
        HttpRequestData req)
    {
        var jobId = Guid.NewGuid().ToString();

        _logger.LogInformation("Starting job {JobId}", jobId);

        // 1. Create job record
        await _jobStatusService.CreateJobAsync(jobId);

        // 2. Send message to queue
        var message = new StartJobMessage
        {
            JobId = jobId
        };

        var json = JsonSerializer.Serialize(message);
        var base64Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        await _queueClient.SendMessageAsync(base64Message);

        // 3. Return 202
        var response = req.CreateResponse(HttpStatusCode.Accepted);

        await response.WriteAsJsonAsync(new
        {
            jobId,
            statusUrl = $"/api/jobs/{jobId}"
        });

        return response;
    }
}
