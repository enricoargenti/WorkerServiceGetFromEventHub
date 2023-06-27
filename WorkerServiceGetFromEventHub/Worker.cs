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

    private OpenDoorRequest _openDoorRequest;

    //private Timer _timer;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        client.BaseAddress = new Uri("https://localhost:7295/");
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        string connStringEventHub = _configuration.GetConnectionString("EventHub");
        string connStringstorageAccount = _configuration.GetConnectionString("StorageAccount");

        _eventHubConsumerClient = new EventHubConsumerClient(EventHubConsumerClient.DefaultConsumerGroupName,
            connStringEventHub.Replace("sb://", "amqps://"));

        var tasks = new List<Task>();
        var partitions = await _eventHubConsumerClient.GetPartitionIdsAsync();
        foreach (string partition in partitions)
        {
            tasks.Add(ReceiveMessagesFromDeviceAsync(partition));
        }

        // Calls the function every three minutes to delete older requests
        TimeSpan interval = TimeSpan.FromMinutes(3);   // FromSeconds(10);
        //_timer = new Timer(DeleteExpiredOpenDoorRequestsAsync, null, TimeSpan.Zero, interval);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        //_timer?.Dispose();
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


    async Task ReceiveMessagesFromDeviceAsync(string partitionId)
    {
        Console.WriteLine($"Starting listener thread for partition: {partitionId}");
        while (true)
        {
            await foreach (PartitionEvent receivedEvent in _eventHubConsumerClient.ReadEventsFromPartitionAsync(partitionId, EventPosition.Latest))
            {
                string msgSource;
                string body = Encoding.UTF8.GetString(receivedEvent.Data.Body.ToArray());

                if (receivedEvent.Data.SystemProperties.ContainsKey("iothub-message-source"))
                {
                    //msgSource = receivedEvent.Data.SystemProperties["iothub-message-source"].ToString();
                    Console.WriteLine($"Message: {body}");

                    // json deserialize
                    var message = JsonSerializer.Deserialize<OpenDoorRequest>(body);

                    Console.WriteLine("DooId -> " + message.DoorId);
                    Console.WriteLine("Gateway (DeviceId) -> " + message.DeviceId);
                    Console.WriteLine("DeviceGeneratedCode -> " + message.DeviceGeneratedCode);
                    Console.WriteLine("CodeInsertedOnDoorByUser -> " + message.CodeInsertedOnDoorByUser);

                    // Control if the message is a new openRequest
                    if (message.TypeOfMessage == "newOpenDoorRequest")
                    {
                        // Delete the older codes (more than three minutes)
                        await DeleteCodes(3);

                        // Random code generation
                        Random random = new Random();
                        string randomGeneratedCode = "";
                        for (int i = 0; i < 5; i++)
                        {
                            // Generate a random number between 1 and 9
                            randomGeneratedCode += random.Next(1, 10).ToString();
                        }
                        message.CloudGeneratedCode = randomGeneratedCode;


                        // Let'ws try to write it into the database calling our APIs
                        try
                        {
                            Uri location = await InsertOpenDoorRequestAsync(message);
                            Console.WriteLine($"OpenDoorRequest added to the database");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"An error occurred: {ex.Message}");
                        }
                    }
                    else if (message.TypeOfMessage == "secondMessageFromDoor")
                    {
                        // This code will be the key to access the correct row inside the db
                        string deviceGeneratedCode = message.DeviceGeneratedCode;
                        //
                        Console.WriteLine("Second message: to do");
                        await GetOpenDoorRequest(deviceGeneratedCode);

                        // Now that we have the openDoorRequest, we can make a PUT to alter it
                        _openDoorRequest.CodeInsertedOnDoorByUser = message.CodeInsertedOnDoorByUser;

                        await AddCodeInsertedOnDoorByUser();

                    }



                }
            }
        }
    }


    public async Task GetOpenDoorRequest(string deviceGeneratedCode)
    {
        try
        {
            string path = $"api/DoorOpenRequest/deviceGeneratedCode/{deviceGeneratedCode}";

            _openDoorRequest = await GetOpenDoorRequestAsync(path);

            if (_openDoorRequest != null)
            {
                Console.WriteLine($"OpenDoorRequest found: {_openDoorRequest.Id}");
            }
            else
            {
                Console.WriteLine("OpenDoorRequest not found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public async Task AddCodeInsertedOnDoorByUser()
    {
        try
        {
            string path = $"api/DoorOpenRequest/{_openDoorRequest.Id}";

            await UpdateOpenDoorRequestAsync(path, _openDoorRequest);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public async Task DeleteCodes(int minutes)
    {
        try
        {
            string path = $"api/DoorOpenRequest/minutes/{minutes}";
            await DeleteOpenDoorRequestsAsync(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }


    static async Task<Uri> InsertOpenDoorRequestAsync(OpenDoorRequest openDoorRequest)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("api/DoorOpenRequest", openDoorRequest);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) 
        { 
            Console.WriteLine(ex.Message);
        }
        // return URI of the created resource.
        return response.Headers.Location;
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

    static async Task UpdateOpenDoorRequestAsync(string path, OpenDoorRequest content)
    {
        HttpResponseMessage response = await client.PutAsJsonAsync(path, content);

        Console.WriteLine();
        Console.WriteLine("content: " + JsonSerializer.Serialize(content));
        Console.WriteLine("path: " + path);

        Console.WriteLine("Response della PUT: " + response.StatusCode);
        Console.WriteLine(response.ToString());
        Console.WriteLine(response.RequestMessage);
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("OpenDoorRequest successfully updated");
        }
        else
        {
            Console.WriteLine("Failed OpenDoorRequest update");
        }
    }

    static async Task DeleteOpenDoorRequestsAsync(string path)
    {
        HttpResponseMessage response = await client.DeleteAsync(path);
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Expired rows successfully deleted");
        }
        else
        {
            Console.WriteLine("Error: the delete action could not be processed for any reason");
        }
    }

}