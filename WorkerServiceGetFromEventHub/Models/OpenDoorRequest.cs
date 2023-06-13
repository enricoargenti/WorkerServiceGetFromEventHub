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
    public int GatewayId { get; set; }
    public int DeviceGeneratedCode { get; set; }
    public int CloudGeneratedCode { get; set; }
    public DateTime AccessRequestTime { get; set; }

    public OpenDoorRequest()
    {

    }
}
