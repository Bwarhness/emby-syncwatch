using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Services;
using EmbyPluginSyncWatch.Api.Dto;
using EmbyPluginSyncWatch.EntryPoint;
using EmbyPluginSyncWatch.Manager;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmbyPluginSyncWatch.Api
{
    #region Request Types

    [Route("/SyncWatch/Rooms", "GET", Summary = "Get all active sync rooms")]
    public class GetRooms : IReturn<List<RoomDto>> { }

    [Route("/SyncWatch/Rooms", "POST", Summary = "Create a new sync room")]
    public class CreateRoom : IReturn<RoomDto>
    {
        [ApiMember(Name = "Name", Description = "Room name", IsRequired = false)]
        public string Name { get; set; }
    }

    [Route("/SyncWatch/Rooms/{RoomId}", "GET", Summary = "Get a specific room")]
    public class GetRoom : IReturn<RoomDto>
    {
        [ApiMember(Name = "RoomId", Description = "Room ID", IsRequired = true)]
        public string RoomId { get; set; }
    }

    [Route("/SyncWatch/Rooms/{RoomId}/Join", "POST", Summary = "Join a sync room")]
    public class JoinRoom : IReturn<RoomDto>
    {
        [ApiMember(Name = "RoomId", Description = "Room ID to join", IsRequired = true)]
        public string RoomId { get; set; }
    }

    [Route("/SyncWatch/Rooms/Leave", "POST", Summary = "Leave the current sync room")]
    public class LeaveRoom : IReturn<bool> { }

    [Route("/SyncWatch/Status", "GET", Summary = "Get current sync status")]
    public class GetSyncStatus : IReturn<SyncStatusDto> { }

    [Route("/SyncWatch/Ping", "GET", Summary = "Simple ping test")]
    [Unauthenticated]
    public class GetPing : IReturn<string> { }

    #endregion

    /// <summary>
    /// REST API service for SyncWatch functionality
    /// </summary>
    [Authenticated]
    public class SyncWatchService : IService, IRequiresRequest
    {
        public IRequest Request { get; set; }

        // No constructor dependencies - use static/service locator pattern instead
        public SyncWatchService()
        {
            Plugin.Logger?.Info("[SyncWatch] SyncWatchService constructor called");
        }
        
        private ISessionManager SessionManager => SyncWatchEntryPoint.SessionManager;
        private IAuthorizationContext AuthContext => SyncWatchEntryPoint.AuthContext;

        private SyncPlayManager SyncManager 
        {
            get
            {
                var manager = SyncWatchEntryPoint.SyncManager;
                if (manager == null)
                {
                    throw new InvalidOperationException("SyncWatch plugin is still initializing. Please try again in a moment.");
                }
                return manager;
            }
        }

        private (string sessionId, string userId) GetSessionInfo()
        {
            // Step 1: Get auth info
            var authInfo = AuthContext.GetAuthorizationInfo(Request);
            if (authInfo == null)
            {
                throw new Exception("AuthContext.GetAuthorizationInfo returned null");
            }
            
            var authDeviceId = authInfo.DeviceId.ToString();
            var authUserId = authInfo.UserId.ToString();
            
            // Step 2: Get sessions
            var sessions = SessionManager.Sessions;
            if (sessions == null)
            {
                return (authDeviceId, authUserId);
            }
            
            // Step 3: Find matching session (simplified)
            string sessionId = authDeviceId;
            try
            {
                foreach (var s in sessions)
                {
                    if (s?.DeviceId == authDeviceId)
                    {
                        sessionId = s.Id ?? authDeviceId;
                        break;
                    }
                }
            }
            catch
            {
                // If session enumeration fails, just use device ID
            }
            
            return (sessionId, authUserId);
        }

        private string GetServerUrl()
        {
            // Build server URL from request
            var scheme = Request.IsSecureConnection ? "https" : "http";
            return $"{scheme}://{Request.Headers["Host"]}";
        }

        /// <summary>
        /// GET /SyncWatch/Ping - Simple test endpoint with diagnostics
        /// </summary>
        public object Get(GetPing request)
        {
            var diag = new System.Text.StringBuilder();
            diag.AppendLine("SyncWatch Diagnostics:");
            diag.AppendLine($"- SessionManager: {(SessionManager != null ? "OK" : "NULL")}");
            diag.AppendLine($"- AuthContext: {(AuthContext != null ? "OK" : "NULL")}");
            diag.AppendLine($"- SyncManager: {(SyncWatchEntryPoint.SyncManager != null ? "OK" : "NULL")}");
            diag.AppendLine($"- Request: {(Request != null ? "OK" : "NULL")}");
            try
            {
                var authInfo = AuthContext?.GetAuthorizationInfo(Request);
                diag.AppendLine($"- AuthInfo: {(authInfo != null ? $"DeviceId={authInfo.DeviceId}, UserId={authInfo.UserId}" : "NULL")}");
            }
            catch (Exception ex)
            {
                diag.AppendLine($"- AuthInfo ERROR: {ex.Message}");
            }
            return diag.ToString();
        }

        /// <summary>
        /// GET /SyncWatch/Rooms - List all rooms
        /// </summary>
        public object Get(GetRooms request)
        {
            var (sessionId, userId) = GetSessionInfo();
            var serverUrl = GetServerUrl();

            return SyncManager.GetRooms()
                .Select(r => MapRoomToDto(r, sessionId, serverUrl))
                .ToList();
        }

        /// <summary>
        /// GET /SyncWatch/Rooms/{RoomId} - Get specific room
        /// </summary>
        public object Get(GetRoom request)
        {
            var (sessionId, _) = GetSessionInfo();
            var room = SyncManager.GetRoom(request.RoomId);

            if (room == null)
            {
                throw new Exception($"Room '{request.RoomId}' not found");
            }

            return MapRoomToDto(room, sessionId, GetServerUrl());
        }

        /// <summary>
        /// POST /SyncWatch/Rooms - Create new room
        /// </summary>
        public object Post(CreateRoom request)
        {
            var (sessionId, userId) = GetSessionInfo();
            var room = SyncManager.CreateRoom(sessionId, userId, request.Name);

            return MapRoomToDto(room, sessionId, GetServerUrl());
        }

        /// <summary>
        /// POST /SyncWatch/Rooms/{RoomId}/Join - Join a room
        /// </summary>
        public object Post(JoinRoom request)
        {
            var (sessionId, _) = GetSessionInfo();
            var room = SyncManager.JoinRoom(sessionId, request.RoomId);

            if (room == null)
            {
                throw new Exception($"Room '{request.RoomId}' not found");
            }

            return MapRoomToDto(room, sessionId, GetServerUrl());
        }

        /// <summary>
        /// POST /SyncWatch/Rooms/Leave - Leave current room
        /// </summary>
        public object Post(LeaveRoom request)
        {
            var (sessionId, _) = GetSessionInfo();
            SyncManager.LeaveRoom(sessionId);
            return true;
        }

        /// <summary>
        /// GET /SyncWatch/Status - Get current sync status
        /// </summary>
        public object Get(GetSyncStatus request)
        {
            try
            {
                // Step 1: Get session info
                string sessionId;
                try
                {
                    var info = GetSessionInfo();
                    sessionId = info.sessionId;
                }
                catch (Exception ex)
                {
                    return new { error = $"GetSessionInfo failed: {ex.Message}" };
                }

                // Step 2: Get room
                SyncRoom room;
                try
                {
                    room = SyncManager.GetRoomForSession(sessionId);
                }
                catch (Exception ex)
                {
                    return new { error = $"GetRoomForSession failed: {ex.Message}" };
                }

                // Step 3: Build response
                try
                {
                    return new SyncStatusDto
                    {
                        InRoom = room != null,
                        Room = room != null ? MapRoomToDto(room, sessionId, GetServerUrl()) : null
                    };
                }
                catch (Exception ex)
                {
                    return new { error = $"Building response failed: {ex.Message}" };
                }
            }
            catch (Exception ex)
            {
                return new { error = $"Unexpected error: {ex.Message}", stack = ex.StackTrace };
            }
        }

        private RoomDto MapRoomToDto(SyncRoom room, string sessionId, string serverUrl)
        {
            return new RoomDto
            {
                Id = room.Id,
                Name = room.Name,
                MemberCount = room.MemberSessionIds.Count,
                State = room.State.ToString(),
                CurrentItemId = room.CurrentItemId,
                PositionTicks = room.EstimatedPositionTicks,
                JoinLink = room.GetJoinLink(serverUrl),
                IsOwner = room.OwnerId == sessionId,
                Members = room.MemberSessionIds.ToList(),
                CreatedAt = room.CreatedAt
            };
        }
    }
}
