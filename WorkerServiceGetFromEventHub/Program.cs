using System.Net.Http.Headers;
using WorkerServiceGetFromEventHub;
using WorkerServiceGetFromEventHub.Services;

// var apiUrl = builder.Configuration.GetValue<string>("apiUrl");

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddHttpClient("api", client =>
        {
            client.BaseAddress = new Uri("https://localhost:7295/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddSingleton<ApiProxyService>();
    })
    .Build();

await host.RunAsync();
