using MediaBrowser.Model.Plugins;

namespace EmbyPluginSyncWatch
{
    /// <summary>
    /// Plugin configuration settings
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Maximum allowed position drift in seconds before forcing a resync
        /// </summary>
        public int MaxDriftSeconds { get; set; } = 3;

        /// <summary>
        /// Whether to automatically inject the SyncWatch UI script into web clients
        /// </summary>
        public bool AutoInjectScript { get; set; } = true;

        /// <summary>
        /// Maximum number of rooms allowed
        /// </summary>
        public int MaxRooms { get; set; } = 50;

        /// <summary>
        /// Maximum members per room
        /// </summary>
        public int MaxMembersPerRoom { get; set; } = 20;

        /// <summary>
        /// Room timeout in minutes (auto-delete empty rooms after this time)
        /// </summary>
        public int RoomTimeoutMinutes { get; set; } = 60;

        public PluginConfiguration()
        {
            // Defaults set above
        }
    }
}
