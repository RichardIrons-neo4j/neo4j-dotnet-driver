﻿using System.Linq;
using System.Threading.Tasks;
using Neo4j.Driver.Internal.Routing;
using Newtonsoft.Json;

namespace Neo4j.Driver.Tests.TestBackend;

internal class GetRoutingTable : IProtocolObject
{
    public GetRoutingTableDataType data { get; set; } = new();

    [JsonIgnore] public IRoutingTable RoutingTable { get; set; }

    public override async Task Process(Controller controller)
    {
        var protocolDriver = (NewDriver)ObjManager.GetObject(data.driverId);
        var driver = (Internal.Driver)protocolDriver.Driver;
        RoutingTable = driver.GetRoutingTable(data.database);

        await Task.CompletedTask;
    }

    public override string Respond()
    {
        return new ProtocolResponse(
            "RoutingTable",
            new
            {
                database = RoutingTable.Database,
                ttl = "huh",
                routers = RoutingTable.Routers.Select(x => x.Authority),
                readers = RoutingTable.Readers.Select(x => x.Authority),
                writers = RoutingTable.Writers.Select(x => x.Authority)
            }).Encode();
    }

    public class GetRoutingTableDataType
    {
        public string driverId { get; set; }
        public string database { get; set; }
    }
}
