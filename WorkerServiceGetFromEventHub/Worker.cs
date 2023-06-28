using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WorkerServiceGetFromEventHub.Models;
using Microsoft.Azure.Devices;
using WorkerServiceGetFromEventHub.Services;

namespace WorkerServiceGetFromEventHub;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly ApiProxyService _apiProxyService;

    private EventProcessorClient _processor;
    private EventHubConsumerClient _eventHubConsumerClient;
    private readonly ServiceClient _serviceClient;
    string connectionString = "HostName=Pi-Cloud.azure-devices.net;SharedAccessKeyName=service;SharedAccessKey=sx1De7uIm+lA/4E1olGyS1tvJjpKt/vzlDbfOs5eqHY=";


    private OpenDoorRequest _openDoorRequest;

    public Worker(ILogger<Worker> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory, ApiProxyService apiProxyService)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
        _apiProxyService = apiProxyService;
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
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processor.StopProcessingAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {}

    private async Task SendCloudToDeviceMessageAsync(string device, string code)
    {
        var commandMessage = new Message(Encoding.ASCII.GetBytes("Random ack code: " + code));
        await _serviceClient.SendAsync(device, commandMessage);
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
                        await _apiProxyService.DeleteExpiredOpenDoorRequestsAsync(3);

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
                        await _apiProxyService.InsertOpenDoorRequestAsync(message);
                    }
                    else if (message.TypeOfMessage == "secondMessageFromDoor")
                    {
                        // This code will be the key to access the correct row inside the db
                        string deviceGeneratedCode = message.DeviceGeneratedCode;

                        OpenDoorRequest openDoorRequest = await _apiProxyService.GetDoorOpenRequestAsync(deviceGeneratedCode);

                        // If the code inserted by the user on the door keypad is equal to the code generated by the cloud
                        if (openDoorRequest.CloudGeneratedCode.Equals(message.CodeInsertedOnDoorByUser))
                        {
                            Console.WriteLine("C2D MESSAGE: OPEN THE DOOR");
                            // c2dMessage.type = "success"
                            // opendoor
                            
                            // ADD THE LOG WITH A POST
                        }
                        else
                        {
                            Console.WriteLine("C2D MESSAGE: CANNOT OPEN THE DOOR");
                            // c2dMessage.type = "failure"
                        }

  



                        //
                        //SendCloudToDeviceMessageAsync(_openDoorRequest.DeviceId, _openDoorRequest.CloudGeneratedCode.ToString()).Wait();

                    }



                }
            }
        }
    }


}