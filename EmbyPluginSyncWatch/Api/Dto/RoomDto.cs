using System;
using System.Collections.Generic;

namespace EmbyPluginSyncWatch.Api.Dto
{
    /// <summary>
    /// Data transfer object representing a sync room
    /// </summary>
    public class RoomDto
    {
        /// <summary>Unique room identifier</summary>
        public string Id { get; set; }

        /// <summary>Room display name</summary>
        public string Name { get; set; }

        /// <summary>Number of members in the room</summary>
        public int MemberCount { get; set; }

        /// <summary>Current playback state (Idle, Waiting, Playing, Paused)</summary>
        public string State { get; set; }

        /// <summary>Currently playing item ID</summary>
        public string CurrentItemId { get; set; }

        /// <summary>Current playback position in ticks</summary>
        public long PositionTicks { get; set; }

        /// <summary>Shareable join link</summary>
        public string JoinLink { get; set; }

        /// <summary>Whether the current user is the owner</summary>
        public bool IsOwner { get; set; }

        /// <summary>List of member session IDs</summary>
        public List<string> Members { get; set; }

        /// <summary>When the room was created</summary>
        public DateTime CreatedAt { get; set; }
    }
}
