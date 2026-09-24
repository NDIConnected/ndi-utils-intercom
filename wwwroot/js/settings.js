// SignalR connection
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/intercomHub")
    .withAutomaticReconnect()
    .build();

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

let config = null;
let inputDevices = [];
let outputDevices = [];
let ndiSources = [];
let asioDevices = [];
/**
 * Channel counts of the ASIO device currently initialized in the engine. The routing
 * dropdowns used to always offer 64 inputs and 64 outputs regardless of the device, and a
 * channel routed past the real count is silently dropped by the mixer — which is
 * indistinguishable from broken routing. 0 means "unknown", in which case we fall back to
 * the widest range rather than hiding options the operator may legitimately need.
 */
let asioChannelCounts = { inputChannels: 0, outputChannels: 0 };
const ASIO_MAX_CHANNELS_FALLBACK = 64;
let bridgeConnected = false;
let currentBridgeMode = "NONE";
let selectedBridgePanel = null; // which tab is currently shown (host/join/local)
let bridgeStatusInterval = null;
let maxIntercomChannels = 16;
/** From GetProductInfo: ASIO + audio backend differ by OS. */
let productInfo = {
    maxChannels: 16,
    asioAvailable: true,
    audioBackendName: "WASAPI",
    productDisplayName: "",
    uiTitleShort: ""
};
const APPLICATION_ID_REGEX = /^[A-Za-z0-9_.-]{1,64}$/;

// Start connection
async function startConnection() {
    try {
        await connection.start();
        const product = await connection.invoke("GetProductInfo");
        productInfo = product;
        maxIntercomChannels = product.maxChannels || 16;
        productInfo = {
            maxChannels: product.maxChannels || 16,
            asioAvailable: product.asioAvailable !== false,
            audioBackendName: product.audioBackendName || "",
            productDisplayName: product.productDisplayName || "",
            uiTitleShort: product.uiTitleShort || ""
        };
        document.title = (product.productDisplayName || "Settings") + " — Settings";
        applyPlatformShell();
        await loadSettings();
    } catch (err) {
        setTimeout(startConnection, 5000);
    }
}

function injectWindowsAsioSettingsSection() {
    const mount = document.getElementById("asioSectionMount");
    if (!mount || mount.dataset.inserted === "1" || !productInfo.asioAvailable) {
        return;
    }
    mount.innerHTML =
        '<div class="settings-section" id="settingsSectionAsio">' +
        "<h2>ASIO Device</h2>" +
        '<div class="settings-group">' +
        "<label>ASIO Device:</label>" +
        '<select id="asioDeviceSelect">' +
        '<option value="">-- Select ASIO Device --</option>' +
        "</select>" +
        "</div>" +
        '<div class="settings-group">' +
        '<button type="button" class="btn-scan" id="btnScanAsioChannels">Scan Channels</button>' +
        "</div>" +
        '<div id="asioChannelInfo" style="display: none; margin-top: 10px; padding: 10px; background: rgba(52, 152, 219, 0.1); border-radius: 6px;">' +
        '<p style="margin: 0; font-size: 13px;">' +
        "<strong>Input Channels:</strong> <span id=\"asioInputCount\">0</span><br>" +
        "<strong>Output Channels:</strong> <span id=\"asioOutputCount\">0</span>" +
        "</p>" +
        "</div>" +
        "</div>";
    const btn = document.getElementById("btnScanAsioChannels");
    if (btn) {
        btn.addEventListener("click", () => scanAsioChannels());
    }
    mount.dataset.inserted = "1";
}

function applyPlatformShell() {
    const hint = document.getElementById("audioBackendHint");
    if (hint) {
        hint.textContent = productInfo.audioBackendName
            ? `Input and output lists use ${productInfo.audioBackendName}.`
            : "";
    }

    injectWindowsAsioSettingsSection();
}

// Load all settings
async function loadSettings() {
    try {
        // Load configuration
        config = await connection.invoke("GetConfiguration");

        // Load audio devices
        inputDevices = await connection.invoke("GetInputDevices");
        outputDevices = await connection.invoke("GetOutputDevices");

        // Load NDI sources
        ndiSources = await connection.invoke("GetNDISources");

        // Load ASIO devices (Windows only)
        if (productInfo.asioAvailable) {
            asioDevices = await connection.invoke("GetAsioDevices");
            await refreshAsioChannelCounts();
        } else {
            asioDevices = [];
        }

        // Populate UI
        const applicationIdInput = document.getElementById("applicationId");
        if (applicationIdInput) {
            applicationIdInput.value = (config.applicationId || "Intercom_A").trim();
        }

        populateAudioDevices();
        populateAsioDevices();
        populateNoiseGate();
        populateFeedbackGate();
        populateNDIChannels();
        
        // Load NDI Bridge status
        await loadBridgeStatus();
    } catch (err) {
        // Silently continue
    }
}

function getValidatedApplicationId() {
    const applicationIdInput = document.getElementById("applicationId");
    const applicationId = (applicationIdInput?.value || "").trim();

    if (!APPLICATION_ID_REGEX.test(applicationId)) {
        alert("Invalid Application ID. Use 1-64 chars: A-Z, a-z, 0-9, underscore (_), dot (.), hyphen (-).");
        applicationIdInput?.focus();
        return null;
    }

    return applicationId;
}

