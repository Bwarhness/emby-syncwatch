# EmbyPluginSyncWatch

**Watch together with friends** - A synchronized playback plugin for Emby Server.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![Emby](https://img.shields.io/badge/Emby-4.7+-green)
![.NET](https://img.shields.io/badge/.NET-Standard%202.0-purple)

## Features

- 🎬 **Synchronized Playback** - Play, pause, and seek stay in sync across all room members
- 👥 **Easy Room Management** - Create rooms and share links with friends
- 🔗 **Shareable Links** - One-click join links like `/web/#/syncwatch/join/abc123`
- 🎨 **Floating UI** - Non-intrusive button overlay on video pages
- ⚡ **Real-time Sync** - Automatic position correction when drift is detected

## Screenshots

The plugin adds a floating "👥" button to the Emby web interface. Clicking it opens the SyncWatch panel where you can:

1. Create a new watch room
2. See and join existing rooms
3. Copy a shareable link to invite friends
4. View sync status

## Installation

### Prerequisites

- Emby Server 4.7 or later
- .NET SDK 6.0+ (for building)

### Building from Source

```bash
# Clone or download the repository
cd EmbyPluginSyncWatch

# Restore dependencies and build
dotnet build -c Release

# The plugin DLL will be at:
# EmbyPluginSyncWatch/bin/Release/netstandard2.0/EmbyPluginSyncWatch.dll
```

### Installing the Plugin

1. **Build the plugin** (see above)

2. **Copy the DLL** to your Emby plugins folder:
   - **Windows:** `%AppData%\Emby-Server\programdata\plugins\`
   - **Linux:** `~/.config/emby-server/plugins/`
   - **Docker:** Mount to `/config/plugins/`

3. **Restart Emby Server**

4. **Enable the plugin** in Emby Dashboard → Plugins

### Adding the UI Script

The floating button needs to be loaded in the web client. Choose one method:

#### Method 1: Custom Script (Recommended)

In Emby Dashboard → Settings → Custom CSS/JavaScript, add:

```html
<script src="/emby/web/configurationpages?name=syncwatch.js"></script>
```

#### Method 2: Auto-inject (if supported)

Enable "Auto-inject UI script" in the plugin settings.

## Usage

### Creating a Room

1. Click the floating **👥** button on any video page
2. Click **"Create New Room"**
3. Enter a room name (or use the default "Watch Party")
4. Share the generated link with friends

### Joining a Room

**Option A: Via Link**
- Click the shareable link (e.g., `https://your-server/web/#/syncwatch/join/abc123`)
- You'll automatically join the room

**Option B: Via Room List**
- Click the **👥** button
- Select an existing room from the list

### Watching Together

Once everyone is in the room:
1. The room owner starts playing a video
2. All members automatically start playing the same video at the same position
3. **Play/Pause** - synced across all members
4. **Seek** - synced across all members
5. **New members** - automatically synced to current position

### Leaving a Room

Click **"Leave Room"** in the SyncWatch panel.

## API Endpoints

The plugin exposes these REST endpoints:

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/SyncWatch/Rooms` | List all active rooms |
| POST | `/SyncWatch/Rooms` | Create a new room |
| GET | `/SyncWatch/Rooms/{id}` | Get room details |
| POST | `/SyncWatch/Rooms/{id}/Join` | Join a room |
| POST | `/SyncWatch/Rooms/Leave` | Leave current room |
| GET | `/SyncWatch/Status` | Get current sync status |

All endpoints require authentication (`X-Emby-Token` header).

## Configuration

Access plugin settings via Emby Dashboard → Plugins → SyncWatch.

| Setting | Default | Description |
|---------|---------|-------------|
| Max Drift | 3 sec | Position difference before forcing resync |
| Max Rooms | 50 | Maximum concurrent rooms |
| Max Members | 20 | Maximum members per room |
| Room Timeout | 60 min | Auto-delete empty rooms after this time |
| Auto-inject | true | Automatically add UI script to web client |

## Technical Details

### How Sync Works

1. Plugin subscribes to `ISessionManager` playback events
2. When a room member plays/pauses/seeks, the event is captured
3. Plugin broadcasts corresponding commands to other room members via `SendPlaystateCommand()`
4. A small delay prevents command echo

### Architecture

```
┌────────────────────────────────────────────┐
│              Browser Client                │
│  ┌──────────────────────────────────────┐  │
│  │         syncwatch.js                 │  │
│  │  - Floating UI button                │  │
│  │  - Room management                   │  │
│  │  - Calls REST API                    │  │
│  └──────────────────────────────────────┘  │
└────────────────────────────────────────────┘
                    │
                    ▼
┌────────────────────────────────────────────┐
│              Emby Server                   │
│  ┌──────────────────────────────────────┐  │
│  │      SyncWatchService (API)          │  │
│  │  /SyncWatch/Rooms/*                  │  │
│  └──────────────────────────────────────┘  │
│                    │                       │
│                    ▼                       │
│  ┌──────────────────────────────────────┐  │
│  │         SyncPlayManager              │  │
│  │  - Room state management             │  │
│  │  - Playback event handling           │  │
│  │  - Command broadcasting              │  │
│  └──────────────────────────────────────┘  │
│                    │                       │
│                    ▼                       │
│  ┌──────────────────────────────────────┐  │
│  │  ISessionManager (Emby Core)         │  │
│  │  - SendPlaystateCommand()            │  │
│  │  - PlaybackStart/Progress/Stopped    │  │
│  └──────────────────────────────────────┘  │
└────────────────────────────────────────────┘
```

## Troubleshooting

### Floating button doesn't appear

1. Ensure the plugin is installed and enabled
2. Check that the JS script is loaded (see "Adding the UI Script")
3. Check browser console for errors
4. Try hard-refreshing the page (Ctrl+Shift+R)

### Sync is not working

1. Verify all users are in the same room
2. Check that users are authenticated with Emby
3. Look at Emby server logs for SyncWatch errors
4. Ensure firewalls allow WebSocket connections

### Room commands fail

1. Check that the API endpoints are accessible
2. Verify authentication token is valid
3. Check server logs for permission issues

## Known Limitations

- Browser clients only (native apps not supported yet)
- Single video sync (no playlist support yet)
- No latency compensation (users on slow connections may drift)
- Rooms are in-memory only (lost on server restart)

## Contributing

Contributions welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

MIT License - See LICENSE file for details.

## Credits

- Built for [Emby Media Server](https://emby.media)
- Inspired by Syncplay and Jellyfin SyncPlay
