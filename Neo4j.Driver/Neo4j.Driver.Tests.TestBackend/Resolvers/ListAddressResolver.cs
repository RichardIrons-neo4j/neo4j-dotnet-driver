﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo4j.Driver.Tests.TestBackend;

internal class ListAddressResolver : IServerAddressResolver
{
    private readonly ServerAddress[] servers;

    public ListAddressResolver(Controller control, string uri)
    {
        Control = control;
        Uri = new Uri(uri);
    }

    public ListAddressResolver(params ServerAddress[] servers)
    {
        this.servers = servers;
    }

    private Controller Control { get; }
    private Uri Uri { get; }

    public ISet<ServerAddress> Resolve(ServerAddress address)
    {
        var errorMessage =
            "A ResolverResolutionCompleted request is expected straight after a ResolverResolutionRequired reponse is sent";

        var response = new ProtocolResponse(
                "ResolverResolutionRequired",
                new
                {
                    id = ProtocolObjectManager.GenerateUniqueIdString(),
                    address = Uri.Host + ":" + Uri.Port
                })
            .Encode();

        //Send the ResolverResolutionRequired response
        Control.SendResponse(response).ConfigureAwait(false);

        //Read the ResolverResolutionCompleted request, throw if another type of request has come in
        var result = Control.TryConsumeStreamObjectOfType<ResolverResolutionCompleted>().Result;
        if (result is null)
        {
            throw new NotSupportedException(errorMessage);
        }

        //Return a IServerAddressResolver instance thats Resolve method uses the addresses in the ResolverResolutionoCompleted request.
        return new HashSet<ServerAddress>(
            result
                .data
                .addresses
                .Select(
                    x =>
                    {
                        var split = x.Split(':');
                        return ServerAddress.From(split[0], Convert.ToInt32(split[1]));
                    }));
    }
}