// Populate audio devices
function populateAudioDevices() {
    populateDeviceSelect(
        document.getElementById("microphoneSelect"),
        inputDevices,
        config.selectedMicrophone,
        "Saved microphone (not currently available)"
    );
    populateDeviceSelect(
        document.getElementById("speakerSelect"),
        outputDevices,
        config.selectedSpeaker,
        "Saved speaker (not currently available)"
    );
}

// Keeps the saved id selected even when that endpoint is unplugged or not yet
// enumerated. Otherwise the browser shows the first device and Apply stores it.
function populateDeviceSelect(select, devices, selectedId, unavailableLabel) {
    if (!select) {
        return;
    }

    const saved = selectedId || "";
    select.innerHTML = "";

    let matched = false;
    (devices || []).forEach(device => {
        const option = document.createElement("option");
        const id = device.deviceId || "";
        option.value = id;
        option.textContent = device.friendlyName || "Default";
        if (id === saved) {
            option.selected = true;
            matched = true;
        }
        select.appendChild(option);
    });

    if (!matched && saved) {
        const option = document.createElement("option");
        option.value = saved;
        option.textContent = unavailableLabel;
        option.selected = true;
        select.appendChild(option);
    }

    if (select.options.length === 0) {
        const opt = document.createElement("option");
        opt.value = "";
        opt.textContent = "Default";
        select.appendChild(opt);
    }
}

// Populate ASIO devices
function populateAsioDevices() {
    if (!productInfo.asioAvailable) {
        return;
    }

    const asioSelect = document.getElementById("asioDeviceSelect");

    asioSelect.innerHTML = '<option value="">-- Select ASIO Device --</option>';
    let asioMatched = !config.selectedAsioDevice;
    asioDevices.forEach(device => {
        const option = document.createElement("option");
        option.value = device;
        option.textContent = device;
        option.selected = device === config.selectedAsioDevice;
        if (option.selected) {
            asioMatched = true;
        }
        asioSelect.appendChild(option);
    });

    if (config.selectedAsioDevice && !asioMatched) {
        const option = document.createElement("option");
        option.value = config.selectedAsioDevice;
        option.textContent = config.selectedAsioDevice + " (not available)";
        option.selected = true;
        asioSelect.appendChild(option);
    }

    // Show channel info if device is already selected
    if (config.selectedAsioDevice && config.asioInputChannelCount > 0) {
        document.getElementById("asioInputCount").textContent = config.asioInputChannelCount;
        document.getElementById("asioOutputCount").textContent = config.asioOutputChannelCount;
        document.getElementById("asioChannelInfo").style.display = "block";
    }
}

/**
 * Reads the live channel counts from the engine, falling back to the values persisted in
 * the config when no device is initialized yet (e.g. the driver is still coming up).
 */
async function refreshAsioChannelCounts() {
    let inputs = 0;
    let outputs = 0;

    try {
        const counts = await connection.invoke("GetAsioChannelCounts");
        inputs = counts.inputChannels || 0;
        outputs = counts.outputChannels || 0;
    } catch (err) {
        // Engine has no ASIO device up yet; the config values are the best we have.
    }

    if (inputs <= 0) inputs = config?.asioInputChannelCount || 0;
    if (outputs <= 0) outputs = config?.asioOutputChannelCount || 0;

    asioChannelCounts = { inputChannels: inputs, outputChannels: outputs };
}

/**
 * Builds the option list for a routing dropdown. Any saved value beyond the device range is
 * still listed, flagged as unavailable, so Apply cannot silently rewrite the operator's
 * routing and the problem is visible instead.
 */
function buildAsioChannelOptions(count, selectedValue, label) {
    const limit = count > 0 ? count : ASIO_MAX_CHANNELS_FALLBACK;
    const selected = Number(selectedValue) || 0;
    const options = [];

    for (let idx = 0; idx < limit; idx++) {
        options.push(
            `<option value="${idx}" ${selected === idx ? "selected" : ""}>${label} ${idx + 1}</option>`
        );
    }

    if (selected >= limit) {
        options.push(
            `<option value="${selected}" selected>${label} ${selected + 1} (not available on this device)</option>`
        );
    }

    return options.join("");
}

// Scan ASIO channels
async function scanAsioChannels() {
    if (!productInfo.asioAvailable) {
        return;
    }

    const asioSelect = document.getElementById("asioDeviceSelect");
    const selectedDevice = asioSelect.value;

    if (!selectedDevice) {
        alert("Please select an ASIO device first");
        return;
    }

    try {
        // Initialize ASIO device
        const success = await connection.invoke("InitializeAsioDevice", selectedDevice);

        if (success) {
            // Get channel counts
            const channelCounts = await connection.invoke("GetAsioChannelCounts");

            document.getElementById("asioInputCount").textContent = channelCounts.inputChannels;
            document.getElementById("asioOutputCount").textContent = channelCounts.outputChannels;
            document.getElementById("asioChannelInfo").style.display = "block";

            // Re-render the routing dropdowns only when the ranges actually changed, so a
            // scan on an unchanged device does not discard pending edits below.
            const rangesChanged =
                (channelCounts.inputChannels || 0) !== asioChannelCounts.inputChannels ||
                (channelCounts.outputChannels || 0) !== asioChannelCounts.outputChannels;

            asioChannelCounts = {
                inputChannels: channelCounts.inputChannels || 0,
                outputChannels: channelCounts.outputChannels || 0
            };

            if (config) {
                config.selectedAsioDevice = selectedDevice;
                config.asioInputChannelCount = asioChannelCounts.inputChannels;
                config.asioOutputChannelCount = asioChannelCounts.outputChannels;
            }

            if (rangesChanged) {
                populateNDIChannels();
            }

            alert(`ASIO Device initialized!\n\nInput Channels: ${channelCounts.inputChannels}\nOutput Channels: ${channelCounts.outputChannels}`);
        } else {
            alert("Failed to initialize ASIO device");
        }
    } catch (err) {
        alert("ASIO Error: " + err.message);
    }
}

