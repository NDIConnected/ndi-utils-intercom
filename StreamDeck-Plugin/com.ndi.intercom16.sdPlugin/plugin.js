// Global WebSocket connection to Stream Deck
var websocket = null;
var pluginUUID = null;

// API Configuration
var API_BASE = 'http://localhost:5016/api/intercom';

// SignalR Configuration
var signalRConnection = null;
var signalRConnected = false;

// Action UUIDs
var ACTION_TALK = 'com.ndi.intercom16.talk';
var ACTION_LISTEN = 'com.ndi.intercom16.listen';
var ACTION_SET_GROUP = 'com.ndi.intercom16.setgroup';
var ACTION_ROTATE_GROUP = 'com.ndi.intercom16.rotategroup';
var ACTION_RESET = 'com.ndi.intercom16.reset';

// Store contexts and settings
var contextSettings = {};

/**
 * Connect to Stream Deck WebSocket
 */
function connectElgatoStreamDeckSocket(inPort, inPluginUUID, inRegisterEvent, inInfo) {
    pluginUUID = inPluginUUID;

    console.log('[NDI Intercom] Connecting to Stream Deck...');

    websocket = new WebSocket('ws://127.0.0.1:' + inPort);

    websocket.onopen = function() {
        console.log('[NDI Intercom] WebSocket connected');

        // Register plugin
        var json = {
            event: inRegisterEvent,
            uuid: inPluginUUID
        };
        websocket.send(JSON.stringify(json));
    };

    websocket.onerror = function(error) {
        console.error('[NDI Intercom] WebSocket error:', error);
    };

    websocket.onclose = function() {
        console.log('[NDI Intercom] WebSocket closed');
    };

    websocket.onmessage = function(evt) {
        try {
            var jsonObj = JSON.parse(evt.data);
            handleStreamDeckEvent(jsonObj);
        } catch (error) {
            console.error('[NDI Intercom] Error parsing message:', error);
        }
    };
}

/**
 * Initialize SignalR connection for real-time updates
 */
function initializeSignalR() {
    // Extract base URL from API_BASE (remove /api/intercom)
    var hubUrl = API_BASE.replace('/api/intercom', '/intercomHub');

    console.log('[NDI Intercom] Initializing SignalR connection to: ' + hubUrl);

    try {
        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Handle ChannelUpdated event from server
        signalRConnection.on('ChannelUpdated', function(channelData) {
            console.log('[NDI Intercom] SignalR: Channel updated:', channelData.channelNumber);

            // Update all buttons for this channel
            for (var context in contextSettings) {
                var ctxData = contextSettings[context];
                var channel = parseInt(ctxData.settings.channel);

                if (channel === channelData.channelNumber) {
                    updateButtonFromChannelData(context, channelData, ctxData.action);
                }
            }
        });

        // Handle connection state changes
        signalRConnection.onreconnecting(function(error) {
            console.log('[NDI Intercom] SignalR: Reconnecting...', error);
            signalRConnected = false;
        });

        signalRConnection.onreconnected(function(connectionId) {
            console.log('[NDI Intercom] SignalR: Reconnected successfully');
            signalRConnected = true;
        });

        signalRConnection.onclose(function(error) {
            console.log('[NDI Intercom] SignalR: Connection closed', error);
            signalRConnected = false;

            // Try to reconnect after 5 seconds
            setTimeout(function() {
                console.log('[NDI Intercom] SignalR: Attempting to reconnect...');
                startSignalR();
            }, 5000);
        });

        // Start the connection
        startSignalR();

    } catch (error) {
        console.error('[NDI Intercom] Error initializing SignalR:', error);
    }
}

/**
 * Start SignalR connection
 */
function startSignalR() {
    if (signalRConnection) {
        signalRConnection.start()
            .then(function() {
                console.log('[NDI Intercom] SignalR: Connected successfully');
                signalRConnected = true;
            })
            .catch(function(error) {
                console.error('[NDI Intercom] SignalR: Connection failed:', error);
                signalRConnected = false;
            });
    }
}

/**
 * Handle Stream Deck events
 */
function handleStreamDeckEvent(jsonObj) {
    var event = jsonObj.event;
    var action = jsonObj.action;
    var context = jsonObj.context;
    var settings = {};

    if (jsonObj.payload && jsonObj.payload.settings) {
        settings = jsonObj.payload.settings;
    }

    console.log('[NDI Intercom] Event: ' + event + ', Action: ' + action);

    // Store settings for this context
    if (context) {
        contextSettings[context] = { action: action, settings: settings };
    }

    if (event === 'keyDown') {
        handleKeyDown(context, settings, action);
    } else if (event === 'willAppear') {
        handleWillAppear(context, settings, action);
    } else if (event === 'willDisappear') {
        delete contextSettings[context];
    } else if (event === 'didReceiveSettings') {
        contextSettings[context] = { action: action, settings: settings };
        var channel = parseInt(settings.channel) || 1;
        var serverUrl = (settings.serverUrl || API_BASE).trim();

        // Update from server for dynamic labels
        if (action === ACTION_TALK || action === ACTION_LISTEN || action === ACTION_ROTATE_GROUP) {
            updateButtonState(context, channel, serverUrl);
        } else {
            updateButtonTitle(context, settings, action);
        }
    } else if (event === 'propertyInspectorDidAppear') {
        console.log('[NDI Intercom] Property Inspector opened');
    } else if (event === 'propertyInspectorDidDisappear') {
        console.log('[NDI Intercom] Property Inspector closed');
    }
}

