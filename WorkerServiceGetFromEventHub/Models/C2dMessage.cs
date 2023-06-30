using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerServiceGetFromEventHub.Models;

internal class C2dMessage
{
    // Hey: questo è un messaggio dal cloud e non da me (Gateway)
    // a (1 byte) che vuol dire 
    public int DoorId { get; set; }
    public int Action { get; set; } // 1 = Open 2 = Not Open 3 = Exception (error)

}
