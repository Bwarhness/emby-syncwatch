/**
 * SyncWatch - Browser UI for synchronized playback
 * Injects a floating "Sync" button on video pages
 */
(function() {
    'use strict';

    const PLUGIN_NAME = 'SyncWatch';
    const API_BASE = '/emby/SyncWatch';
    
    let currentRoom = null;
    let overlayElement = null;
    let floatingButton = null;
    let pollInterval = null;
    let isOverlayVisible = false;

    // ========================================
    // API Functions
    // ========================================

    function getHeaders() {
        let token = null;
        if (window.ApiClient) {
            // Try different methods to get the token
            if (typeof ApiClient.accessToken === 'function') {
                token = ApiClient.accessToken();
            } else if (ApiClient._serverInfo && ApiClient._serverInfo.AccessToken) {
                token = ApiClient._serverInfo.AccessToken;
            } else if (ApiClient.serverInfo && ApiClient.serverInfo().AccessToken) {
                token = ApiClient.serverInfo().AccessToken;
            }
        }
        console.log('[SyncWatch] Token:', token ? 'found' : 'not found');
        return {
            'X-Emby-Token': token,
            'Content-Type': 'application/json'
        };
    }

    async function apiCall(endpoint, method = 'GET', body = null) {
        const options = {
            method,
            headers: getHeaders()
        };
        if (body) {
            options.body = JSON.stringify(body);
        }
        const response = await fetch(`${API_BASE}${endpoint}`, options);
        if (!response.ok) {
            throw new Error(`API error: ${response.status}`);
        }
        const text = await response.text();
        return text ? JSON.parse(text) : null;
    }

    async function fetchRooms() {
        return await apiCall('/Rooms');
    }

    async function fetchStatus() {
        return await apiCall('/Status');
    }

    async function createRoom(name) {
        return await apiCall('/Rooms', 'POST', { Name: name });
    }

    async function joinRoom(roomId) {
        return await apiCall(`/Rooms/${roomId}/Join`, 'POST');
    }

    async function leaveRoom() {
        await apiCall('/Rooms/Leave', 'POST');
        currentRoom = null;
    }

    // ========================================
    // UI Creation
    // ========================================

    function createStyles() {
        if (document.getElementById('syncwatch-styles')) return;

        const styles = document.createElement('style');
        styles.id = 'syncwatch-styles';
        styles.textContent = `
            #syncwatch-button {
                position: fixed;
                bottom: 100px;
                right: 20px;
                width: 50px;
                height: 50px;
                border-radius: 50%;
                background: linear-gradient(135deg, #00a4dc, #0077b5);
                border: none;
                color: white;
                font-size: 24px;
                cursor: pointer;
                z-index: 999998;
                box-shadow: 0 4px 15px rgba(0, 164, 220, 0.4);
                transition: transform 0.2s, box-shadow 0.2s;
                display: flex;
                align-items: center;
                justify-content: center;
            }

            #syncwatch-button:hover {
                transform: scale(1.1);
                box-shadow: 0 6px 20px rgba(0, 164, 220, 0.6);
            }

            #syncwatch-button.in-room {
                background: linear-gradient(135deg, #4caf50, #2e7d32);
                box-shadow: 0 4px 15px rgba(76, 175, 80, 0.4);
                animation: pulse 2s infinite;
            }

            @keyframes pulse {
                0%, 100% { box-shadow: 0 4px 15px rgba(76, 175, 80, 0.4); }
                50% { box-shadow: 0 4px 25px rgba(76, 175, 80, 0.8); }
            }

            #syncwatch-overlay {
                position: fixed;
                bottom: 160px;
                right: 20px;
                background: rgba(20, 20, 25, 0.95);
                color: #fff;
                padding: 20px;
                border-radius: 12px;
                z-index: 999999;
                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                min-width: 280px;
                max-width: 350px;
                box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5);
                border: 1px solid rgba(255, 255, 255, 0.1);
                backdrop-filter: blur(10px);
            }

            #syncwatch-overlay h3 {
                margin: 0 0 15px 0;
                font-size: 18px;
                display: flex;
                align-items: center;
                gap: 8px;
            }

            #syncwatch-overlay .close-btn {
                position: absolute;
                top: 10px;
                right: 12px;
                background: none;
                border: none;
                color: #888;
                font-size: 20px;
                cursor: pointer;
                padding: 5px;
            }

            #syncwatch-overlay .close-btn:hover {
                color: #fff;
            }

            #syncwatch-overlay button.primary {
                background: linear-gradient(135deg, #00a4dc, #0077b5);
                border: none;
                color: white;
                padding: 10px 20px;
                border-radius: 6px;
                cursor: pointer;
                font-size: 14px;
                font-weight: 500;
                width: 100%;
                margin: 5px 0;
                transition: opacity 0.2s;
            }

            #syncwatch-overlay button.primary:hover {
                opacity: 0.9;
            }

            #syncwatch-overlay button.secondary {
                background: rgba(255, 255, 255, 0.1);
                border: 1px solid rgba(255, 255, 255, 0.2);
                color: white;
                padding: 10px 20px;
                border-radius: 6px;
                cursor: pointer;
                font-size: 14px;
                width: 100%;
                margin: 5px 0;
                transition: background 0.2s;
            }

            #syncwatch-overlay button.secondary:hover {
                background: rgba(255, 255, 255, 0.2);
            }

            #syncwatch-overlay button.danger {
                background: rgba(244, 67, 54, 0.2);
                border: 1px solid rgba(244, 67, 54, 0.4);
                color: #f44336;
            }

            #syncwatch-overlay button.danger:hover {
                background: rgba(244, 67, 54, 0.3);
            }

            #syncwatch-overlay .room-list {
                max-height: 200px;
                overflow-y: auto;
                margin: 10px 0;
            }

            #syncwatch-overlay .room-item {
                padding: 12px;
                margin: 8px 0;
                background: rgba(255, 255, 255, 0.05);
                border-radius: 8px;
                cursor: pointer;
                transition: background 0.2s;
                display: flex;
                justify-content: space-between;
                align-items: center;
            }

            #syncwatch-overlay .room-item:hover {
                background: rgba(255, 255, 255, 0.1);
            }

            #syncwatch-overlay .room-item .room-name {
                font-weight: 500;
            }

            #syncwatch-overlay .room-item .room-meta {
                font-size: 12px;
                color: #888;
            }

            #syncwatch-overlay .status-bar {
                padding: 10px;
                margin-top: 10px;
                background: rgba(76, 175, 80, 0.1);
                border: 1px solid rgba(76, 175, 80, 0.3);
                border-radius: 6px;
                font-size: 13px;
                display: flex;
                align-items: center;
                gap: 8px;
            }

            #syncwatch-overlay .status-bar.idle {
                background: rgba(255, 255, 255, 0.05);
                border-color: rgba(255, 255, 255, 0.1);
                color: #888;
            }

            #syncwatch-overlay .copy-link {
                display: flex;
                gap: 8px;
                margin: 10px 0;
            }

            #syncwatch-overlay .copy-link input {
                flex: 1;
                background: rgba(0, 0, 0, 0.3);
                border: 1px solid rgba(255, 255, 255, 0.2);
                border-radius: 4px;
                padding: 8px;
                color: white;
                font-size: 12px;
            }

            #syncwatch-overlay .copy-link button {
                padding: 8px 12px;
                background: rgba(255, 255, 255, 0.1);
                border: 1px solid rgba(255, 255, 255, 0.2);
                border-radius: 4px;
                color: white;
                cursor: pointer;
            }

            #syncwatch-overlay .member-count {
                display: inline-flex;
                align-items: center;
                gap: 4px;
                padding: 4px 8px;
                background: rgba(0, 164, 220, 0.2);
                border-radius: 12px;
                font-size: 12px;
            }

            #syncwatch-overlay .divider {
                height: 1px;
                background: rgba(255, 255, 255, 0.1);
                margin: 15px 0;
            }
        `;
        document.head.appendChild(styles);
    }

    function createFloatingButton() {
        if (floatingButton) return;

        floatingButton = document.createElement('button');
        floatingButton.id = 'syncwatch-button';
        floatingButton.innerHTML = '👥';
        floatingButton.title = 'SyncWatch - Watch together';
        floatingButton.onclick = toggleOverlay;
        document.body.appendChild(floatingButton);
    }

    function createOverlay() {
        if (overlayElement) return;

        overlayElement = document.createElement('div');
        overlayElement.id = 'syncwatch-overlay';
        overlayElement.style.display = 'none';
        document.body.appendChild(overlayElement);
    }

    function toggleOverlay() {
        if (!overlayElement) createOverlay();

        isOverlayVisible = !isOverlayVisible;
        overlayElement.style.display = isOverlayVisible ? 'block' : 'none';

        if (isOverlayVisible) {
            updateUI();
        }
    }

    function hideOverlay() {
        isOverlayVisible = false;
        if (overlayElement) {
            overlayElement.style.display = 'none';
        }
    }

    // ========================================
    // UI Updates
    // ========================================

    async function updateUI() {
        if (!overlayElement) return;

        try {
            const status = await fetchStatus();
            currentRoom = status.InRoom ? status.Room : null;

            // Update button state
            if (floatingButton) {
                floatingButton.classList.toggle('in-room', currentRoom !== null);
            }

            if (currentRoom) {
                renderInRoomUI();
            } else {
                await renderLobbyUI();
            }
        } catch (error) {
            console.error('[SyncWatch] Error updating UI:', error);
            overlayElement.innerHTML = `
                <h3>🎬 SyncWatch</h3>
                <button class="close-btn" onclick="SyncWatch.hide()">×</button>
                <div style="color: #f44336; padding: 10px;">
                    Error loading sync status. Please try again.
                </div>
                <button class="secondary" onclick="SyncWatch.refresh()">Retry</button>
            `;
        }
    }

    function renderInRoomUI() {
        const stateEmoji = {
            'Idle': '⏸️',
            'Waiting': '⏳',
            'Playing': '▶️',
            'Paused': '⏸️'
        };

        overlayElement.innerHTML = `
            <h3>🎬 SyncWatch</h3>
            <button class="close-btn" onclick="SyncWatch.hide()">×</button>
            
            <div style="margin-bottom: 15px;">
                <div style="font-size: 16px; font-weight: 500;">${escapeHtml(currentRoom.Name)}</div>
                <div class="member-count">
                    👥 ${currentRoom.MemberCount} member${currentRoom.MemberCount !== 1 ? 's' : ''}
                </div>
            </div>

            <div class="status-bar ${currentRoom.State === 'Idle' ? 'idle' : ''}">
                <span>${stateEmoji[currentRoom.State] || '❓'}</span>
                <span>${currentRoom.State === 'Idle' ? 'Waiting for playback' : 'Playback synced'}</span>
            </div>

            <div class="divider"></div>

            <div style="margin-bottom: 10px; font-size: 13px; color: #888;">
                Share this link to invite others:
            </div>
            <div class="copy-link">
                <input type="text" id="syncwatch-link" value="${currentRoom.JoinLink}" readonly>
                <button onclick="SyncWatch.copyLink()">📋</button>
            </div>

            <div class="divider"></div>

            <button class="secondary danger" onclick="SyncWatch.leave()">Leave Room</button>
        `;
    }

    async function renderLobbyUI() {
        let roomsHtml = '';
        
        try {
            const rooms = await fetchRooms();
            
            if (rooms && rooms.length > 0) {
                roomsHtml = `
                    <div style="margin-bottom: 10px; font-size: 13px; color: #888;">
                        Active rooms:
                    </div>
                    <div class="room-list">
                        ${rooms.map(r => `
                            <div class="room-item" onclick="SyncWatch.join('${r.Id}')">
                                <div>
                                    <div class="room-name">${escapeHtml(r.Name)}</div>
                                    <div class="room-meta">${r.State} • ${r.MemberCount} member${r.MemberCount !== 1 ? 's' : ''}</div>
                                </div>
                                <span>→</span>
                            </div>
                        `).join('')}
                    </div>
                    <div class="divider"></div>
                `;
            }
        } catch (e) {
            console.warn('[SyncWatch] Could not load rooms:', e);
        }

        overlayElement.innerHTML = `
            <h3>🎬 SyncWatch</h3>
            <button class="close-btn" onclick="SyncWatch.hide()">×</button>
            
            ${roomsHtml}

            <button class="primary" onclick="SyncWatch.create()">
                ✨ Create New Room
            </button>

            <div class="status-bar idle">
                <span>💡</span>
                <span>Create a room and share the link to watch together!</span>
            </div>
        `;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }

    // ========================================
    // Public API
    // ========================================

    window.SyncWatch = {
        async create() {
            const name = prompt('Enter room name:', 'Watch Party');
            if (!name) return;

            try {
                currentRoom = await createRoom(name);
                updateUI();
            } catch (error) {
                console.error('[SyncWatch] Error creating room:', error);
                alert('Failed to create room. Please try again.');
            }
        },

        async join(roomId, retries = 3) {
            try {
                currentRoom = await joinRoom(roomId);
                // Show the overlay after successful join
                if (!isOverlayVisible) {
                    toggleOverlay();
                } else {
                    updateUI();
                }
            } catch (error) {
                console.error('[SyncWatch] Error joining room:', error);
                
                // Retry on auth errors (user might not be fully logged in yet)
                if (retries > 0 && (error.message.includes('401') || error.message.includes('403'))) {
                    console.log('[SyncWatch] Auth not ready, retrying in 2s...', retries - 1, 'left');
                    setTimeout(() => this.join(roomId, retries - 1), 2000);
                    return;
                }
                
                alert('Failed to join room. It may no longer exist.');
            }
        },

        async leave() {
            try {
                await leaveRoom();
                updateUI();
            } catch (error) {
                console.error('[SyncWatch] Error leaving room:', error);
            }
        },

        copyLink() {
            const input = document.getElementById('syncwatch-link');
            if (input) {
                input.select();
                document.execCommand('copy');
                
                // Visual feedback
                const btn = input.nextElementSibling;
                const original = btn.textContent;
                btn.textContent = '✓';
                setTimeout(() => btn.textContent = original, 1500);
            }
        },

        toggle() {
            toggleOverlay();
        },

        hide() {
            hideOverlay();
        },

        refresh() {
            updateUI();
        },

        // Auto-join from URL (persists intent through login flow)
        checkJoinLink() {
            // Check URL for join link
            let match = window.location.hash.match(/syncwatch-join=([a-f0-9]+)/i);
            if (!match) {
                match = window.location.hash.match(/syncwatch\/join\/([a-f0-9]+)/i);
            }
            
            if (match) {
                const roomId = match[1];
                console.log('[SyncWatch] Join link detected in URL, room:', roomId);
                // Store intent in sessionStorage (survives login redirect)
                sessionStorage.setItem('syncwatch-join', roomId);
                // Clean URL
                history.replaceState(null, '', window.location.pathname + '#');
            }
            
            // Check sessionStorage for pending join
            const pendingRoom = sessionStorage.getItem('syncwatch-join');
            if (pendingRoom) {
                console.log('[SyncWatch] Pending join from sessionStorage:', pendingRoom);
                sessionStorage.removeItem('syncwatch-join');
                this.join(pendingRoom);
            }
        }
    };

    // ========================================
    // Initialization
    // ========================================

    function init() {
        console.log('[SyncWatch] Initializing...');
        
        createStyles();
        createFloatingButton();
        createOverlay();

        // Check for join link in URL
        SyncWatch.checkJoinLink();

        // Poll for status updates when in room
        pollInterval = setInterval(async () => {
            if (currentRoom && isOverlayVisible) {
                await updateUI();
            }
        }, 5000);

        // Listen for hash changes (join links)
        window.addEventListener('hashchange', () => {
            SyncWatch.checkJoinLink();
        });

        console.log('[SyncWatch] Initialized successfully');
    }

    // Wait for DOM and ApiClient
    function waitForReady() {
        if (document.body && window.ApiClient) {
            init();
        } else {
            setTimeout(waitForReady, 500);
        }
    }

    waitForReady();

})();
