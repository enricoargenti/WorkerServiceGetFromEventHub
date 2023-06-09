using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using System.Text;

namespace WorkerServiceGetFromEventHub;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private EventProcessorClient _processor;
    private EventHubConsumerClient _eventHubConsumerClient;
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


        //var blobContainerName = "events";
        //// Create a blob container client that the event processor will use
        //BlobContainerClient storageClient = new BlobContainerClient(connStringstorageAccount, blobContainerName);
        //storageClient.CreateIfNotExists();
        //// Create an event processor client to process events in the event hub
        //// TODO: Replace the <EVENT_HUBS_NAMESPACE> and <HUB_NAME> placeholder values
        //_processor = new EventProcessorClient(
        //    storageClient,
        //    EventHubConsumerClient.DefaultConsumerGroupName,
        //    connStringEventHub);

        //// Register handlers for processing events and handling errors
        //_processor.ProcessEventAsync += ProcessEventHandler;
        //_processor.ProcessErrorAsync += ProcessErrorHandler;

        //// Start the processing
        //await _processor.StartProcessingAsync();



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
                //if (receivedEvent.Data.SystemProperties.ContainsKey("iothub-message-source"))
                //{
                //    msgSource = receivedEvent.Data.SystemProperties["iothub-message-source"].ToString();
                //    Console.WriteLine($"{partitionId} {msgSource} {body}");
                //}
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
}