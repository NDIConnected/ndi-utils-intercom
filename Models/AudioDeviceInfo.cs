namespace NDIIntercom.Models
{
    public class AudioDeviceInfo
    {
        public string DeviceId { get; set; }
        public string FriendlyName { get; set; }
        public bool IsInput { get; set; }
        public bool IsOutput { get; set; }

        public override string ToString()
        {
            return FriendlyName;
        }
    }
}
