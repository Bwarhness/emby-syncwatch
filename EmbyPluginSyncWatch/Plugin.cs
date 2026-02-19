using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;

namespace EmbyPluginSyncWatch
{
    /// <summary>
    /// Main plugin class for SyncWatch - synchronized playback for Emby
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public override string Name => "SyncWatch";
        
        public override Guid Id => new Guid("f8e12a3b-c456-7890-def1-234567890abc");
        
        public override string Description => "Watch together with friends - synchronized playback across browser clients";

        public static Plugin Instance { get; private set; }
        
        /// <summary>
        /// Static logger instance for use by services without direct DI access
        /// </summary>
        public static ILogger Logger { get; set; }

        public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer) 
            : base(appPaths, xmlSerializer)
        {
            Instance = this;
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "syncwatchconfig",
                    EmbeddedResourcePath = GetType().Namespace + ".Web.configPage.html"
                },
                new PluginPageInfo
                {
                    Name = "syncwatch.js",
                    EmbeddedResourcePath = GetType().Namespace + ".Web.syncwatch.js"
                }
            };
        }
    }
}
