namespace EmbyPluginSyncWatch.Api.Dto
{
    /// <summary>
    /// Request to create a new sync room
    /// </summary>
    public class CreateRoomRequest
    {
        /// <summary>Optional room name (defaults to "Watch Party")</summary>
        public string Name { get; set; }
    }
}