// Populate microphone noise gate settings
function populateNoiseGate() {
    const enableNoiseGate = document.getElementById("enableNoiseGate");
    const noiseGateThreshold = document.getElementById("noiseGateThreshold");
    const noiseGateAttack = document.getElementById("noiseGateAttack");
    const noiseGateRelease = document.getElementById("noiseGateRelease");
    const noiseGateHold = document.getElementById("noiseGateHold");

    const noiseThresholdValue = document.getElementById("noiseThresholdValue");
    const noiseAttackValue = document.getElementById("noiseAttackValue");
    const noiseReleaseValue = document.getElementById("noiseReleaseValue");
    const noiseHoldValue = document.getElementById("noiseHoldValue");

    enableNoiseGate.checked = config.noiseGateEnabled || false;
    noiseGateThreshold.value = config.noiseGateThresholdDb ?? -45;
    noiseGateAttack.value = config.noiseGateAttackMs ?? 1;
    noiseGateRelease.value = config.noiseGateReleaseMs ?? 100;
    noiseGateHold.value = config.noiseGateHoldMs ?? 50;

    noiseThresholdValue.textContent = noiseGateThreshold.value + " dB";
    noiseAttackValue.textContent = noiseGateAttack.value + " ms";
    noiseReleaseValue.textContent = noiseGateRelease.value + " ms";
    noiseHoldValue.textContent = noiseGateHold.value + " ms";

    noiseGateThreshold.addEventListener("input", (e) => {
        noiseThresholdValue.textContent = e.target.value + " dB";
    });

    noiseGateAttack.addEventListener("input", (e) => {
        noiseAttackValue.textContent = e.target.value + " ms";
    });

    noiseGateRelease.addEventListener("input", (e) => {
        noiseReleaseValue.textContent = e.target.value + " ms";
    });

    noiseGateHold.addEventListener("input", (e) => {
        noiseHoldValue.textContent = e.target.value + " ms";
    });
}

// Populate channels feedback gate settings
function populateFeedbackGate() {
    const enableFeedbackGate = document.getElementById("enableFeedbackGate");
    const feedbackGateThreshold = document.getElementById("feedbackGateThreshold");
    const feedbackGateAttack = document.getElementById("feedbackGateAttack");
    const feedbackGateRelease = document.getElementById("feedbackGateRelease");

    const feedbackThresholdValue = document.getElementById("feedbackThresholdValue");
    const feedbackAttackValue = document.getElementById("feedbackAttackValue");
    const feedbackReleaseValue = document.getElementById("feedbackReleaseValue");

    enableFeedbackGate.checked = config.feedbackGateEnabled || false;
    feedbackGateThreshold.value = config.gateThresholdDb ?? -40;
    feedbackGateAttack.value = config.gateAttackMs ?? 0;
    feedbackGateRelease.value = config.gateReleaseMs ?? 200;

    feedbackThresholdValue.textContent = feedbackGateThreshold.value + " dB";
    feedbackAttackValue.textContent = feedbackGateAttack.value + " ms";
    feedbackReleaseValue.textContent = feedbackGateRelease.value + " ms";

    feedbackGateThreshold.addEventListener("input", (e) => {
        feedbackThresholdValue.textContent = e.target.value + " dB";
    });

    feedbackGateAttack.addEventListener("input", (e) => {
        feedbackAttackValue.textContent = e.target.value + " ms";
    });

    feedbackGateRelease.addEventListener("input", (e) => {
        feedbackReleaseValue.textContent = e.target.value + " ms";
    });
}

