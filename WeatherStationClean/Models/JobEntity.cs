using Azure;
using Azure.Data.Tables;

namespace WeatherStationNew.Models;

public class JobEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "job";
    public string RowKey { get; set; } = default!;

    public string Status { get; set; } = "Running";

    public int TotalImages { get; set; }
    public int CompletedImages { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}
