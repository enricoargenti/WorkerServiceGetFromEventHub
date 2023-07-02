namespace WorkerServiceGetFromEventHub.Models;

public class Access
{
    public string UserId { get; set; }
    public int DoorId { get; set; }
    public string DeviceId { get; set; }
    public DateTime? AccessRequestTime { get; set; }

    public Access()
    {

    }
}
