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

let channels = [];
let maxIntercomChannels = 16;

// Track last NDI level update for each channel (for timeout detection)
const ndiLevelTimestamps = {};
const ndiLastValues = {};  // Track last value to detect if frozen
const ndiFrozen = {};      // Track if channel is in frozen state

// Start connection
async function startConnection() {
    try {
        await connection.start();
        const product = await connection.invoke("GetProductInfo");
        maxIntercomChannels = product.maxChannels || 16;
        document.title = product.productDisplayName || document.title;
        const titleEl = document.querySelector(".header .title");
        if (titleEl && product.uiTitleShort) {
            titleEl.textContent = product.uiTitleShort;
        }
        await loadChannels();
    } catch (err) {
        setTimeout(startConnection, 5000);
    }
}

// Load channels from server
async function loadChannels() {
    try {
        channels = await connection.invoke("GetChannels");
        renderChannels();
    } catch (err) {
        // Silently continue
    }
}

// Render channels grid
function renderChannels() {
    const grid = document.getElementById("channelsGrid");
    grid.innerHTML = "";

    const fewChannels = channels.length > 0 && channels.length <= 4;
    grid.classList.toggle("channels-grid--few", fewChannels);

    channels.forEach(channel => {
        const card = document.createElement("div");
        card.className = "channel-card";
        card.innerHTML = `
            <div class="channel-header" onclick="editChannelLabel(${channel.channelNumber})">
                ${escapeHtml(channel.label || "Channel " + channel.channelNumber)}
            </div>

            <button class="btn-talk ${channel.talkEnabled ? 'active' : ''}"
                    onclick="toggleTalk(${channel.channelNumber})">
                TALK
            </button>

            <button class="btn-listen ${channel.listenEnabled ? 'active' : ''}"
                    onclick="toggleListen(${channel.channelNumber})">
                LISTEN
            </button>

            <div class="level-controls">
                <div class="level-control">
                    <div class="level-label">Input Level</div>
                    <div class="level-with-vu">
                        <div class="ndi-vu-meter-vertical" id="ndiVu${channel.channelNumber}">
                            <div class="ndi-vu-meter-fill-vertical" id="ndiVuFill${channel.channelNumber}"></div>
                        </div>
                        <div class="level-knob input"
                             data-channel="${channel.channelNumber}"
                             data-type="input"
                             data-value="${channel.inputLevel}"
                             style="transform: rotate(${knobRotationDegrees(channel.inputLevel, 'input')}deg)">
                        </div>
                    </div>
                    <div class="level-value input">${channel.inputLevel}</div>
                </div>

                <div class="level-control">
                    <div class="level-label">Output Level</div>
                    <div class="level-knob output"
                         data-channel="${channel.channelNumber}"
                         data-type="output"
                         data-value="${channel.outputLevel}"
                         style="transform: rotate(${(channel.outputLevel - 100) * 2.7}deg)">
                    </div>
                    <div class="level-value output">${channel.outputLevel}</div>
                </div>
            </div>

            <div class="intercom-groups">
                <div class="groups-label">Intercom Group</div>
                <div class="groups-buttons">
                    <button class="group-btn ${(channel.intercomGroup === 0 || !channel.intercomGroup) ? 'active' : ''}"
                            onclick="setGroup(${channel.channelNumber}, 0)">
                        NONE
                    </button>
                    ${[1, 2, 3, 4].map(g => `
                        <button class="group-btn ${channel.intercomGroup === g ? 'active' : ''}"
                                onclick="setGroup(${channel.channelNumber}, ${g})">
                            ${g}
                        </button>
                    `).join('')}
                </div>
            </div>
        `;
        grid.appendChild(card);
    });

    // Initialize knob handlers
    initializeKnobs();
}

// Toggle TALK
async function toggleTalk(channelNumber) {
    try {
        await connection.invoke("ToggleTalk", channelNumber);
        await loadChannels();
    } catch (err) {
        // Silently continue
    }
}

// Toggle LISTEN
async function toggleListen(channelNumber) {
    try {
        await connection.invoke("ToggleListen", channelNumber);
        await loadChannels();
    } catch (err) {
        // Silently continue
    }
}

// Set intercom group
async function setGroup(channelNumber, group) {
    try {
        await connection.invoke("SetIntercomGroup", channelNumber, group);
        await loadChannels();
    } catch (err) {
        // Silently continue
    }
}

