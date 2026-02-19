using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using WeatherStation.Models;

namespace WeatherStation.Services;

public class JobStatusService
{
    private readonly TableClient _tableClient;

    public JobStatusService(IConfiguration configuration)
    {
        var connectionString = configuration["StorageConnection"];
        _tableClient = new TableClient(connectionString, "JobStatusTable");
        _tableClient.CreateIfNotExists();
    }

    public async Task CreateJobAsync(string jobId)
    {
        var entity = new JobEntity
        {
            PartitionKey = "job",
            RowKey = jobId,
            Status = "Pending",
            TotalImages = 0,
            CompletedImages = 0
        };

        await _tableClient.AddEntityAsync(entity);
    }

    public async Task<JobEntity?> GetJobAsync(string jobId)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<JobEntity>("job", jobId);
            return response.Value;
        }
        catch
        {
            return null;
        }
    }

    public async Task IncrementCompletedAsync(string jobId)
    {
        while (true)
        {
            var entity = await GetJobAsync(jobId);
            if (entity == null) return;

            entity.CompletedImages++;

            if (entity.CompletedImages >= entity.TotalImages)
            {
                entity.Status = "Completed";
            }
            else
            {
                entity.Status = "Processing";
            }

            try
            {
                await _tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
                break;
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                continue;
            }
        }
    }
}
