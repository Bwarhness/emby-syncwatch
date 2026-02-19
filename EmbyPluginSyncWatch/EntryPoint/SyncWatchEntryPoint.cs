using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using EmbyPluginSyncWatch.Manager;
using System;
using System.Threading;

namespace EmbyPluginSyncWatch.EntryPoint
{
    /// <summary>
    /// Server entry point that initializes the SyncWatch plugin
    /// </summary>
    public class SyncWatchEntryPoint : IServerEntryPoint
    {
        private readonly ISessionManager _sessionManager;
        private readonly IApplicationPaths _appPaths;
        private readonly ILogger _logger;
        private Timer _cleanupTimer;
        private ScriptInjector _scriptInjector;

        /// <summary>
        /// Singleton instance of the sync manager, accessible for API service
        /// </summary>
        public static SyncPlayManager SyncManager { get; private set; }

        public SyncWatchEntryPoint(
            ISessionManager sessionManager,
            IApplicationPaths appPaths,
            ILogManager logManager)
        {
            _sessionManager = sessionManager;
            _appPaths = appPaths;
            _logger = logManager.GetLogger("SyncWatch");
            Plugin.Logger = _logger; // Set static logger for services
            SyncManager = new SyncPlayManager(sessionManager, _logger);
            _scriptInjector = new ScriptInjector(appPaths, _logger);
        }

        public void Run()
        {
            _logger.Info("[SyncWatch] Plugin starting...");

            // Auto-inject script if enabled
            var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
            if (config.AutoInjectScript)
            {
                _logger.Info("[SyncWatch] Auto-inject is enabled, attempting script injection...");
                if (_scriptInjector.InjectScript())
                {
                    _logger.Info("[SyncWatch] Script injection completed");
                }
                else
                {
                    _logger.Warn("[SyncWatch] Script injection failed - manual installation may be required");
                    _logger.Info("[SyncWatch] See plugin config page for manual installation instructions");
                }
            }
            else
            {
                _logger.Info("[SyncWatch] Auto-inject is disabled, skipping script injection");
            }

            // Subscribe to playback events
            _sessionManager.PlaybackStart += OnPlaybackStart;
            _sessionManager.PlaybackProgress += OnPlaybackProgress;
            _sessionManager.PlaybackStopped += OnPlaybackStopped;
            _sessionManager.SessionEnded += OnSessionEnded;

            // Start cleanup timer (every 5 minutes)
            _cleanupTimer = new Timer(
                _ => SyncManager.CleanupStaleRooms(config.RoomTimeoutMinutes),
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5)
            );

            _logger.Info("[SyncWatch] Plugin started successfully");
        }

        private void OnPlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            if (e?.Session == null) return;

            try
            {
                SyncManager.HandlePlaybackStart(e.Session, e);
            }
            catch (Exception ex)
            {
                _logger.Error($"[SyncWatch] Error handling PlaybackStart: {ex.Message}");
            }
        }

        private void OnPlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            if (e?.Session == null) return;

            try
            {
                SyncManager.HandlePlaybackProgress(e.Session, e);
            }
            catch (Exception ex)
            {
                _logger.Error($"[SyncWatch] Error handling PlaybackProgress: {ex.Message}");
            }
        }

        private void OnPlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            if (e?.Session == null) return;

            try
            {
                SyncManager.HandlePlaybackStopped(e.Session);
            }
            catch (Exception ex)
            {
                _logger.Error($"[SyncWatch] Error handling PlaybackStopped: {ex.Message}");
            }
        }

        private void OnSessionEnded(object sender, SessionEventArgs e)
        {
            if (e?.SessionInfo == null) return;

            try
            {
                // Auto-leave room when session ends
                SyncManager.LeaveRoom(e.SessionInfo.Id);
            }
            catch (Exception ex)
            {
                _logger.Error($"[SyncWatch] Error handling SessionEnded: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();

            _sessionManager.PlaybackStart -= OnPlaybackStart;
            _sessionManager.PlaybackProgress -= OnPlaybackProgress;
            _sessionManager.PlaybackStopped -= OnPlaybackStopped;
            _sessionManager.SessionEnded -= OnSessionEnded;

            _logger.Info("[SyncWatch] Plugin disposed");
        }
    }
}
