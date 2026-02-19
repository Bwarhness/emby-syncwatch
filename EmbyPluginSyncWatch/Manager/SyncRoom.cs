using System;
using System.Collections.Generic;

namespace EmbyPluginSyncWatch.Manager
{
    /// <summary>
    /// Represents the playback state of a sync room
    /// </summary>
    public enum SyncState
    {
        /// <summary>No active playback</summary>
        Idle,
        /// <summary>Waiting for all members to be ready</summary>
        Waiting,
        /// <summary>Actively playing</summary>
        Playing,
        /// <summary>Paused</summary>
        Paused
    }

    /// <summary>
    /// Represents a synchronized playback room
    /// </summary>
    public class SyncRoom
    {
        /// <summary>Unique room identifier</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>Human-readable room name</summary>
        public string Name { get; set; }

        /// <summary>Session ID of the room owner/creator</summary>
        public string OwnerId { get; set; }

        /// <summary>User ID of the room owner</summary>
        public string OwnerUserId { get; set; }

        /// <summary>When the room was created</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Current playback state</summary>
        public SyncState State { get; set; } = SyncState.Idle;

        /// <summary>Currently playing item ID</summary>
        public long CurrentItemId { get; set; }

        /// <summary>Current playback position in ticks</summary>
        public long PositionTicks { get; set; }

        /// <summary>When the position was last updated</summary>
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        /// <summary>Set of session IDs of all room members</summary>
        public HashSet<string> MemberSessionIds { get; set; } = new HashSet<string>();

        /// <summary>Set of session IDs that have confirmed ready state</summary>
        public HashSet<string> ReadySessionIds { get; set; } = new HashSet<string>();

        /// <summary>
        /// Gets the current estimated position accounting for elapsed time
        /// </summary>
        public long EstimatedPositionTicks
        {
            get
            {
                if (State != SyncState.Playing)
                    return PositionTicks;

                var elapsed = DateTime.UtcNow - LastUpdate;
                return PositionTicks + elapsed.Ticks;
            }
        }

        /// <summary>
        /// Gets the shareable join link for this room
        /// </summary>
        public string GetJoinLink(string serverUrl)
        {
            // Use query-style hash to avoid Emby's router interpreting it as a route
            return $"{serverUrl}/web/#syncwatch-join={Id}";
        }
    }
}
