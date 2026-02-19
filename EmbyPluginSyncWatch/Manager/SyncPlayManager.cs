using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Session;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyPluginSyncWatch.Manager
{
    /// <summary>
    /// Core manager for synchronized playback across rooms
    /// </summary>
    public class SyncPlayManager
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger _logger;

        // Room storage: RoomId -> SyncRoom
        private readonly ConcurrentDictionary<string, SyncRoom> _rooms = new ConcurrentDictionary<string, SyncRoom>();

        // Session to Room mapping: SessionId -> RoomId
        private readonly ConcurrentDictionary<string, string> _sessionToRoom = new ConcurrentDictionary<string, string>();

        // Flag to prevent echo when we send commands
        private volatile bool _isProcessingRemoteCommand = false;

        // Minimum seek threshold in ticks (2 seconds)
        private static readonly long SeekThresholdTicks = TimeSpan.FromSeconds(2).Ticks;

        public SyncPlayManager(ISessionManager sessionManager, ILogger logger)
        {
            _sessionManager = sessionManager;
            _logger = logger;
        }

        #region Room Management

        /// <summary>
        /// Creates a new sync room
        /// </summary>
        public SyncRoom CreateRoom(string sessionId, string userId, string roomName)
        {
            // Leave any existing room first
            LeaveRoom(sessionId);

            var room = new SyncRoom
            {
                Name = string.IsNullOrWhiteSpace(roomName) ? "Watch Party" : roomName,
                OwnerId = sessionId,
                OwnerUserId = userId
            };
            room.MemberSessionIds.Add(sessionId);

            _rooms[room.Id] = room;
            _sessionToRoom[sessionId] = room.Id;

            _logger.Info($"[SyncWatch] Room '{room.Name}' (ID: {room.Id}) created by session {sessionId}");
            return room;
        }

        /// <summary>
        /// Joins an existing room
        /// </summary>
        public SyncRoom JoinRoom(string sessionId, string roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                _logger.Warn($"[SyncWatch] Room {roomId} not found for join request");
                return null;
            }

            // Leave any existing room first
            LeaveRoom(sessionId);

            room.MemberSessionIds.Add(sessionId);
            _sessionToRoom[sessionId] = roomId;

            _logger.Info($"[SyncWatch] Session {sessionId} joined room {room.Name} ({room.Id})");

            // Sync new member to current state
            _ = SyncSessionToRoomAsync(sessionId, room);

            return room;
        }

        /// <summary>
        /// Leaves the current room
        /// </summary>
        public void LeaveRoom(string sessionId)
        {
            if (!_sessionToRoom.TryRemove(sessionId, out var roomId))
                return;

            if (_rooms.TryGetValue(roomId, out var room))
            {
                room.MemberSessionIds.Remove(sessionId);
                room.ReadySessionIds.Remove(sessionId);

                _logger.Info($"[SyncWatch] Session {sessionId} left room {room.Name} ({room.Id})");

                // Delete room if empty
                if (room.MemberSessionIds.Count == 0)
                {
                    _rooms.TryRemove(roomId, out _);
                    _logger.Info($"[SyncWatch] Room {room.Name} ({room.Id}) deleted (empty)");
                }
            }
        }

        /// <summary>
        /// Gets all active rooms
        /// </summary>
        public SyncRoom[] GetRooms() => _rooms.Values.ToArray();

        /// <summary>
        /// Gets a specific room by ID
        /// </summary>
        public SyncRoom GetRoom(string roomId)
        {
            _rooms.TryGetValue(roomId, out var room);
            return room;
        }

        /// <summary>
        /// Gets the room for a specific session
        /// </summary>
        public SyncRoom GetRoomForSession(string sessionId)
        {
            if (_sessionToRoom.TryGetValue(sessionId, out var roomId))
            {
                _rooms.TryGetValue(roomId, out var room);
                return room;
            }
            return null;
        }

        /// <summary>
        /// Cleans up stale rooms
        /// </summary>
        public void CleanupStaleRooms(int timeoutMinutes)
        {
            var threshold = DateTime.UtcNow.AddMinutes(-timeoutMinutes);
            var staleRooms = _rooms.Values
                .Where(r => r.MemberSessionIds.Count == 0 && r.CreatedAt < threshold)
                .ToList();

            foreach (var room in staleRooms)
            {
                if (_rooms.TryRemove(room.Id, out _))
                {
                    _logger.Info($"[SyncWatch] Cleaned up stale room {room.Name} ({room.Id})");
                }
            }
        }

        #endregion

        #region Playback Event Handlers

        /// <summary>
        /// Handles playback start events
        /// </summary>
        public void HandlePlaybackStart(SessionInfo session, PlaybackProgressEventArgs e)
        {
            if (_isProcessingRemoteCommand) return;
            if (e == null) return;

            var room = GetRoomForSession(session.Id);
            if (room == null) return;

            // Get item ID from session's now playing item
            var itemId = session.NowPlayingItem?.InternalId ?? 0;
            if (itemId == 0) return;

            _logger.Debug($"[SyncWatch] PlaybackStart from {session.Id} in room {room.Id}: Item={itemId}");

            room.CurrentItemId = itemId;
            room.PositionTicks = e.PlaybackPositionTicks ?? 0;
            room.State = SyncState.Playing;
            room.LastUpdate = DateTime.UtcNow;

            // Broadcast to other members
            _ = BroadcastPlayToItemAsync(room, session.Id);
        }

        /// <summary>
        /// Handles playback progress events
        /// </summary>
        public void HandlePlaybackProgress(SessionInfo session, PlaybackProgressEventArgs e)
        {
            if (_isProcessingRemoteCommand) return;
            if (e == null) return;

            var room = GetRoomForSession(session.Id);
            if (room == null) return;

            var isPaused = e.IsPaused;
            var newPosition = e.PlaybackPositionTicks ?? room.PositionTicks;

            // Detect pause
            if (isPaused && room.State == SyncState.Playing)
            {
                room.State = SyncState.Paused;
                room.PositionTicks = newPosition;
                room.LastUpdate = DateTime.UtcNow;

                _logger.Debug($"[SyncWatch] Pause detected from {session.Id}");
                _ = BroadcastPauseAsync(room, session.Id);
                return;
            }

            // Detect unpause
            if (!isPaused && room.State == SyncState.Paused)
            {
                room.State = SyncState.Playing;
                room.PositionTicks = newPosition;
                room.LastUpdate = DateTime.UtcNow;

                _logger.Debug($"[SyncWatch] Unpause detected from {session.Id}");
                _ = BroadcastUnpauseAsync(room, session.Id);
                return;
            }

            // Detect seek (position jump > threshold)
            var positionDiff = Math.Abs(newPosition - room.EstimatedPositionTicks);
            if (positionDiff > SeekThresholdTicks && room.State == SyncState.Playing)
            {
                room.PositionTicks = newPosition;
                room.LastUpdate = DateTime.UtcNow;

                _logger.Debug($"[SyncWatch] Seek detected from {session.Id}: diff={TimeSpan.FromTicks(positionDiff).TotalSeconds:F1}s");
                _ = BroadcastSeekAsync(room, session.Id);
            }
        }

        /// <summary>
        /// Handles playback stopped events
        /// </summary>
        public void HandlePlaybackStopped(SessionInfo session)
        {
            var room = GetRoomForSession(session.Id);
            if (room == null) return;

            room.ReadySessionIds.Remove(session.Id);
            _logger.Debug($"[SyncWatch] Playback stopped for {session.Id} in room {room.Id}");
        }

        #endregion

        #region Command Broadcasting

        private async Task BroadcastPlayToItemAsync(SyncRoom room, string excludeSessionId)
        {
            if (room.CurrentItemId == 0) return;

            _isProcessingRemoteCommand = true;
            try
            {
                foreach (var sessionId in room.MemberSessionIds.ToArray())
                {
                    if (sessionId == excludeSessionId) continue;

                    try
                    {
                        await _sessionManager.SendPlayCommand(
                            null,
                            sessionId,
                            new PlayRequest
                            {
                                ItemIds = new long[] { room.CurrentItemId },
                                StartPositionTicks = room.PositionTicks,
                                PlayCommand = PlayCommand.PlayNow
                            },
                            CancellationToken.None
                        );

                        _logger.Debug($"[SyncWatch] Sent PlayNow to {sessionId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[SyncWatch] Failed to send play command to {sessionId}: {ex.Message}");
                    }
                }
            }
            finally
            {
                await Task.Delay(200);
                _isProcessingRemoteCommand = false;
            }
        }

        private async Task BroadcastPauseAsync(SyncRoom room, string excludeSessionId)
        {
            await BroadcastPlaystateCommandAsync(room, excludeSessionId, new PlaystateRequest
            {
                Command = PlaystateCommand.Pause
            });
        }

        private async Task BroadcastUnpauseAsync(SyncRoom room, string excludeSessionId)
        {
            await BroadcastPlaystateCommandAsync(room, excludeSessionId, new PlaystateRequest
            {
                Command = PlaystateCommand.Unpause
            });
        }

        private async Task BroadcastSeekAsync(SyncRoom room, string excludeSessionId)
        {
            await BroadcastPlaystateCommandAsync(room, excludeSessionId, new PlaystateRequest
            {
                Command = PlaystateCommand.Seek,
                SeekPositionTicks = room.PositionTicks
            });
        }

        private async Task BroadcastPlaystateCommandAsync(SyncRoom room, string excludeSessionId, PlaystateRequest command)
        {
            _isProcessingRemoteCommand = true;
            try
            {
                foreach (var sessionId in room.MemberSessionIds.ToArray())
                {
                    if (sessionId == excludeSessionId) continue;

                    try
                    {
                        await _sessionManager.SendPlaystateCommand(
                            null,
                            sessionId,
                            command,
                            CancellationToken.None
                        );

                        _logger.Debug($"[SyncWatch] Sent {command.Command} to {sessionId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[SyncWatch] Failed to send {command.Command} to {sessionId}: {ex.Message}");
                    }
                }
            }
            finally
            {
                await Task.Delay(200);
                _isProcessingRemoteCommand = false;
            }
        }

        private async Task SyncSessionToRoomAsync(string sessionId, SyncRoom room)
        {
            if (room.State == SyncState.Idle || room.CurrentItemId == 0)
                return;

            _isProcessingRemoteCommand = true;
            try
            {
                // Send play command with current item and position
                await _sessionManager.SendPlayCommand(
                    null,
                    sessionId,
                    new PlayRequest
                    {
                        ItemIds = new long[] { room.CurrentItemId },
                        StartPositionTicks = room.EstimatedPositionTicks,
                        PlayCommand = PlayCommand.PlayNow
                    },
                    CancellationToken.None
                );

                _logger.Info($"[SyncWatch] Synced {sessionId} to room state: Item={room.CurrentItemId}, Position={TimeSpan.FromTicks(room.EstimatedPositionTicks)}");

                // If room is paused, pause the new member after playback starts
                if (room.State == SyncState.Paused)
                {
                    await Task.Delay(1000); // Wait for playback to initialize
                    await _sessionManager.SendPlaystateCommand(
                        null,
                        sessionId,
                        new PlaystateRequest { Command = PlaystateCommand.Pause },
                        CancellationToken.None
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[SyncWatch] Failed to sync session {sessionId}: {ex.Message}");
            }
            finally
            {
                await Task.Delay(200);
                _isProcessingRemoteCommand = false;
            }
        }

        #endregion
    }
}
