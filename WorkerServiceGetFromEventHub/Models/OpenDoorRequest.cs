using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerServiceGetFromEventHub.Models;

internal class OpenDoorRequest
{
    public string DoorID { get; set; }
    public string GatewayID { get; set; }
    public string DeviceGeneratedCode { get; set; }
    public DateTime Time { get; set; }

    public OpenDoorRequest()
    {

    }
}
