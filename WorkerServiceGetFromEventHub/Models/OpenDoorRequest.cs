using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerServiceGetFromEventHub.Models;

internal class OpenDoorRequest
{
    public int? Id { get; set; }
    public int DoorId { get; set; }
    public string DeviceId { get; set; }
    public string DeviceGeneratedCode { get; set; }
    public string? CloudGeneratedCode { get; set; }
    public DateTime AccessRequestTime { get; set; }

    public OpenDoorRequest()
    {

    }
}