// Populate NDI channels
function populateNDIChannels() {
    const grid = document.getElementById("ndiChannelsGrid");
    grid.innerHTML = "";

    for (let i = 1; i <= maxIntercomChannels; i++) {
        const channelConfig = config.channels && config.channels[i - 1]
            ? config.channels[i - 1]
            : {
                label: `Channel ${i}`,
                ndiSendName: `Channel ${i}`,
                ndiReceiveName: "",
                mode: 0, // NDI by default
                asioInputChannel: 0,
                asioOutputChannel: 0
            };

        const channelMode = channelConfig.mode || 0; // 0 = NDI, 1 = ASIO
        const savedReceive = channelConfig.ndiReceiveName || "";
        const receiveListed = ndiSources.some(source => source === savedReceive);
        const missingReceiveOption = savedReceive && !receiveListed
            ? `<option value="${escapeHtml(savedReceive)}" selected>${escapeHtml(savedReceive)} (not currently visible)</option>`
            : "";

        const channelDiv = document.createElement("div");
        channelDiv.className = "ndi-channel";

        const modeBlock = productInfo.asioAvailable
            ? `
            <label>Mode:</label>
            <select id="channelMode${i}" onchange="toggleAsioFields(${i})" style="margin-bottom: 12px;">
                <option value="0" ${channelMode === 0 ? 'selected' : ''}>NDI</option>
                <option value="1" ${channelMode === 1 ? 'selected' : ''}>ASIO</option>
            </select>
            `
            : `
            <input type="hidden" id="channelMode${i}" value="0">
            <p style="margin: 0 0 12px 0; color: #888; font-size: 13px;">Mode: NDI (ASIO is available on Windows only).</p>
            `;

        const ndiReceiveDisplay = productInfo.asioAvailable
            ? (channelMode === 0 ? 'block' : 'none')
            : 'block';

        const asioBlock = productInfo.asioAvailable
            ? `
            <div id="asioFields${i}" style="display: ${channelMode === 1 ? 'block' : 'none'}; margin-top: 12px; padding-top: 12px; border-top: 1px solid rgba(255,255,255,0.1);">
                <label>ASIO Input:</label>
                <select id="asioInput${i}" style="margin-bottom: 8px;">
                    ${buildAsioChannelOptions(asioChannelCounts.inputChannels, channelConfig.asioInputChannel, "Input")}
                </select>

                <label>ASIO Output:</label>
                <select id="asioOutput${i}">
                    ${buildAsioChannelOptions(asioChannelCounts.outputChannels, channelConfig.asioOutputChannel, "Output")}
                </select>
            </div>
            `
            : `
            <input type="hidden" id="asioInput${i}" value="0">
            <input type="hidden" id="asioOutput${i}" value="0">
            `;

        channelDiv.innerHTML = `
            <h3>Channel ${i}</h3>
            ${modeBlock}
            <div id="ndiReceiveField${i}" style="display: ${ndiReceiveDisplay};">
                <label>NDI Receive:</label>
                <select id="ndiReceive${i}">
                    <option value="">None</option>
                    ${missingReceiveOption}
                    ${ndiSources.map(source => `
                        <option value="${escapeHtml(source)}" ${source === channelConfig.ndiReceiveName ? "selected" : ""}>
                            ${escapeHtml(source)}
                        </option>
                    `).join("")}
                </select>
            </div>

            <label>NDI Send:</label>
            <input type="text" id="ndiSend${i}" value="${escapeHtml(channelConfig.ndiSendName)}" placeholder="Channel ${i}">
            ${asioBlock}
        `;

        grid.appendChild(channelDiv);
    }
}

// Toggle ASIO fields visibility
function toggleAsioFields(channelNumber) {
    if (!productInfo.asioAvailable) {
        return;
    }

    const modeSelect = document.getElementById(`channelMode${channelNumber}`);
    const asioFields = document.getElementById(`asioFields${channelNumber}`);
    const ndiReceiveField = document.getElementById(`ndiReceiveField${channelNumber}`);

    if (!modeSelect || modeSelect.tagName === "INPUT") {
        return;
    }

    if (modeSelect.value === "1") {
        // ASIO mode
        asioFields.style.display = "block";
        ndiReceiveField.style.display = "none";
    } else {
        // NDI mode
        asioFields.style.display = "none";
        ndiReceiveField.style.display = "block";
    }
}

// Apply settings
async function applySettings() {
    try {
        const applicationId = getValidatedApplicationId();
        if (!applicationId) {
            return;
        }

        // Build configuration object
        const asioSelectEl = document.getElementById("asioDeviceSelect");
        const newConfig = {
            applicationId: applicationId,
            deviceId: config.deviceId || "",
            selectedMicrophone: document.getElementById("microphoneSelect").value,
            selectedSpeaker: document.getElementById("speakerSelect").value,
            selectedAsioDevice: productInfo.asioAvailable && asioSelectEl ? asioSelectEl.value : (config.selectedAsioDevice || ""),
            asioInputChannelCount: config.asioInputChannelCount || 0,
            asioOutputChannelCount: config.asioOutputChannelCount || 0,

            // Microphone Noise Gate
            noiseGateEnabled: document.getElementById("enableNoiseGate").checked,
            noiseGateThresholdDb: parseFloat(document.getElementById("noiseGateThreshold").value),
            noiseGateAttackMs: parseInt(document.getElementById("noiseGateAttack").value),
            noiseGateReleaseMs: parseInt(document.getElementById("noiseGateRelease").value),
            noiseGateHoldMs: parseInt(document.getElementById("noiseGateHold").value),

            // Channels Feedback Gate
            feedbackGateEnabled: document.getElementById("enableFeedbackGate").checked,
            gateThresholdDb: parseFloat(document.getElementById("feedbackGateThreshold").value),
            gateAttackMs: parseInt(document.getElementById("feedbackGateAttack").value),
            gateReleaseMs: parseInt(document.getElementById("feedbackGateRelease").value),

            channels: []
        };

        // Live Talk/Listen (not edited on this page) so Apply does not wipe button state.
        let liveChannels = [];
        try {
            liveChannels = await connection.invoke("GetChannels") || [];
        } catch (_) {
            liveChannels = [];
        }

        // Collect channel configuration (NDI + ASIO)
        for (let i = 1; i <= maxIntercomChannels; i++) {
            const channelConfig = config.channels && config.channels[i - 1]
                ? config.channels[i - 1]
                : {};
            const live = liveChannels[i - 1] || {};

            const modeEl = document.getElementById(`channelMode${i}`);
            const channelMode = productInfo.asioAvailable
                ? parseInt(modeEl.value, 10)
                : 0;

            const asioInEl = document.getElementById(`asioInput${i}`);
            const asioOutEl = document.getElementById(`asioOutput${i}`);

            newConfig.channels.push({
                channelNumber: i,
                label: channelConfig.label || `Channel ${i}`,
                inputLevel: channelConfig.inputLevel ?? 100,
                outputLevel: channelConfig.outputLevel ?? 100,
                talkEnabled: live.talkEnabled ?? channelConfig.talkEnabled ?? false,
                listenEnabled: live.listenEnabled ?? channelConfig.listenEnabled ?? false,
                intercomGroup: channelConfig.intercomGroup ?? channelConfig.ndiIntercomGroup ?? channelConfig.asioIntercomGroup ?? 0,
                ndiSendName: document.getElementById(`ndiSend${i}`).value,
                ndiReceiveName: channelMode === 0 ? document.getElementById(`ndiReceive${i}`).value : (channelConfig.ndiReceiveName || ""),
                mode: channelMode,
                asioInputChannel: productInfo.asioAvailable && asioInEl ? parseInt(asioInEl.value, 10) : 0,
                asioOutputChannel: productInfo.asioAvailable && asioOutEl ? parseInt(asioOutEl.value, 10) : 0
            });
        }

        // Apply configuration
        await connection.invoke("ApplyConfiguration", newConfig);

        alert("Settings applied successfully!");
        window.location.href = "/index.html";
    } catch (err) {
        alert("Error applying settings: " + err.message);
    }
}

