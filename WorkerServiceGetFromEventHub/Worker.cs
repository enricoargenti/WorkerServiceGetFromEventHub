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
using Microsoft.VisualBasic;

namespace WorkerServiceGetFromEventHub;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly ApiProxyService _apiProxyService;

    private EventProcessorClient _processor;
    private EventHubConsumerClient _eventHubConsumerClient;
    private ServiceClient _serviceClient;


    public Worker(ILogger<Worker> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory, ApiProxyService apiProxyService)
    {
        _logger = logger;
        _configuration = configuration;
        //_serviceClient = ServiceClient.CreateFromConnectionString(_configuration.GetConnectionString("IoTHub"));
        _apiProxyService = apiProxyService;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // To read messages from IoTHub EventHub
        string connStringEventHub = _configuration.GetConnectionString("EventHub");
        string connStringstorageAccount = _configuration.GetConnectionString("StorageAccount");
        _eventHubConsumerClient = new EventHubConsumerClient(EventHubConsumerClient.DefaultConsumerGroupName,
                                    connStringEventHub.Replace("sb://", "amqps://"));

        // To send messages to IoTHub devices
        string connStringIoTHub = _configuration.GetConnectionString("IoTHub");
        _serviceClient = ServiceClient.CreateFromConnectionString(connStringIoTHub);

        // Create tasks to run continuously
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


    // Cloud to device messages method
    private async Task SendCloudToDeviceMessageAsync(string device, string code)
    {
        try
        {
            var commandMessage = new Message(Encoding.ASCII.GetBytes(code));
            await _serviceClient.SendAsync(device, commandMessage);
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }


    async Task ReceiveMessagesFromDeviceAsync(string partitionId)
    {
        Console.WriteLine($"Starting listener thread for partition: {partitionId}");
        while (true)
        {
            await foreach (PartitionEvent receivedEvent in _eventHubConsumerClient.ReadEventsFromPartitionAsync(partitionId, EventPosition.Latest))
            {
                string body = Encoding.UTF8.GetString(receivedEvent.Data.Body.ToArray());

                if (receivedEvent.Data.SystemProperties.ContainsKey("iothub-message-source"))
                {
                    //msgSource = receivedEvent.Data.SystemProperties["iothub-message-source"].ToString();
                    Console.WriteLine($"Message: {body}");

                    // Json deserialization
                    OpenDoorRequest message = new OpenDoorRequest();
                    try
                    {
                        var msg = JsonSerializer.Deserialize<OpenDoorRequest>(body);
                        message = msg;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }

                    // Control if the message is a new openRequest
                    if (message.TypeOfMessage == "firstMessageFromDoor")
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

                        // Get the corresponding request
                        OpenDoorRequest openDoorRequest = await _apiProxyService.GetDoorOpenRequestAsync(deviceGeneratedCode);

                        // Create a new cloud to device message
                        C2dMessage c2DMessage = new C2dMessage();

                        c2DMessage.DoorId = message.DoorId;

                        // If the code inserted by the user on the door keypad is equal to the code generated by the cloud
                        if (openDoorRequest.CloudGeneratedCode.Equals(message.CodeInsertedOnDoorByUser))
                        {
                            Console.WriteLine("C2D MESSAGE: OPEN THE DOOR");
                            c2DMessage.Action = 1;

                            // Add the log to the database
                            Access access = new Access();
                            access.UserId = openDoorRequest.UserId;
                            access.DoorId = openDoorRequest.DoorId;
                            access.DeviceId = openDoorRequest.DeviceId;
                            access.AccessRequestTime = openDoorRequest.AccessRequestTime;
                            await _apiProxyService.InsertAccessAsync(access);
                        }
                        else
                        {
                            Console.WriteLine("C2D MESSAGE: CANNOT OPEN THE DOOR");
                            c2DMessage.Action = 2;
                        }
                        string jsonC2DMessage = JsonSerializer.Serialize(c2DMessage);
                        SendCloudToDeviceMessageAsync(message.DeviceId, jsonC2DMessage).Wait();

                        Console.WriteLine($"Message to gateway {message.DeviceId} sent: {jsonC2DMessage}\n");

                        // Delete the request from the db in both cases
                        await _apiProxyService.DeleteOpenDoorRequestAsync(openDoorRequest.Id.Value);

                    }



                }
            }
        }
    }


}