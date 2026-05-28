using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Helper class for monitoring NDI senders registered on the Discovery Server (NDI 6.3 Advanced SDK)
    /// </summary>
    public class NDISendListener : IDisposable
    {
        private IntPtr _listenerInstance = IntPtr.Zero;
        private Dictionary<string, Dictionary<string, string>> _senderEvents = new();
        private HashSet<string> _subscribedSenders = new();

        /// <summary>
        /// Gets whether the listener is connected to the Discovery Server
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (_listenerInstance == IntPtr.Zero)
                    return false;
                return NDIWrapper.NDIlib_send_listener_is_connected(_listenerInstance);
            }
        }

        /// <summary>
        /// Gets the URL of the Discovery Server
        /// </summary>
        public string? ServerUrl
        {
            get
            {
                if (_listenerInstance == IntPtr.Zero)
                    return null;
                IntPtr urlPtr = NDIWrapper.NDIlib_send_listener_get_server_url(_listenerInstance);
                if (urlPtr == IntPtr.Zero)
                    return null;
                return Marshal.PtrToStringAnsi(urlPtr);
            }
        }

        /// <summary>
        /// Initialize the SendListener
        /// </summary>
        /// <param name="discoveryServerUrl">URL of the Discovery Server (null = default)</param>
        /// <returns>True if successfully initialized</returns>
        public bool Initialize(string? discoveryServerUrl = null)
        {
            var listenerSettings = new NDIWrapper.send_listener_create_t
            {
                p_url_address = IntPtr.Zero
            };

            _listenerInstance = NDIWrapper.NDIlib_send_listener_create(ref listenerSettings);

            if (_listenerInstance == IntPtr.Zero)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get the current list of registered senders from Discovery Server and update with event data
        /// </summary>
        /// <returns>List of sender information with stream properties</returns>
        public List<Models.NDISenderStreamInfo> GetSenders()
        {
            var senderList = new List<Models.NDISenderStreamInfo>();

            if (_listenerInstance == IntPtr.Zero)
                return senderList;

            // Get list of senders
            uint numSenders = 0;
            IntPtr sendersPtr = NDIWrapper.NDIlib_send_listener_get_senders(_listenerInstance, ref numSenders);

            if (sendersPtr == IntPtr.Zero || numSenders == 0)
            {
                // No senders currently registered: drop any cached state so the collections
                // don't keep growing on a churning network (devices coming and going).
                _subscribedSenders.Clear();
                _senderEvents.Clear();
                return senderList;
            }

            // Track which UUIDs we observe in this poll so we can prune the ones that
            // have disappeared from the Discovery Server (24/7 leak prevention).
            var seenUuids = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                int structSize = Marshal.SizeOf<NDIWrapper.sender_t>();

                for (int i = 0; i < numSenders; i++)
                {
                    IntPtr currentPtr = IntPtr.Add(sendersPtr, i * structSize);
                    var sender = Marshal.PtrToStructure<NDIWrapper.sender_t>(currentPtr);

                    string uuid = sender.p_uuid != IntPtr.Zero ? Marshal.PtrToStringAnsi(sender.p_uuid) ?? "" : "";
                    if (!string.IsNullOrEmpty(uuid))
                        seenUuids.Add(uuid);

                    // Subscribe to events if not already subscribed
                    if (!sender.events_subscribed && !string.IsNullOrEmpty(uuid) && !_subscribedSenders.Contains(uuid))
                    {
                        try
                        {
                            NDIWrapper.NDIlib_send_listener_subscribe_events(_listenerInstance, uuid);
                            _subscribedSenders.Add(uuid);
                        }
                        catch (Exception)
                        {
                            // Silently continue on subscription errors
                        }
                    }

                    var senderInfo = new Models.NDISenderStreamInfo
                    {
                        Uuid = uuid,
                        Name = sender.p_name != IntPtr.Zero ? Marshal.PtrToStringAnsi(sender.p_name) ?? "" : "",
                        Metadata = sender.p_metadata != IntPtr.Zero ? Marshal.PtrToStringAnsi(sender.p_metadata) ?? "" : "",
                        Address = sender.p_address != IntPtr.Zero ? Marshal.PtrToStringAnsi(sender.p_address) ?? "" : "",
                        Port = sender.port,
                        EventsSubscribed = sender.events_subscribed || _subscribedSenders.Contains(uuid),
                        Groups = new List<string>()
                    };

                    // Groups intentionally ignored: identity/routing does not rely on NDI groups.

                    // Populate event properties if we have them cached
                    if (_senderEvents.TryGetValue(uuid, out var events))
                    {
                        PopulateEventProperties(senderInfo, events);
                    }

                    senderList.Add(senderInfo);
                }
            }
            catch (Exception)
            {
                // Silently continue on enumeration errors
            }

            // Prune: any UUID we previously cached but did NOT see in this poll has left
            // the Discovery Server. Without this, a device-churning network leaks one
            // entry per device per session indefinitely.
            PruneDisappeared(seenUuids);

            return senderList;
        }

        private void PruneDisappeared(HashSet<string> seenUuids)
        {
            if (_subscribedSenders.Count > 0)
            {
                _subscribedSenders.RemoveWhere(uuid => !seenUuids.Contains(uuid));
            }
            if (_senderEvents.Count > 0)
            {
                // ToList() avoids modifying during enumeration.
                var stale = _senderEvents.Keys.Where(uuid => !seenUuids.Contains(uuid)).ToList();
                foreach (var uuid in stale)
                {
                    _senderEvents.Remove(uuid);
                }
            }
        }

        /// <summary>
        /// Poll for events from subscribed senders and update cache
        /// </summary>
        public void UpdateEvents(uint timeoutMs = 0)
        {
            if (_listenerInstance == IntPtr.Zero)
                return;

            try
            {
                uint numEvents = 0;
                IntPtr eventsPtr = NDIWrapper.NDIlib_send_listener_get_events(_listenerInstance, ref numEvents, timeoutMs);

                if (eventsPtr == IntPtr.Zero || numEvents == 0)
                    return;

                try
                {
                    int structSize = Marshal.SizeOf<NDIWrapper.listener_event>();

                    for (int i = 0; i < numEvents; i++)
                    {
                        IntPtr currentPtr = IntPtr.Add(eventsPtr, i * structSize);
                        var evt = Marshal.PtrToStructure<NDIWrapper.listener_event>(currentPtr);

                        string uuid = evt.p_uuid != IntPtr.Zero ? Marshal.PtrToStringAnsi(evt.p_uuid) ?? "" : "";
                        string name = evt.p_name != IntPtr.Zero ? Marshal.PtrToStringAnsi(evt.p_name) ?? "" : "";
                        string value = evt.p_value != IntPtr.Zero ? Marshal.PtrToStringAnsi(evt.p_value) ?? "" : "";

                        if (!string.IsNullOrEmpty(uuid) && !string.IsNullOrEmpty(name))
                        {
                            if (!_senderEvents.ContainsKey(uuid))
                            {
                                _senderEvents[uuid] = new Dictionary<string, string>();
                            }
                            _senderEvents[uuid][name] = value;
                        }
                    }
                }
                finally
                {
                    // Free events memory
                    NDIWrapper.NDIlib_send_listener_free_events(_listenerInstance, eventsPtr);
                }
            }
            catch (Exception)
            {
                // Silently continue on event update errors
            }
        }

        /// <summary>
        /// Populate sender info properties from event data
        /// </summary>
        private void PopulateEventProperties(Models.NDISenderStreamInfo senderInfo, Dictionary<string, string> events)
        {
            // Audio properties
            if (events.TryGetValue("audio-codec", out var audioCodec))
                senderInfo.AudioCodec = audioCodec;

            if (events.TryGetValue("audio-channels", out var audioChannels) && int.TryParse(audioChannels, out var channels))
                senderInfo.AudioChannels = channels;

            if (events.TryGetValue("audio-samplerate", out var audioSampleRate) && int.TryParse(audioSampleRate, out var sampleRate))
                senderInfo.AudioSampleRate = sampleRate;

            if (events.TryGetValue("audio-frames-sent", out var audioFrames) && long.TryParse(audioFrames, out var aFrames))
                senderInfo.AudioFramesSent = aFrames;

            if (events.TryGetValue("audio-bytes-sent", out var audioBytes) && long.TryParse(audioBytes, out var aBytes))
                senderInfo.AudioBytesSent = aBytes;

            // Video properties
            if (events.TryGetValue("video-codec", out var videoCodec))
                senderInfo.VideoCodec = videoCodec;

            if (events.TryGetValue("video-resolution", out var videoResolution))
                senderInfo.VideoResolution = videoResolution;

            if (events.TryGetValue("video-framerate", out var videoFrameRate))
                senderInfo.VideoFrameRate = videoFrameRate;

            if (events.TryGetValue("video-frames-sent", out var videoFrames) && long.TryParse(videoFrames, out var vFrames))
                senderInfo.VideoFramesSent = vFrames;

            if (events.TryGetValue("video-bytes-sent", out var videoBytes) && long.TryParse(videoBytes, out var vBytes))
                senderInfo.VideoBytesSent = vBytes;

            // Connection count
            if (events.TryGetValue("connection-count", out var connCount) && int.TryParse(connCount, out var count))
                senderInfo.ConnectionCount = count;
        }

        /// <summary>
        /// Wait for changes in the sender list
        /// </summary>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if changes detected within timeout</returns>
        public bool WaitForChanges(uint timeoutMs = 5000)
        {
            if (_listenerInstance == IntPtr.Zero)
                return false;

            return NDIWrapper.NDIlib_send_listener_wait_for_senders(_listenerInstance, timeoutMs);
        }

        public void Dispose()
        {
            if (_listenerInstance != IntPtr.Zero)
            {
                NDIWrapper.NDIlib_send_listener_destroy(_listenerInstance);
                _listenerInstance = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }
    }
}
