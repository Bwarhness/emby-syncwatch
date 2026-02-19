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

    #endregion

    /// <summary>
    /// REST API service for SyncWatch functionality
    /// </summary>
    [Authenticated]
    public class SyncWatchService : IService, IRequiresRequest
    {
        public IRequest Request { get; set; }

        private readonly ISessionManager _sessionManager;
        private readonly IAuthorizationContext _authContext;

        public SyncWatchService(ISessionManager sessionManager, IAuthorizationContext authContext)
        {
            _sessionManager = sessionManager;
            _authContext = authContext;
        }

        private SyncPlayManager SyncManager => SyncWatchEntryPoint.SyncManager;

        private (string sessionId, string userId) GetSessionInfo()
        {
            var authInfo = _authContext.GetAuthorizationInfo(Request);
            
            // Find session by device ID or user ID
            var sessions = _sessionManager.Sessions;
            
            // Handle potential type differences between AuthorizationInfo and SessionInfo
            // by converting to strings for comparison
            var authDeviceId = Convert.ToString(authInfo.DeviceId);
            var authUserId = Convert.ToString(authInfo.UserId);
            
            var session = sessions.FirstOrDefault(s => 
                s.DeviceId == authDeviceId || 
                s.UserId.ToString() == authUserId);
            
            return (session?.Id ?? authDeviceId ?? "", authUserId ?? "");
        }

        private string GetServerUrl()
        {
            // Build server URL from request
            var scheme = Request.IsSecureConnection ? "https" : "http";
            return $"{scheme}://{Request.Headers["Host"]}";
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
            var (sessionId, _) = GetSessionInfo();
            var room = SyncManager.GetRoomForSession(sessionId);

            return new SyncStatusDto
            {
                InRoom = room != null,
                Room = room != null ? MapRoomToDto(room, sessionId, GetServerUrl()) : null
            };
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
