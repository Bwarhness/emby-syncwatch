using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace EmbyPluginSyncWatch
{
    /// <summary>
    /// Handles injection of the SyncWatch script into the Emby web interface.
    /// This allows the plugin to work without Emby Premiere's custom JS feature.
    /// </summary>
    public class ScriptInjector
    {
        private readonly IApplicationPaths _appPaths;
        private readonly ILogger _logger;
        
        /// <summary>
        /// Script tag to inject into the web interface
        /// </summary>
        private const string ScriptTag = "<script src=\"/emby/web/configurationpages?name=syncwatch.js\"></script>";
        
        /// <summary>
        /// Marker comment to identify our injection
        /// </summary>
        private const string InjectionMarker = "<!-- SyncWatch Auto-Inject -->";

        public ScriptInjector(IApplicationPaths appPaths, ILogger logger)
        {
            _appPaths = appPaths;
            _logger = logger;
        }

        /// <summary>
        /// Inject the SyncWatch script into the web interface.
        /// </summary>
        /// <returns>True if injection was successful or already present, false on error</returns>
        public bool InjectScript()
        {
            try
            {
                var indexPath = FindIndexHtml();
                if (string.IsNullOrEmpty(indexPath))
                {
                    _logger.Warn("[SyncWatch] Could not locate index.html for script injection");
                    return false;
                }

                _logger.Info($"[SyncWatch] Found web interface at: {indexPath}");
                return InjectIntoFile(indexPath);
            }
            catch (Exception ex)
            {
                _logger.Error($"[SyncWatch] Script injection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove the injected script from the web interface.
        /// </summary>
        /// <returns>True if removal was successful, false on error</returns>
        public bool RemoveScript()
        {
            try
            {
                var indexPath = FindIndexHtml();
                if (string.IsNullOrEmpty(indexPath))
                {
                    return true; // Nothing to remove
                }

                return RemoveFromFile(indexPath);
            }
            catch (Exception ex)
            {
                _logger.Error($"[SyncWatch] Script removal failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Find the index.html file in the web interface directory.
        /// </summary>
        private string FindIndexHtml()
        {
            // Try common Emby web paths
            var possiblePaths = new[]
            {
                // Standard Emby paths
                Path.Combine(_appPaths.ProgramSystemPath, "dashboard-ui", "index.html"),
                Path.Combine(_appPaths.ProgramSystemPath, "system", "dashboard-ui", "index.html"),
                
                // Alternative paths for different installations
                Path.Combine(Path.GetDirectoryName(_appPaths.ProgramSystemPath) ?? "", "dashboard-ui", "index.html"),
                
                // Docker/container paths
                "/app/emby/dashboard-ui/index.html",
                "/opt/emby-server/dashboard-ui/index.html",
                "/opt/emby-server/system/dashboard-ui/index.html",
                
                // Linux package paths  
                "/usr/lib/emby-server/dashboard-ui/index.html",
                "/usr/lib/emby-server/system/dashboard-ui/index.html",
                
                // Windows paths
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), 
                    "Emby Server", "dashboard-ui", "index.html"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Emby Server", "system", "dashboard-ui", "index.html")
            };

            foreach (var path in possiblePaths)
            {
                _logger.Debug($"[SyncWatch] Checking path: {path}");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Try to find via the application root
            var appRoot = Path.GetDirectoryName(_appPaths.ProgramSystemPath);
            if (!string.IsNullOrEmpty(appRoot))
            {
                _logger.Debug($"[SyncWatch] Searching from app root: {appRoot}");
                return SearchForIndexHtml(appRoot);
            }

            return null;
        }

        /// <summary>
        /// Recursively search for index.html in dashboard-ui folders
        /// </summary>
        private string SearchForIndexHtml(string rootPath, int depth = 0)
        {
            if (depth > 3) return null; // Don't search too deep
            
            try
            {
                var directories = Directory.GetDirectories(rootPath);
                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    
                    if (dirName.Equals("dashboard-ui", StringComparison.OrdinalIgnoreCase))
                    {
                        var indexPath = Path.Combine(dir, "index.html");
                        if (File.Exists(indexPath))
                        {
                            return indexPath;
                        }
                    }
                    
                    // Recurse into system directories
                    if (dirName.Equals("system", StringComparison.OrdinalIgnoreCase))
                    {
                        var result = SearchForIndexHtml(dir, depth + 1);
                        if (result != null) return result;
                    }
                }
            }
            catch
            {
                // Ignore permission errors
            }

            return null;
        }

        /// <summary>
        /// Inject the script tag into the specified index.html file.
        /// </summary>
        private bool InjectIntoFile(string indexPath)
        {
            var contents = File.ReadAllText(indexPath);

            // Check if already injected
            if (contents.Contains(InjectionMarker))
            {
                _logger.Info("[SyncWatch] Script already injected, skipping");
                return true;
            }

            // Also check for the script tag itself (in case marker was removed)
            if (contents.Contains("syncwatch.js", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info("[SyncWatch] SyncWatch script tag already present");
                return true;
            }

            // Create backup
            var backupPath = indexPath + ".syncwatch-backup";
            if (!File.Exists(backupPath))
            {
                _logger.Info($"[SyncWatch] Creating backup at: {backupPath}");
                File.Copy(indexPath, backupPath);
            }

            // Inject before </head>
            var injection = $"{InjectionMarker}\n    {ScriptTag}\n    ";
            var headEndRegex = new Regex(@"</head>", RegexOptions.IgnoreCase);
            
            if (!headEndRegex.IsMatch(contents))
            {
                _logger.Error("[SyncWatch] Could not find </head> tag in index.html");
                return false;
            }

            contents = headEndRegex.Replace(contents, injection + "</head>", 1);

            File.WriteAllText(indexPath, contents);
            _logger.Info("[SyncWatch] Script successfully injected into web interface");
            
            return true;
        }

        /// <summary>
        /// Remove the injected script from the specified index.html file.
        /// </summary>
        private bool RemoveFromFile(string indexPath)
        {
            var contents = File.ReadAllText(indexPath);

            if (!contents.Contains(InjectionMarker))
            {
                _logger.Debug("[SyncWatch] No injection marker found, nothing to remove");
                return true;
            }

            // Remove our injection (marker + script tag + whitespace)
            var removalPattern = new Regex(
                @"\s*" + Regex.Escape(InjectionMarker) + @"\s*" + 
                Regex.Escape(ScriptTag) + @"\s*",
                RegexOptions.IgnoreCase);

            contents = removalPattern.Replace(contents, "");

            File.WriteAllText(indexPath, contents);
            _logger.Info("[SyncWatch] Script injection removed from web interface");
            
            return true;
        }
    }
}