// Edit channel label
async function editChannelLabel(channelNumber) {
    const channel = channels.find(c => c.channelNumber === channelNumber);
    const newLabel = prompt("Enter new label:", channel.label);

    if (newLabel && newLabel.trim()) {
        try {
            await connection.invoke("UpdateChannelLabel", channelNumber, newLabel.trim());
            await loadChannels();
        } catch (err) {
            // Silently continue
        }
    }
}

// Update specific channel UI without full re-render
function updateChannelUI(channel) {
    // Update TALK button
    const cards = document.querySelectorAll('.channel-card');
    const card = cards[channel.channelNumber - 1]; // 0-indexed
    if (!card) return;

    // Update TALK button
    const talkBtn = card.querySelector('.btn-talk');
    if (talkBtn) {
        if (channel.talkEnabled) {
            talkBtn.classList.add('active');
        } else {
            talkBtn.classList.remove('active');
        }
    }

    // Update LISTEN button
    const listenBtn = card.querySelector('.btn-listen');
    if (listenBtn) {
        if (channel.listenEnabled) {
            listenBtn.classList.add('active');
        } else {
            listenBtn.classList.remove('active');
        }
    }

    // Update channel label
    const header = card.querySelector('.channel-header');
    if (header) {
        header.textContent = channel.label || 'Channel ' + channel.channelNumber;
    }

    // Update intercom group buttons (unified group for all modes)
    const groupBtns = card.querySelectorAll('.group-btn');
    const currentGroup = channel.intercomGroup ?? 0;

    groupBtns.forEach((btn, index) => {
        const groupNum = index; // 0 = NONE, 1-4 = Groups

        if (currentGroup === groupNum) {
            btn.classList.add('active');
        } else {
            btn.classList.remove('active');
        }
    });

    // Update input level (only if not currently being dragged)
    const inputKnob = card.querySelector('.level-knob.input');
    if (inputKnob && !inputKnob.dataset.dragging) {
        inputKnob.dataset.value = channel.inputLevel;
        inputKnob.style.transform = `rotate(${knobRotationDegrees(channel.inputLevel, 'input')}deg)`;
        const inputValue = card.querySelector('.level-value.input');
        if (inputValue) {
            inputValue.textContent = channel.inputLevel;
        }
    }

    // Update output level (only if not currently being dragged)
    const outputKnob = card.querySelector('.level-knob.output');
    if (outputKnob && !outputKnob.dataset.dragging) {
        outputKnob.dataset.value = channel.outputLevel;
        outputKnob.style.transform = `rotate(${knobRotationDegrees(channel.outputLevel, 'output')}deg)`;
        const outputValue = card.querySelector('.level-value.output');
        if (outputValue) {
            outputValue.textContent = channel.outputLevel;
        }
    }
}

/**
 * Knob rotation: Input 0=mute (left), 100=unity (center), 300=3× (right).
 * Output keeps legacy 0–100 mapping (unity at max / center).
 */
function knobRotationDegrees(value, type) {
    const v = Number(value) || 0;
    if (type === 'input') {
        if (v <= 100) {
            return ((v - 100) / 100) * 135;
        }
        return ((v - 100) / 200) * 135;
    }
    return (v - 100) * 2.7;
}

