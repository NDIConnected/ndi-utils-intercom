using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    /// <summary>
    /// NDI RecvListener - Monitors receivers on the Discovery Server and subscribes to events
    /// </summary>
    public class NDIRecvListener : IDisposable
    {
        private IntPtr _listenerInstance = IntPtr.Zero;

        /// <summary>
        /// Initialize the RecvListener with vendor credentials for event monitoring
        /// </summary>
        /// <param name="discoveryServerUrl">Discovery Server URL (null for default)</param>
        /// <returns>True if initialization succeeded</returns>
        public bool Initialize(string? discoveryServerUrl = null)
        {
            var listenerSettings = new NDIWrapper.recv_listener_create_t
            {
                p_url_address = IntPtr.Zero
            };

            _listenerInstance = NDIWrapper.NDIlib_recv_listener_create(ref listenerSettings);

            if (_listenerInstance == IntPtr.Zero)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get list of receivers registered on the Discovery Server
        /// Automatically subscribes to events for new receivers
        /// </summary>
        /// <returns>List of receiver information</returns>
        public List<NDIReceiverInfo> GetReceivers()
        {
            var receiverList = new List<NDIReceiverInfo>();

            if (_listenerInstance == IntPtr.Zero)
                return receiverList;

            uint numReceivers = 0;
            IntPtr receiversPtr = NDIWrapper.NDIlib_recv_listener_get_receivers(_listenerInstance, ref numReceivers);

            if (receiversPtr == IntPtr.Zero || numReceivers == 0)
                return receiverList;

            try
            {
                int structSize = Marshal.SizeOf<NDIWrapper.receiver_t>();

                for (int i = 0; i < numReceivers; i++)
                {
                    try
                    {
                        IntPtr currentPtr = IntPtr.Add(receiversPtr, i * structSize);
                        var receiver = Marshal.PtrToStructure<NDIWrapper.receiver_t>(currentPtr);

                        // Safe string marshaling with additional null checks
                        string uuid = "";
                        string name = "";
                        string inputUuid = "";

                        try
                        {
                            uuid = receiver.p_uuid != IntPtr.Zero ? NdiNativeStrings.PtrToStringUtf8(receiver.p_uuid) ?? "" : "";
                        }
                        catch { }

                        try
                        {
                            name = receiver.p_name != IntPtr.Zero ? NdiNativeStrings.PtrToStringUtf8(receiver.p_name) ?? "" : "";
                        }
                        catch { }

                        try
                        {
                            inputUuid = receiver.p_input_uuid != IntPtr.Zero ? NdiNativeStrings.PtrToStringUtf8(receiver.p_input_uuid) ?? "" : "";
                        }
                        catch { }

                    var receiverInfo = new NDIReceiverInfo
                    {
                        Id = uuid,
                        Name = name,
                        InputUuid = !string.IsNullOrEmpty(inputUuid) ? inputUuid : null,
                        IsConnected = !string.IsNullOrEmpty(inputUuid),
                        LastSeen = DateTime.Now
                    };

                        receiverList.Add(receiverInfo);

                        // AUTOMATIC EVENT SUBSCRIPTION
                        // Subscribe to events if not already subscribed (events_subscribed == 0)
                        if (receiver.events_subscribed == 0 && !string.IsNullOrEmpty(uuid))
                        {
                            SubscribeEvents(uuid);
                        }
                    }
                    catch (Exception)
                    {
                        // Silently continue on receiver processing errors
                    }
                }
            }
            catch (Exception)
            {
                // Silently continue on receiver enumeration errors
            }

            return receiverList;
        }

        /// <summary>
        /// Subscribe to events from a specific receiver
        /// </summary>
        /// <param name="receiverUuid">UUID of the receiver</param>
        public void SubscribeEvents(string receiverUuid)
        {
            if (_listenerInstance == IntPtr.Zero || string.IsNullOrEmpty(receiverUuid))
                return;

            try
            {
                NDIWrapper.NDIlib_recv_listener_subscribe_events(_listenerInstance, receiverUuid);
            }
            catch (Exception)
            {
                // Silently continue on subscription errors
            }
        }

        /// <summary>
        /// Unsubscribe from events from a specific receiver
        /// </summary>
        /// <param name="receiverUuid">UUID of the receiver</param>
        public void UnsubscribeEvents(string receiverUuid)
        {
            if (_listenerInstance == IntPtr.Zero || string.IsNullOrEmpty(receiverUuid))
                return;

            try
            {
                NDIWrapper.NDIlib_recv_listener_unsubscribe_events(_listenerInstance, receiverUuid);
            }
            catch (Exception)
            {
                // Silently continue on unsubscription errors
            }
        }

        /// <summary>
        /// Get pending events from subscribed receivers
        /// </summary>
        /// <param name="timeoutMs">Timeout in milliseconds (0 = don't wait)</param>
        /// <returns>List of receiver events</returns>
        public List<NDIReceiverEvent> GetEvents(uint timeoutMs = 0)
        {
            var eventList = new List<NDIReceiverEvent>();

            if (_listenerInstance == IntPtr.Zero)
                return eventList;

            uint numEvents = 0;
            IntPtr eventsPtr = NDIWrapper.NDIlib_recv_listener_get_events(_listenerInstance, ref numEvents, timeoutMs);

            if (eventsPtr == IntPtr.Zero || numEvents == 0)
                return eventList;

            try
            {
                int structSize = Marshal.SizeOf<NDIWrapper.listener_event>();

                for (int i = 0; i < numEvents; i++)
                {
                    IntPtr currentPtr = IntPtr.Add(eventsPtr, i * structSize);
                    var evt = Marshal.PtrToStructure<NDIWrapper.listener_event>(currentPtr);

                    string uuid = evt.p_uuid != IntPtr.Zero ? NdiNativeStrings.PtrToStringUtf8(evt.p_uuid) ?? "" : "";
                    string name = evt.p_name != IntPtr.Zero ? NdiNativeStrings.PtrToStringUtf8(evt.p_name) ?? "" : "";
                    string value = evt.p_value != IntPtr.Zero ? NdiNativeStrings.PtrToStringUtf8(evt.p_value) ?? "" : "";

                    eventList.Add(new NDIReceiverEvent
                    {
                        ReceiverUuid = uuid,
                        EventName = name,
                        EventValue = value
                    });
                }
            }
            catch (Exception)
            {
                // Silently continue on event retrieval errors
            }
            finally
            {
                // CRITICAL: Always free the events memory
                NDIWrapper.NDIlib_recv_listener_free_events(_listenerInstance, eventsPtr);
            }

            return eventList;
        }

        public void Dispose()
        {
            if (_listenerInstance != IntPtr.Zero)
            {
                NDIWrapper.NDIlib_recv_listener_destroy(_listenerInstance);
                _listenerInstance = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }
    }
}