/**
 * Make HTTP request using XMLHttpRequest
 */
function makeRequest(method, url, body, onSuccess, onError) {
    var xhr = new XMLHttpRequest();

    xhr.onreadystatechange = function() {
        if (xhr.readyState === 4) {
            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    var data = xhr.responseText ? JSON.parse(xhr.responseText) : null;
                    if (onSuccess) onSuccess(data);
                } catch (e) {
                    console.error('[NDI Intercom] Error parsing response:', e);
                    if (onError) onError(e);
                }
            } else {
                console.error('[NDI Intercom] API error: ' + xhr.status + ' ' + xhr.statusText);
                if (onError) onError(new Error('HTTP ' + xhr.status + ': ' + xhr.statusText));
            }
        }
    };

    xhr.onerror = function() {
        console.error('[NDI Intercom] Network error');
        if (onError) onError(new Error('Network error'));
    };

    try {
        xhr.open(method, url, true);
        if (body) {
            xhr.setRequestHeader('Content-Type', 'application/json');
            xhr.send(JSON.stringify(body));
        } else {
            xhr.send();
        }
    } catch (e) {
        console.error('[NDI Intercom] Error making request:', e);
        if (onError) onError(e);
    }
}

/**
 * Handle button press
 */
function handleKeyDown(context, settings, action) {
    var channel = parseInt(settings.channel) || 1;
    var serverUrl = (settings.serverUrl || API_BASE).trim();

    console.log('[NDI Intercom] Key pressed: ' + action + ', Channel: ' + channel + ', Server: ' + serverUrl);

    if (action === ACTION_TALK) {
        makeRequest('POST', serverUrl + '/channels/' + channel + '/talk/toggle', null,
            function() {
                console.log('[NDI Intercom] TALK toggled for channel ' + channel);
                updateButtonState(context, channel, serverUrl);
            },
            function(error) {
                console.error('[NDI Intercom] Error toggling TALK:', error);
                showAlert(context);
            }
        );
    } else if (action === ACTION_LISTEN) {
        makeRequest('POST', serverUrl + '/channels/' + channel + '/listen/toggle', null,
            function() {
                console.log('[NDI Intercom] LISTEN toggled for channel ' + channel);
                updateButtonState(context, channel, serverUrl);
            },
            function(error) {
                console.error('[NDI Intercom] Error toggling LISTEN:', error);
                showAlert(context);
            }
        );
    } else if (action === ACTION_SET_GROUP) {
        var group = parseInt(settings.group) || 0;
        makeRequest('POST', serverUrl + '/channels/' + channel + '/group', { group: group },
            function() {
                console.log('[NDI Intercom] Group ' + group + ' set for channel ' + channel);
                showOk(context);
            },
            function(error) {
                console.error('[NDI Intercom] Error setting group:', error);
                showAlert(context);
            }
        );
    } else if (action === ACTION_ROTATE_GROUP) {
        makeRequest('POST', serverUrl + '/channels/' + channel + '/group/rotate', null,
            function(data) {
                console.log('[NDI Intercom] Group rotated to ' + data.group + ' for channel ' + channel);
                updateButtonState(context, channel, serverUrl);
            },
            function(error) {
                console.error('[NDI Intercom] Error rotating group:', error);
                showAlert(context);
            }
        );
    } else if (action === ACTION_RESET) {
        makeRequest('POST', serverUrl + '/reset', null,
            function() {
                console.log('[NDI Intercom] All channels reset');
                showOk(context);
                // Update all buttons
                for (var ctx in contextSettings) {
                    var ctxData = contextSettings[ctx];
                    if (ctxData.settings.channel) {
                        updateButtonState(ctx, ctxData.settings.channel, serverUrl);
                    }
                }
            },
            function(error) {
                console.error('[NDI Intercom] Error resetting channels:', error);
                showAlert(context);
            }
        );
    }
}

/**
 * Handle button appearance
 */
function handleWillAppear(context, settings, action) {
    var channel = parseInt(settings.channel) || 1;
    var serverUrl = (settings.serverUrl || API_BASE).trim();

    console.log('[NDI Intercom] Button appeared: ' + action + ', Channel: ' + channel + ', Server: ' + serverUrl);

    // Update state and title from server (dynamic labels)
    if (action === ACTION_TALK || action === ACTION_LISTEN || action === ACTION_ROTATE_GROUP) {
        updateButtonState(context, channel, serverUrl);
    } else {
        // Only for other actions (SET_GROUP, RESET) use static title
        updateButtonTitle(context, settings, action);
    }
}

