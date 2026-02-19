using MediaBrowser.Model.Services;

namespace EmbyPluginSyncWatch.Api
{
    [Route("/SyncWatch/Test", "GET", Summary = "Simple test")]
    public class GetTest : IReturn<string> { }

    /// <summary>
    /// Minimal test service - no dependencies, no auth
    /// </summary>
    public class TestService : IService
    {
        public object Get(GetTest request)
        {
            return "Test OK!";
        }
    }
}
