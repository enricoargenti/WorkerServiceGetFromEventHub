using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerServiceGetFromEventHub.Models;

public class OpenDoorRequest
{
    public int? Id { get; set; }
    public int DoorId { get; set; }
    public string DeviceId { get; set; }
    public string? DeviceGeneratedCode { get; set; }
    public string? CloudGeneratedCode { get; set; }
    public string? CodeInsertedOnDoorByUser { get; set; }
    public DateTime? AccessRequestTime { get; set; }
    public string? UserId { get; set; }
    public string? TypeOfMessage { get; set; }

    public OpenDoorRequest()
    {

    }
}
