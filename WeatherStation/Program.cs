using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WeatherStation.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddSingleton<JobStatusService>();
builder.Build().Run();