// Handle reconnection
connection.onreconnecting(() => {
    // Silently continue
});

connection.onreconnected(() => {
    loadSettings();
});

connection.onclose(() => {
    setTimeout(startConnection, 5000);
});

// Start
startConnection();

// ========== NDI Bridge Control Functions ==========

// Helper: GET a Bridge proxy value
async function bridgeGet(path) {
    const resp = await fetch(`/api/ndibridge/proxy/${path}`);
    return await resp.json();
}

// Helper: POST a Bridge proxy value
async function bridgeSet(path, value) {
    const resp = await fetch(`/api/ndibridge/proxy/${path}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ value: String(value) })
    });
    return await resp.json();
}

// Convert HX Quality raw value (10-400) to Mbit/s
function hxQualityToMbps(value) {
    const mbps = 1.6 + (value - 10) * (64 - 1.6) / (400 - 10);
    return mbps.toFixed(1);
}

// Setup range slider display updates
function setupRangeDisplay(inputId, displayId, formatter) {
    const input = document.getElementById(inputId);
    const display = document.getElementById(displayId);
    if (input && display) {
        const update = () => {
            display.textContent = formatter ? formatter(input.value) : input.value;
        };
        input.addEventListener('input', update);
    }
}

// Initialize range displays
function initRangeDisplays() {
    setupRangeDisplay('hostBuffer', 'hostBufferValue', null);
    setupRangeDisplay('hostHxQuality', 'hostHxQualityValue', v => hxQualityToMbps(v) + ' Mbit/s');
    setupRangeDisplay('joinBuffer', 'joinBufferValue', null);
    setupRangeDisplay('joinHxQuality', 'joinHxQualityValue', v => hxQualityToMbps(v) + ' Mbit/s');
    setupRangeDisplay('localHxQuality', 'localHxQualityValue', v => hxQualityToMbps(v) + ' Mbit/s');
}

// Load NDI Bridge status
async function loadBridgeStatus() {
    try {
        const testResponse = await fetch('/api/ndibridge/test');
        const testData = await testResponse.json();
        
        bridgeConnected = testData.connected;
        
        if (bridgeConnected) {
            // Get run mode
            const modeResponse = await fetch('/api/ndibridge/runmode');
            const modeData = await modeResponse.json();
            currentBridgeMode = modeData.mode || "NONE";
            
            // Update UI
            updateBridgeStatus(true, `Connected - Mode: ${currentBridgeMode}`);
            document.getElementById('bridgeModeGroup').style.display = 'block';
            document.getElementById('bridgeGlobalSettings').style.display = 'block';
            document.getElementById('currentBridgeMode').textContent = currentBridgeMode;
            
            // Update button states
            updateBridgeModeButtons();
            
            // Load all settings
            await loadGpuList();
            await loadGlobalSettings();
            await loadAllModeSettings();
            initRangeDisplays();

            // If a mode is running, select that panel and show connection test
            if (currentBridgeMode !== 'NONE') {
                selectBridgePanel(currentBridgeMode.toLowerCase());
                document.getElementById('connectionTestPanel').style.display = 'block';
            }

            // Start periodic status refresh
            startStatusRefresh();
        } else {
            updateBridgeStatus(false, 'Not connected - Check if NDI Bridge Service is running');
            document.getElementById('bridgeModeGroup').style.display = 'none';
            document.getElementById('bridgeGlobalSettings').style.display = 'none';
        }
    } catch (err) {
        updateBridgeStatus(false, 'Error connecting to NDI Bridge Service');
        document.getElementById('bridgeModeGroup').style.display = 'none';
        document.getElementById('bridgeGlobalSettings').style.display = 'none';
    }
}

// Update bridge status UI
function updateBridgeStatus(connected, message) {
    const indicator = document.getElementById('bridgeStatusIndicator');
    const text = document.getElementById('bridgeStatusText');
    
    indicator.className = 'status-indicator ' + (connected ? 'connected' : 'disconnected');
    text.textContent = message;
}

// Refresh bridge status
async function refreshBridgeStatus() {
    await loadBridgeStatus();
}

// Load GPU list and populate all GPU dropdowns
async function loadGpuList() {
    try {
        const data = await bridgeGet('gpu_list');
        let gpuList = [];

        if (data.value) {
            // The Bridge API may return GPU list in different formats:
            // - JSON array string: '["Intel GPU","NVIDIA GPU"]'
            // - Comma-separated: 'Intel GPU,NVIDIA GPU'
            // - Newline-separated: 'Intel GPU\nNVIDIA GPU'
            const raw = data.value;
            try {
                const parsed = JSON.parse(raw);
                if (Array.isArray(parsed)) {
                    gpuList = parsed.map(g => String(g).trim()).filter(Boolean);
                } else {
                    gpuList = [String(parsed).trim()].filter(Boolean);
                }
            } catch {
                // Not JSON, try splitting by common separators
                if (raw.includes('\n')) {
                    gpuList = raw.split('\n').map(g => g.trim()).filter(Boolean);
                } else if (raw.includes(',')) {
                    gpuList = raw.split(',').map(g => g.trim()).filter(Boolean);
                } else {
                    gpuList = [raw.trim()].filter(Boolean);
                }
            }
        }

        console.log('GPU list loaded:', gpuList);

        ['hostHxGpu', 'joinHxGpu', 'localHxGpu'].forEach(selectId => {
            const select = document.getElementById(selectId);
            select.innerHTML = '<option value="Auto">Auto</option>';
            gpuList.forEach(gpu => {
                const opt = document.createElement('option');
                opt.value = gpu;
                opt.textContent = gpu;
                select.appendChild(opt);
            });
        });
    } catch (err) {
        console.error('Error loading GPU list:', err);
    }
}

// Load global settings
async function loadGlobalSettings() {
    try {
        const autoStart = await bridgeGet('auto_start');
        document.getElementById('bridgeAutoStart').value = autoStart.value || '0';

        // Auto-save on change
        document.getElementById('bridgeAutoStart').addEventListener('change', async (e) => {
            await bridgeSet('auto_start', e.target.value);
        });
    } catch (err) {
        console.error('Error loading global settings:', err);
    }
}

// Load all mode settings from Bridge API
async function loadAllModeSettings() {
    try {
        // Host settings
        await loadModeSetting('host/bridge_name', 'hostBridgeName');
        await loadModeSetting('host/port', 'hostPort');
        await loadModeSetting('host/groups', 'hostGroups');
        await loadModeSetting('host/buffer', 'hostBuffer', 'hostBufferValue');
        await loadModeSetting('host/hx_output', 'hostHxOutput');
        await loadModeSetting('host/hx_encoder', 'hostHxEncoder');
        await loadModeSetting('host/hx_quality', 'hostHxQuality', 'hostHxQualityValue', v => hxQualityToMbps(v) + ' Mbit/s');
        await loadModeSetting('host/hx_gpu', 'hostHxGpu');
        await loadModeCheckbox('host/hx_ndi4_compatibility_mode', 'hostNdi4Compat');

        // Join settings
        await loadModeSetting('join/bridge_name', 'joinBridgeName');
        await loadModeSetting('join/ip_address', 'joinIp');
        await loadModeSetting('join/port', 'joinPort');
        await loadModeSetting('join/groups', 'joinGroups');
        await loadModeSetting('join/buffer', 'joinBuffer', 'joinBufferValue');
        await loadModeSetting('join/hx_output', 'joinHxOutput');
        await loadModeSetting('join/hx_encoder', 'joinHxEncoder');
        await loadModeSetting('join/hx_quality', 'joinHxQuality', 'joinHxQualityValue', v => hxQualityToMbps(v) + ' Mbit/s');
        await loadModeSetting('join/hx_gpu', 'joinHxGpu');
        await loadModeCheckbox('join/hx_ndi4_compatibility_mode', 'joinNdi4Compat');

        // Local settings
        await loadModeSetting('local/bridge_name', 'localBridgeName');
        await loadModeSetting('local/groups', 'localGroups');
        await loadModeSetting('local/send_groups', 'localSendGroups');
        await loadModeSetting('local/hx_output', 'localHxOutput');
        await loadModeSetting('local/hx_quality', 'localHxQuality', 'localHxQualityValue', v => hxQualityToMbps(v) + ' Mbit/s');
        await loadModeSetting('local/hx_gpu', 'localHxGpu');
        await loadModeCheckbox('local/hx_ndi4_compatibility_mode', 'localNdi4Compat');
    } catch (err) {
        console.error('Error loading mode settings:', err);
    }
}

// Load a single setting into an input/select element
async function loadModeSetting(apiPath, elementId, displayId, formatter) {
    try {
        const data = await bridgeGet(apiPath);
        const el = document.getElementById(elementId);
        if (el && data.value !== undefined) {
            el.value = data.value;
            if (displayId) {
                const disp = document.getElementById(displayId);
                if (disp) disp.textContent = formatter ? formatter(data.value) : data.value;
            }
        }
    } catch (err) {
        // Silently continue if endpoint doesn't exist
    }
}

// Load a boolean setting into a checkbox
async function loadModeCheckbox(apiPath, elementId) {
    try {
        const data = await bridgeGet(apiPath);
        const el = document.getElementById(elementId);
        if (el && data.value !== undefined) {
            el.checked = data.value === 'true' || data.value === 'True' || data.value === true;
        }
    } catch (err) {
        // Silently continue
    }
}

// ========== Mode Panel Selection ==========

// Select which settings panel to show (tab-like behavior)
function selectBridgePanel(mode) {
    selectedBridgePanel = mode;

    // Hide all panels
    document.getElementById('hostSettings').style.display = 'none';
    document.getElementById('joinSettings').style.display = 'none';
    document.getElementById('localSettings').style.display = 'none';

    // Show selected panel
    const panelId = mode + 'Settings';
    document.getElementById(panelId).style.display = 'block';

    // Update tab button classes
    updateBridgeModeButtons();
}

// Update mode button states (selected tab + active mode)
function updateBridgeModeButtons() {
    const hostBtn = document.getElementById('btnHostMode');
    const joinBtn = document.getElementById('btnJoinMode');
    const localBtn = document.getElementById('btnLocalMode');
    const stopBtn = document.getElementById('btnStopMode');
    
    // Clear all classes
    hostBtn.classList.remove('active', 'selected');
    joinBtn.classList.remove('active', 'selected');
    localBtn.classList.remove('active', 'selected');

    // Mark active mode (currently running)
    if (currentBridgeMode === 'HOST') hostBtn.classList.add('active');
    if (currentBridgeMode === 'JOIN') joinBtn.classList.add('active');
    if (currentBridgeMode === 'LOCAL') localBtn.classList.add('active');

    // Mark selected tab (panel visible)
    if (selectedBridgePanel === 'host' && currentBridgeMode !== 'HOST') hostBtn.classList.add('selected');
    if (selectedBridgePanel === 'join' && currentBridgeMode !== 'JOIN') joinBtn.classList.add('selected');
    if (selectedBridgePanel === 'local' && currentBridgeMode !== 'LOCAL') localBtn.classList.add('selected');
    
    // Enable/disable stop button
    stopBtn.disabled = currentBridgeMode === 'NONE';

    // Update start buttons text and state
    updateStartButtons();
}

// Update start button labels based on mode state
function updateStartButtons() {
    ['host', 'join', 'local'].forEach(mode => {
        const btn = document.getElementById(`btnStart${mode.charAt(0).toUpperCase() + mode.slice(1)}`);
        if (!btn) return;

        if (currentBridgeMode === mode.toUpperCase()) {
            btn.textContent = `${mode.charAt(0).toUpperCase() + mode.slice(1)} Running`;
            btn.disabled = true;
        } else {
            btn.textContent = `Start ${mode.charAt(0).toUpperCase() + mode.slice(1)}`;
            btn.disabled = false;
        }
    });
}

// ========== Start / Stop Bridge Mode ==========

// Start a bridge mode (called from panel's Start button)
async function startBridgeMode(mode) {
    if (!bridgeConnected) {
        alert('Not connected to NDI Bridge Service');
        return;
    }

    // Validate required fields
    if (mode === 'join') {
        const joinIp = document.getElementById('joinIp').value.trim();
        if (!joinIp) {
            alert('Please enter the Host IP Address before starting Join mode.');
            document.getElementById('joinIp').focus();
            return;
        }
    }

    try {
        // Stop current mode if running
        if (currentBridgeMode !== 'NONE') {
            await stopBridgeMode();
        }

        // Save settings before starting
        await saveBridgeModeSettings(mode);

        // Start the mode
        const response = await fetch(`/api/ndibridge/${mode}/start`, { method: 'POST' });
        const data = await response.json();

        if (data.success) {
            currentBridgeMode = mode.toUpperCase();
            updateBridgeModeButtons();
            updateBridgeStatus(true, `Connected - Mode: ${currentBridgeMode}`);
            document.getElementById('currentBridgeMode').textContent = currentBridgeMode;
            document.getElementById('connectionTestPanel').style.display = 'block';
        } else {
            alert(`Failed to start ${mode} mode: ${data.message || data.error || 'Unknown error'}`);
        }
    } catch (err) {
        alert(`Error: ${err.message}`);
    }
}

// Stop the currently running bridge mode
async function stopBridgeMode() {
    if (!bridgeConnected || currentBridgeMode === 'NONE') {
        return;
    }
    
    try {
        const mode = currentBridgeMode.toLowerCase();
        const response = await fetch(`/api/ndibridge/${mode}/stop`, { method: 'POST' });
        const data = await response.json();

        if (data.success) {
            currentBridgeMode = 'NONE';
            updateBridgeModeButtons();
            updateBridgeStatus(true, 'Connected - Mode: NONE');
            document.getElementById('currentBridgeMode').textContent = 'NONE';
            document.getElementById('connectionTestPanel').style.display = 'none';
        } else {
            alert(`Failed to stop: ${data.message || data.error || 'Unknown error'}`);
        }
    } catch (err) {
        alert(`Error: ${err.message}`);
    }
}

// ========== Save Settings ==========

// Save all settings for a given mode to the Bridge API
async function saveBridgeModeSettings(mode) {
    try {
        if (mode === 'host') {
            await bridgeSet('host/bridge_name', document.getElementById('hostBridgeName').value);
            await bridgeSet('host/port', document.getElementById('hostPort').value);
            await bridgeSet('host/groups', document.getElementById('hostGroups').value);
            await bridgeSet('host/buffer', document.getElementById('hostBuffer').value);
            await bridgeSet('host/hx_output', document.getElementById('hostHxOutput').value);
            await bridgeSet('host/hx_encoder', document.getElementById('hostHxEncoder').value);
            await bridgeSet('host/hx_quality', document.getElementById('hostHxQuality').value);
            await bridgeSet('host/hx_gpu', document.getElementById('hostHxGpu').value);
            await bridgeSet('host/hx_ndi4_compatibility_mode', document.getElementById('hostNdi4Compat').checked.toString());
        } else if (mode === 'join') {
            await bridgeSet('join/bridge_name', document.getElementById('joinBridgeName').value);
            await bridgeSet('join/ip_address', document.getElementById('joinIp').value);
            await bridgeSet('join/port', document.getElementById('joinPort').value);
            await bridgeSet('join/groups', document.getElementById('joinGroups').value);
            await bridgeSet('join/buffer', document.getElementById('joinBuffer').value);
            await bridgeSet('join/hx_output', document.getElementById('joinHxOutput').value);
            await bridgeSet('join/hx_encoder', document.getElementById('joinHxEncoder').value);
            await bridgeSet('join/hx_quality', document.getElementById('joinHxQuality').value);
            await bridgeSet('join/hx_gpu', document.getElementById('joinHxGpu').value);
            await bridgeSet('join/hx_ndi4_compatibility_mode', document.getElementById('joinNdi4Compat').checked.toString());
        } else if (mode === 'local') {
            await bridgeSet('local/bridge_name', document.getElementById('localBridgeName').value);
            await bridgeSet('local/groups', document.getElementById('localGroups').value);
            await bridgeSet('local/send_groups', document.getElementById('localSendGroups').value);
            await bridgeSet('local/hx_output', document.getElementById('localHxOutput').value);
            await bridgeSet('local/hx_quality', document.getElementById('localHxQuality').value);
            await bridgeSet('local/hx_gpu', document.getElementById('localHxGpu').value);
            await bridgeSet('local/hx_ndi4_compatibility_mode', document.getElementById('localNdi4Compat').checked.toString());
        }
    } catch (err) {
        console.error('Error saving bridge settings:', err);
    }
}

// ========== Encryption ==========

async function setEncryptionKey(mode) {
    const input = document.getElementById(`${mode}EncryptionKey`);
    const key = input.value.trim();
    if (!key) {
        alert('Please enter an encryption key.');
        input.focus();
        return;
    }

    try {
        const result = await bridgeSet(`${mode}/set_encryption_key`, key);
        if (result.success) {
            input.value = '';
            alert('Encryption key set successfully.');
        } else {
            alert(`Failed to set encryption key: ${result.error || 'Unknown error'}`);
        }
    } catch (err) {
        alert(`Error: ${err.message}`);
    }
}

async function clearEncryptionKey(mode) {
    try {
        const result = await bridgeSet(`${mode}/clear_encryption_key`, '');
        if (result.success) {
            document.getElementById(`${mode}EncryptionKey`).value = '';
            alert('Encryption key cleared.');
        } else {
            alert(`Failed to clear encryption key: ${result.error || 'Unknown error'}`);
        }
    } catch (err) {
        alert(`Error: ${err.message}`);
    }
}

// ========== Connection Test ==========

async function startConnectionTest() {
    try {
        // Set bitrate first
        const bitrate = document.getElementById('connectionTestBitrate').value;
        await bridgeSet('connection_test_bitrate', bitrate);

        // Start test
        const resp = await fetch('/api/ndibridge/proxy/connection_test_start', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ value: '' })
        });
        const data = await resp.json();

        if (data.success) {
            document.getElementById('btnConnectionTestStart').style.display = 'none';
            document.getElementById('btnConnectionTestStop').style.display = 'inline-block';
            document.getElementById('connectionTestResult').style.display = 'block';
            document.getElementById('connectionTestMessage').textContent = 'Test running...';
        } else {
            alert(`Failed to start connection test: ${data.error || 'Unknown error'}`);
        }
    } catch (err) {
        alert(`Error: ${err.message}`);
    }
}

async function stopConnectionTest() {
    try {
        const resp = await fetch('/api/ndibridge/proxy/connection_test_stop', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ value: '' })
        });
        const data = await resp.json();

        document.getElementById('btnConnectionTestStart').style.display = 'inline-block';
        document.getElementById('btnConnectionTestStop').style.display = 'none';

        if (data.success) {
            document.getElementById('connectionTestResult').style.display = 'block';
            document.getElementById('connectionTestMessage').textContent =
                `Test complete. Recommended buffer: ${data.value || 'N/A'}`;
        } else {
            document.getElementById('connectionTestMessage').textContent =
                `Test stopped: ${data.error || 'Unknown'}`;
        }
    } catch (err) {
        alert(`Error: ${err.message}`);
    }
}

// ========== Periodic Status Refresh ==========

function startStatusRefresh() {
    // Clear existing interval
    if (bridgeStatusInterval) clearInterval(bridgeStatusInterval);
    
    // Refresh status every 5 seconds
    bridgeStatusInterval = setInterval(refreshModeStatus, 5000);
}

async function refreshModeStatus() {
    if (!bridgeConnected || currentBridgeMode === 'NONE') return;

    try {
        const mode = currentBridgeMode.toLowerCase();

        // Status message
        const statusData = await bridgeGet(`${mode}/status_message`);
        const statusEl = document.getElementById(`${mode}StatusMessage`);
        if (statusEl) statusEl.textContent = statusData.value || '-';

        // Bandwidth (host and join only)
        if (mode === 'host' || mode === 'join') {
            const bwData = await bridgeGet(`${mode}/bandwidth_message`);
            const bwEl = document.getElementById(`${mode}Bandwidth`);
            if (bwEl) bwEl.textContent = bwData.value || '-';

            const gpuData = await bridgeGet(`${mode}/gpu_limit`);
            const gpuEl = document.getElementById(`${mode}GpuLimit`);
            if (gpuEl) gpuEl.textContent = gpuData.value === 'true' || gpuData.value === 'True' ? 'Yes' : 'No';
        }
    } catch (err) {
        // Silently continue
    }
}
