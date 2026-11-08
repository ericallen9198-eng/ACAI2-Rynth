#nullable disable
// Wire-compatible snapshot; provenance: origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;

namespace ACAI2.Protocol
{
    public static class ProtocolInfo
    {
        public const string Version = "2";
    }

    public sealed class ClientRegistration
    {
        public string ProtocolVersion { get; set; }
        public string InstanceId { get; set; }
        public string Server { get; set; }
        public string Character { get; set; }
        public string RuntimeId { get; set; }
        public string PluginVersion { get; set; }
        public string StartedUtc { get; set; }
    }

    public sealed class ClientHeartbeat
    {
        public string ProtocolVersion { get; set; }
        public string InstanceId { get; set; }
        public string Server { get; set; }
        public string Character { get; set; }
        public string RuntimeId { get; set; }
        public string SentUtc { get; set; }
    }

    public sealed class NearbyEntity
    {
        public string Name { get; set; }
        public int ObjectId { get; set; }
        public string Kind { get; set; }
        public double Distance { get; set; }
    }

    public sealed class WorldPerception
    {
        public string ProtocolVersion { get; set; }
        public string InstanceId { get; set; }
        public string Server { get; set; }
        public string Character { get; set; }
        public string RuntimeId { get; set; }
        public string CapturedUtc { get; set; }

        // Legacy/simple name lists are retained because they are convenient for
        // human-readable output and keep Phase 1 consumers compatible.
        public List<string> Players { get; set; } = new List<string>();
        public List<string> Npcs { get; set; } = new List<string>();
        public List<string> Monsters { get; set; } = new List<string>();
        public List<string> Portals { get; set; } = new List<string>();
        public List<string> Containers { get; set; } = new List<string>();
        public List<string> Objects { get; set; } = new List<string>();

        // Structured nearby entities are the forward-looking perception model.
        // Distance is the numeric value returned by Decal WorldFilter.Distance.
        public List<NearbyEntity> PlayerEntities { get; set; } = new List<NearbyEntity>();
        public List<NearbyEntity> NpcEntities { get; set; } = new List<NearbyEntity>();
        public List<NearbyEntity> MonsterEntities { get; set; } = new List<NearbyEntity>();
        public List<NearbyEntity> PortalEntities { get; set; } = new List<NearbyEntity>();
        public List<NearbyEntity> ContainerEntities { get; set; } = new List<NearbyEntity>();
        public List<NearbyEntity> OtherEntities { get; set; } = new List<NearbyEntity>();
    }

    public sealed class BridgeAck
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public string BridgeUtc { get; set; }
    }
}
