using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace EmbyPluginSyncWatch.Api
{
    [Route("/SyncWatch/Test", "GET", Summary = "Simple test")]
    public class GetTest : IReturn<string> { }

    /// <summary>
    /// Minimal test service with IRequiresRequest and Authenticated
    /// </summary>
    [Authenticated]
    public class TestService : IService, IRequiresRequest
    {
        public IRequest Request { get; set; }

        public object Get(GetTest request)
        {
            return "Test OK - IRequiresRequest works!";
        }
    }
}