// Initialize knob controls
function initializeKnobs() {
    const knobs = document.querySelectorAll('.level-knob');

    knobs.forEach(knob => {
        let isDragging = false;
        let startY = 0;
        let startValue = 0;
        let activePointerId = null;

        const updateLevel = async (clientY) => {
            const type = knob.dataset.type;
            const max = type === 'input' ? 300 : 100;
            const deltaY = startY - clientY;
            let newValue = startValue + Math.round(deltaY / 2);
            newValue = Math.max(0, Math.min(max, newValue));

            knob.dataset.value = newValue;
            knob.style.transform = `rotate(${knobRotationDegrees(newValue, type)}deg)`;

            const valueDisplay = knob.parentElement.querySelector('.level-value');
            if (!valueDisplay) {
                const parent = knob.closest('.level-control');
                if (parent) {
                    const levelValue = parent.querySelector('.level-value');
                    if (levelValue) {
                        levelValue.textContent = newValue;
                    }
                }
            } else {
                valueDisplay.textContent = newValue;
            }

            const channelNumber = parseInt(knob.dataset.channel);

            try {
                if (type === 'input') {
                    await connection.invoke("SetInputLevel", channelNumber, newValue);
                } else {
                    await connection.invoke("SetOutputLevel", channelNumber, newValue);
                }
            } catch (err) {
                // Silently continue
            }
        };

        knob.addEventListener('pointerdown', (e) => {
            activePointerId = e.pointerId;
            knob.setPointerCapture(activePointerId);
            isDragging = true;
            knob.dataset.dragging = 'true';
            startY = e.clientY;
            startValue = parseInt(knob.dataset.value, 10);
            e.preventDefault();
        });

        knob.addEventListener('pointermove', (e) => {
            if (!isDragging || e.pointerId !== activePointerId) return;
            updateLevel(e.clientY);
        });

        const endDrag = (e) => {
            if (e.pointerId !== activePointerId) return;
            isDragging = false;
            activePointerId = null;
            knob.dataset.dragging = '';
            if (knob.hasPointerCapture(e.pointerId)) {
                knob.releasePointerCapture(e.pointerId);
            }
        };

        knob.addEventListener('pointerup', endDrag);
        knob.addEventListener('pointercancel', endDrag);
    });
}

// VU Meter update
connection.on("VUMeterUpdate", (level) => {
    const vuMeterFill = document.getElementById("vuMeterFill");
    if (vuMeterFill) {
        // Convert dB to percentage (rough mapping)
        // -60dB = 0%, 0dB = 100%
        const percentage = Math.max(0, Math.min(100, (level + 60) * 1.67));
        vuMeterFill.style.width = percentage + "%";
    }
});

// NDI audio level update (per channel)
connection.on("ChannelNDILevelUpdated", (channelNumber, level) => {
    const ndiVuFill = document.getElementById(`ndiVuFill${channelNumber}`);
    if (ndiVuFill) {
        // Check if value has changed (to detect frozen stream)
        const lastValue = ndiLastValues[channelNumber];
        const valueChanged = lastValue === undefined || Math.abs(level - lastValue) > 0.1;

        if (valueChanged) {
            // Value changed - unfreeze and update timestamp
            ndiFrozen[channelNumber] = false;
            ndiLevelTimestamps[channelNumber] = Date.now();
            ndiLastValues[channelNumber] = level;
        }

        // If channel is frozen, ignore updates and keep at 0%
        if (ndiFrozen[channelNumber]) {
            ndiVuFill.style.height = "0%";
            return;
        }

        // If level is very low (below -40dB), consider it as no signal
        if (level < -40) {
            ndiVuFill.style.height = "0%";
        } else {
            // Convert dB to percentage (rough mapping)
            // -60dB = 0%, 0dB = 100%
            const percentage = Math.max(0, Math.min(100, (level + 60) * 1.67));
            ndiVuFill.style.height = percentage + "%";
        }
    }
});

// Channel updated event
connection.on("ChannelUpdated", (channel) => {
    const index = channels.findIndex(c => c.channelNumber === channel.channelNumber);
    if (index !== -1) {
        channels[index] = channel;
        // Update only the specific channel UI elements, not full re-render
        updateChannelUI(channel);
    }
});

// Handle reconnection
connection.onreconnecting(() => {
    // Silently continue
});

connection.onreconnected(() => {
    loadChannels();
});

connection.onclose(() => {
    setTimeout(startConnection, 5000);
});

// NDI VU meter timeout checker - freeze if value doesn't change for 1 second
setInterval(() => {
    const now = Date.now();
    const timeout = 1000; // 1 second

    for (let ch = 1; ch <= maxIntercomChannels; ch++) {
        const lastUpdate = ndiLevelTimestamps[ch];
        const ndiVuFill = document.getElementById(`ndiVuFill${ch}`);

        if (ndiVuFill && lastUpdate && !ndiFrozen[ch] && (now - lastUpdate) > timeout) {
            // Value hasn't changed for 1 second - freeze and reset VU meter
            ndiFrozen[ch] = true;
            ndiVuFill.style.height = "0%";
        }
    }
}, 200); // Check every 200ms

// Legacy group functions (kept for backward compatibility, redirect to unified setGroup)
async function setNdiGroup(channelNumber, group) { await setGroup(channelNumber, group); }
async function setAsioGroup(channelNumber, group) { await setGroup(channelNumber, group); }

