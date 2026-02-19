namespace EmbyPluginSyncWatch.Api.Dto
{
    /// <summary>
    /// Current sync status for a session
    /// </summary>
    public class SyncStatusDto
    {
        /// <summary>Whether the session is in a room</summary>
        public bool InRoom { get; set; }

        /// <summary>Current room information (null if not in room)</summary>
        public RoomDto Room { get; set; }
    }
}
