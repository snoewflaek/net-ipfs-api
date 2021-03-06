﻿using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ipfs.CoreApi;

namespace Ipfs.Api
{

    class SwarmApi : ISwarmApi
    {
        IpfsClient ipfs;

        internal SwarmApi(IpfsClient ipfs)
        {
            this.ipfs = ipfs;
        }

        public async Task<IEnumerable<Peer>> AddressesAsync(CancellationToken cancel = default(CancellationToken))
        {
            var json = await ipfs.DoCommandAsync("swarm/addrs", cancel);
            return ((JObject)JObject.Parse(json)["Addrs"])
                .Properties()
                .Select(p => new Peer {
                    Id = p.Name,
                    Addresses = ((JArray)p.Value)
                        .Select(v => new MultiAddress((string)v))
                });
        }

        public async Task<IEnumerable<Peer>> PeersAsync(CancellationToken cancel = default(CancellationToken))
        {
            var json = await ipfs.DoCommandAsync("swarm/peers", cancel, null, "verbose=true");
            var result = JObject.Parse(json);

            // Older servers return an array of strings
            var strings = (JArray)result["Strings"];
            if (strings != null)
            {
                return strings
                   .Select(s =>
                   {
                       var parts = ((string)s).Split(' ');
                       var address = new MultiAddress(parts[0]);
                       return new Peer
                       {
                           Id = address.Protocols.First(p => p.Name == "ipfs").Value,
                           ConnectedAddress = parts[0],
                           Latency = ParseLatency(parts[1])
                       };
                   });
            }

            // Current servers return JSON
            var peers = (JArray)result["Peers"];
            if (peers != null)
            {
                return peers.Select(p => new Peer
                {
                    Id = (string)p["Peer"],
                    ConnectedAddress = new MultiAddress((string)p["Addr"] + "/ipfs/" + (string)p["Peer"]),
                    Latency = ParseLatency((string)p["Latency"])
                });
            }
            
            // Hmmm. Another change we can handle
            throw new FormatException("Unknown response from 'swarm/peers");
        }

        TimeSpan ParseLatency(string latency)
        {
            if (latency == "n/a" || latency == "unknown")
            {
                return TimeSpan.Zero;
            }
            if (latency.EndsWith("ms"))
            {
                var ms = Double.Parse(latency.Substring(0, latency.Length - 2));
                return TimeSpan.FromMilliseconds(ms);
            }
            if (latency.EndsWith("s"))
            {
                var sec = Double.Parse(latency.Substring(0, latency.Length - 1));
                return TimeSpan.FromSeconds(sec);
            }

            throw new FormatException(String.Format("Invalid latency unit '{0}'.", latency));
        }

        public async Task ConnectAsync(MultiAddress address, CancellationToken cancel = default(CancellationToken))
        {
            await ipfs.DoCommandAsync("swarm/connect", cancel, address.ToString());
        }

        public async Task DisconnectAsync(MultiAddress address, CancellationToken cancel = default(CancellationToken))
        {
            await ipfs.DoCommandAsync("swarm/disconnect", cancel, address.ToString());
        }
    }

}