// Preset Management Functions
function showSavePresetModal() {
    document.getElementById('savePresetModal').style.display = 'block';
    document.getElementById('presetNameInput').focus();
}

function closeSavePresetModal() {
    document.getElementById('savePresetModal').style.display = 'none';
    document.getElementById('presetNameInput').value = '';
}

async function savePreset() {
    const presetName = document.getElementById('presetNameInput').value.trim();

    if (!presetName) {
        alert('Please enter a preset name');
        return;
    }

    try {
        await connection.invoke("SavePreset", presetName);
        alert(`Preset "${presetName}" saved successfully!`);
        closeSavePresetModal();
    } catch (err) {
        alert(`Error saving preset: ${err.message}`);
    }
}

async function showLoadPresetModal() {
    document.getElementById('loadPresetModal').style.display = 'block';
    await loadPresetsList();
}

function closeLoadPresetModal() {
    document.getElementById('loadPresetModal').style.display = 'none';
}

async function loadPresetsList() {
    try {
        const presets = await connection.invoke("GetPresets");
        const presetsList = document.getElementById('presetsList');
        presetsList.innerHTML = '';

        if (presets.length === 0) {
            presetsList.innerHTML = '<p style="color: #999; text-align: center; padding: 20px;">No presets found</p>';
            return;
        }

        presets.forEach(presetName => {
            const presetItem = document.createElement("div");
            presetItem.className = "preset-item";

            const nameEl = document.createElement("div");
            nameEl.className = "preset-name";
            nameEl.textContent = presetName;

            const actionsEl = document.createElement("div");
            actionsEl.className = "preset-actions";

            const loadBtn = document.createElement("button");
            loadBtn.className = "preset-load-btn";
            loadBtn.textContent = "Load";
            loadBtn.addEventListener("click", () => loadPresetByName(presetName));

            const deleteBtn = document.createElement("button");
            deleteBtn.className = "preset-delete-btn";
            deleteBtn.textContent = "Delete";
            deleteBtn.addEventListener("click", () => deletePresetByName(presetName));

            actionsEl.appendChild(loadBtn);
            actionsEl.appendChild(deleteBtn);
            presetItem.appendChild(nameEl);
            presetItem.appendChild(actionsEl);
            presetsList.appendChild(presetItem);
        });
    } catch (err) {
        alert(`Error loading presets: ${err.message}`);
    }
}

async function loadPresetByName(presetName) {
    if (!confirm(`Load preset "${presetName}"?\n\nThis will overwrite your current configuration.`)) {
        return;
    }

    try {
        await connection.invoke("LoadPreset", presetName);
        alert(`Preset "${presetName}" loaded successfully!`);
        closeLoadPresetModal();
        await loadChannels();
    } catch (err) {
        alert(`Error loading preset: ${err.message}`);
    }
}

async function deletePresetByName(presetName) {
    if (!confirm(`Delete preset "${presetName}"?\n\nThis action cannot be undone.`)) {
        return;
    }

    try {
        await connection.invoke("DeletePreset", presetName);
        alert(`Preset "${presetName}" deleted successfully!`);
        await loadPresetsList();
    } catch (err) {
        alert(`Error deleting preset: ${err.message}`);
    }
}

// Listen for preset events from SignalR
connection.on("PresetSaved", (presetName) => {
    // Silently continue
});

connection.on("PresetLoaded", (presetName) => {
    // Silently continue
});

connection.on("PresetDeleted", (presetName) => {
    // Silently continue
});

connection.on("PresetError", (errorMessage) => {
    alert(`Preset error: ${errorMessage}`);
});

// Close modal when clicking outside
window.onclick = function(event) {
    const saveModal = document.getElementById('savePresetModal');
    const loadModal = document.getElementById('loadPresetModal');

    if (event.target === saveModal) {
        closeSavePresetModal();
    }
    if (event.target === loadModal) {
        closeLoadPresetModal();
    }
}

// Close modal when pressing ESC
document.addEventListener('keydown', function(event) {
    if (event.key === 'Escape') {
        closeSavePresetModal();
        closeLoadPresetModal();
    }
});

// Allow Enter key to save preset
document.addEventListener('keydown', function(event) {
    const modal = document.getElementById('savePresetModal');
    if (modal.style.display === 'block' && event.key === 'Enter') {
        savePreset();
    }
});

// Start
startConnection();
