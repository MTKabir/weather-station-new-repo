using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using WeatherStation.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
        services.AddSingleton<JobStatusService>();
    })
    .Build();

host.Run();
