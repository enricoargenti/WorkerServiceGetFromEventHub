using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WorkerServiceGetFromEventHub.Models;

namespace WorkerServiceGetFromEventHub;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private EventProcessorClient _processor;
    private EventHubConsumerClient _eventHubConsumerClient;

    // Client initialization
    static HttpClient client = new HttpClient();
    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        //var connectionString = _configuration.GetConnectionString("EventHub");
        string connStringEventHub = _configuration.GetConnectionString("EventHub");
        //string eventHubName = "picloud";
        string connStringstorageAccount = _configuration.GetConnectionString("StorageAccount");

        // Start the client that calls the APIs
        RunAsync();



        _eventHubConsumerClient = new EventHubConsumerClient(EventHubConsumerClient.DefaultConsumerGroupName,
            connStringEventHub.Replace("sb://", "amqps://"));

        var tasks = new List<Task>();
        var partitions = await _eventHubConsumerClient.GetPartitionIdsAsync();
        foreach (string partition in partitions)
        {
            tasks.Add(ReceiveMessagesFromDeviceAsync(partition));
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processor.StopProcessingAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //while (!stoppingToken.IsCancellationRequested)
        //{

        //    await Task.Delay(30000, stoppingToken);
        //}
    }
    private async Task ProcessEventHandler(ProcessEventArgs eventArgs)
    {
        // Write the body of the event to the console window
        Console.WriteLine("\tReceived event: {0}", Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray()));

        // Update checkpoint in the blob storage so that the app receives only new events the next time it's run
        await eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);
    }

    async Task ReceiveMessagesFromDeviceAsync(string partitionId)
    {
        Console.WriteLine($"Starting listener thread for partition: {partitionId}");
        while (true)
        {
            await foreach (PartitionEvent receivedEvent in _eventHubConsumerClient.ReadEventsFromPartitionAsync(partitionId, EventPosition.Latest))
            {
                string msgSource;
                string body = Encoding.UTF8.GetString(receivedEvent.Data.Body.ToArray());
                //var jsonBody = Encoding.UTF8.GetString(receivedEvent.Data.Body);
                if (receivedEvent.Data.SystemProperties.ContainsKey("iothub-message-source"))
                {
                    msgSource = receivedEvent.Data.SystemProperties["iothub-message-source"].ToString();
                    Console.WriteLine($"{partitionId} {msgSource} {body}");

                    // json deserialize
                    OpenDoorRequest message = JsonSerializer.Deserialize<OpenDoorRequest>(body);

                    Console.WriteLine("DooId -> " + message.DoorId);
                    Console.WriteLine("Gateway (DeviceId) -> " + message.DeviceId);
                    Console.WriteLine("DeviceGeneratedCode -> " + message.DeviceGeneratedCode);

                    // I change the random generated code created from cloud
                    // Random code generation
                    Random random = new Random();
                    string randomGeneratedCode = "";
                    for (int i = 0; i < 5; i++)
                    {
                        // Generate a random number between 1 and 9
                        randomGeneratedCode += random.Next(1, 10).ToString();
                    }
                    message.CloudGeneratedCode = Convert.ToInt32(randomGeneratedCode);


                    // Let'ws try to write it into he database calling our APIs
                    try
                    {
                        Uri location = await InsertOpenDoorRequestAsync(message);
                        Console.WriteLine($"OpenDoorRequest created at {location}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"An error occurred: {ex.Message}");
                    }

                    //Console.WriteLine("door ID: " + message.DoorID);
                    //Console.WriteLine("gateway ID: " + message.GatewayID);
                    //Console.WriteLine("device gen code: " + message.DeviceGeneratedCode);
                }
            }
        }
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
    {
        // Write details about the error to the console window
        Console.WriteLine($"\tPartition '{eventArgs.PartitionId}': an unhandled exception was encountered. This was not expected to happen.");
        Console.WriteLine(eventArgs.Exception.Message);
        return Task.CompletedTask;
    }

    // Http client initialization
    static async Task RunAsync()
    {
        client.BaseAddress = new Uri("https://localhost:7295/");
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    static async Task<OpenDoorRequest> GetOpenDoorRequestAsync(string path)
    {
        OpenDoorRequest openDoorRequest = null;
        HttpResponseMessage response = await client.GetAsync(path);
        if (response.IsSuccessStatusCode)
        {
            openDoorRequest = await response.Content.ReadAsAsync<OpenDoorRequest>();
        }
        return openDoorRequest;
    }

    static async Task<Uri> InsertOpenDoorRequestAsync(OpenDoorRequest openDoorRequest)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("api/DoorOpenRequest", openDoorRequest);
        response.EnsureSuccessStatusCode();

        // return URI of the created resource.
        return response.Headers.Location;
    }
}