/**
 * Update button state from server
 */
function updateButtonState(context, channel, serverUrl) {
    makeRequest('GET', serverUrl + '/channels/' + channel, null,
        function(channelData) {
            var ctxData = contextSettings[context];
            if (!ctxData) return;

            var action = ctxData.action;
            var title = '';
            var label = channelData.label || ('Ch ' + channel);

            if (action === ACTION_TALK) {
                title = label + '\nTALK';
                // Set button state (color) based on talkEnabled
                setState(context, channelData.talkEnabled ? 1 : 0);
            } else if (action === ACTION_LISTEN) {
                title = label + '\nLISTEN';
                // Set button state (color) based on listenEnabled
                setState(context, channelData.listenEnabled ? 1 : 0);
            } else if (action === ACTION_ROTATE_GROUP) {
                var groupNames = ['NONE', 'GRP 1', 'GRP 2', 'GRP 3', 'GRP 4'];
                title = label + '\n' + groupNames[channelData.intercomGroup];
            }

            if (title) {
                setTitle(context, title);
            }
        },
        function(error) {
            console.error('[NDI Intercom] Error updating button state:', error);
        }
    );
}

/**
 * Update button from SignalR channel data
 */
function updateButtonFromChannelData(context, channelData, action) {
    var title = '';
    var label = channelData.label || ('Ch ' + channelData.channelNumber);

    if (action === ACTION_TALK) {
        title = label + '\nTALK';
        // Set button state (color) based on talkEnabled
        setState(context, channelData.talkEnabled ? 1 : 0);
    } else if (action === ACTION_LISTEN) {
        title = label + '\nLISTEN';
        // Set button state (color) based on listenEnabled
        setState(context, channelData.listenEnabled ? 1 : 0);
    } else if (action === ACTION_ROTATE_GROUP) {
        var groupNames = ['NONE', 'GRP 1', 'GRP 2', 'GRP 3', 'GRP 4'];
        title = label + '\n' + groupNames[channelData.intercomGroup];
    }

    if (title) {
        setTitle(context, title);
    }
}

/**
 * Update button title based on settings
 */
function updateButtonTitle(context, settings, action) {
    var channel = parseInt(settings.channel) || 1;
    var title = '';

    if (action === ACTION_TALK) {
        title = 'TALK ' + channel;
    } else if (action === ACTION_LISTEN) {
        title = 'LISTEN ' + channel;
    } else if (action === ACTION_SET_GROUP) {
        var group = parseInt(settings.group) || 0;
        title = 'SET\nGRP ' + group + '\nCH ' + channel;
    } else if (action === ACTION_ROTATE_GROUP) {
        title = 'ROTATE\nGRP\nCH ' + channel;
    } else if (action === ACTION_RESET) {
        title = 'RESET\nALL';
    }

    if (title) {
        setTitle(context, title);
    }
}

/**
 * Set button title
 */
function setTitle(context, title) {
    if (websocket && websocket.readyState === 1) {
        var json = {
            event: 'setTitle',
            context: context,
            payload: {
                title: title,
                target: 0
            }
        };
        websocket.send(JSON.stringify(json));
    }
}

/**
 * Set button state (for color change)
 */
function setState(context, state) {
    if (websocket && websocket.readyState === 1) {
        var json = {
            event: 'setState',
            context: context,
            payload: {
                state: state
            }
        };
        websocket.send(JSON.stringify(json));
    }
}

/**
 * Show OK checkmark
 */
function showOk(context) {
    if (websocket && websocket.readyState === 1) {
        var json = {
            event: 'showOk',
            context: context
        };
        websocket.send(JSON.stringify(json));
    }
}

/**
 * Show alert
 */
function showAlert(context) {
    if (websocket && websocket.readyState === 1) {
        var json = {
            event: 'showAlert',
            context: context
        };
        websocket.send(JSON.stringify(json));
    }
}

// Initialize SignalR for real-time updates
initializeSignalR();

// Start periodic state update (every 5 seconds as fallback if SignalR is not connected)
setInterval(function() {
    // Only poll if SignalR is not connected
    if (!signalRConnected) {
        console.log('[NDI Intercom] Polling (SignalR not connected)');
        for (var context in contextSettings) {
            var ctxData = contextSettings[context];
            var channel = parseInt(ctxData.settings.channel);
            var serverUrl = (ctxData.settings.serverUrl || API_BASE).trim();

            if (channel && (ctxData.action === ACTION_TALK || ctxData.action === ACTION_LISTEN || ctxData.action === ACTION_ROTATE_GROUP)) {
                updateButtonState(context, channel, serverUrl);
            }
        }
    }
}, 5000);

console.log('[NDI Intercom] Plugin loaded');
