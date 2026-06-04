using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NDIIntercom.Core;
using NDIIntercom.Models;
using NDIIntercom.Hubs;

namespace NDIIntercom.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IntercomController : ControllerBase
    {
        private readonly IntercomEngine _intercomEngine;
        private readonly IHubContext<IntercomHub> _hubContext;

        public IntercomController(IntercomEngine intercomEngine, IHubContext<IntercomHub> hubContext)
        {
            _intercomEngine = intercomEngine;
            _hubContext = hubContext;
        }

        // GET api/intercom/channels
        [HttpGet("channels")]
        public ActionResult<List<ChannelState>> GetChannels()
        {
            return Ok(_intercomEngine.GetChannels());
        }

        // GET api/intercom/channels/1
        [HttpGet("channels/{channelNumber}")]
        public ActionResult<ChannelState> GetChannel(int channelNumber)
        {
            var channel = _intercomEngine.GetChannels().FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel == null)
                return NotFound();

            return Ok(channel);
        }

        // POST api/intercom/channels/1/talk/toggle
        [HttpPost("channels/{channelNumber}/talk/toggle")]
        public async Task<ActionResult> ToggleTalk(int channelNumber)
        {
            var channel = _intercomEngine.GetChannels().FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel == null)
                return NotFound();

            channel.TalkEnabled = !channel.TalkEnabled;
            _intercomEngine.UpdateAsioChannelStates();
            await _hubContext.Clients.All.SendAsync("ChannelUpdated", channel);
            return Ok(new { channelNumber, talkEnabled = channel.TalkEnabled });
        }

        // POST api/intercom/channels/1/talk/on
        [HttpPost("channels/{channelNumber}/talk/on")]
        public async Task<ActionResult> SetTalkOn(int channelNumber)
        {
            var channel = _intercomEngine.GetChannels().FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel == null)
                return NotFound();

            channel.TalkEnabled = true;
            _intercomEngine.UpdateAsioChannelStates();
            await _hubContext.Clients.All.SendAsync("ChannelUpdated", channel);
            return Ok(new { channelNumber, talkEnabled = channel.TalkEnabled });
        }

        // POST api/intercom/channels/1/talk/off
        [HttpPost("channels/{channelNumber}/talk/off")]
        public async Task<ActionResult> SetTalkOff(int channelNumber)
        {
            var channel = _intercomEngine.GetChannels().FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel == null)
                return NotFound();

            channel.TalkEnabled = false;
            _intercomEngine.UpdateAsioChannelStates();
            await _hubContext.Clients.All.SendAsync("ChannelUpdated", channel);
            return Ok(new { channelNumber, talkEnabled = channel.TalkEnabled });
        }

        // POST api/intercom/channels/1/listen/toggle
        [HttpPost("channels/{channelNumber}/listen/toggle")]
        public async Task<ActionResult> ToggleListen(int channelNumber)
        {
            var channel = _intercomEngine.GetChannels().FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel == null)
                return NotFound();

            channel.ListenEnabled = !channel.ListenEnabled;
            _intercomEngine.UpdateAsioChannelStates();
            await _hubContext.Clients.All.SendAsync("ChannelUpdated", channel);
            return Ok(new { channelNumber, listenEnabled = channel.ListenEnabled });
        }

        // POST api/intercom/channels/1/listen/on
        [HttpPost("channels/{channelNumber}/listen/on")]
        public async Task<ActionResult> SetListenOn(int channelNumber)
        {
            var channel = _intercomEngine.GetChannels().FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel == null)
                return NotFound();

            channel.ListenEnabled = true;
            _intercomEngine.UpdateAsioChannelStates();
            await _hubContext.Clients.All.SendAsync("ChannelUpdated", channel);
            return Ok(new { channelNumber, listenEnabled = channel.ListenEnabled });
        }

        // POST api/intercom/channels/1/listen/off
        [HttpPost("channels/{channelNumber}/listen/off")]
        public async Task<ActionResult> SetListenOff(int channelNumber)
        {
            var channel = _intercomEngine.GetChannels().FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel == null)
                return NotFound();

            channel.ListenEnabled = false;
            _intercomEngine.UpdateAsioChannelStates();
            await _hubContext.Clients.All.SendAsync("ChannelUpdated", channel);
            return Ok(new { channelNumber, listenEnabled = channel.ListenEnabled });
        }

        // POST api/intercom/channels/1/group
        [HttpPost("channels/{channelNumber}/group")]
        public async Task<ActionResult> SetGroup(int channelNumber, [FromBody] SetGroupRequest request)
        {
            var channel = _intercomEngine.GetChannels().FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel == null)
                return NotFound();

            if (request.Group < 0 || request.Group > 4)
                return BadRequest("Group must be between 0 and 4");

            channel.IntercomGroup = request.Group;
            _intercomEngine.SaveCurrentConfiguration();

            await _hubContext.Clients.All.SendAsync("ChannelUpdated", channel);
            return Ok(new { channelNumber, group = channel.IntercomGroup });
        }

        // POST api/intercom/channels/1/group/rotate
        [HttpPost("channels/{channelNumber}/group/rotate")]
        public async Task<ActionResult> RotateGroup(int channelNumber)
        {
            var channel = _intercomEngine.GetChannels().FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel == null)
                return NotFound();

            channel.IntercomGroup = (channel.IntercomGroup + 1) % 5;
            _intercomEngine.SaveCurrentConfiguration();

            await _hubContext.Clients.All.SendAsync("ChannelUpdated", channel);
            return Ok(new { channelNumber, group = channel.IntercomGroup });
        }

        // POST api/intercom/reset
        [HttpPost("reset")]
        public async Task<ActionResult> ResetAll()
        {
            var channels = _intercomEngine.GetChannels();
            foreach (var channel in channels)
            {
                channel.TalkEnabled = false;
                channel.ListenEnabled = false;
                await _hubContext.Clients.All.SendAsync("ChannelUpdated", channel);
            }
            _intercomEngine.UpdateAsioChannelStates();

            return Ok(new { message = "All channels reset" });
        }

        // POST api/intercom/config/export
        [HttpPost("config/export")]
        public ActionResult ExportConfiguration([FromBody] ExportConfigRequest request)
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                ConfigManager.ExportConfig(config, request.FilePath);
                return Ok(new { message = "Configuration exported", filePath = request.FilePath });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // POST api/intercom/config/import
        [HttpPost("config/import")]
        public async Task<ActionResult> ImportConfiguration([FromBody] ImportConfigRequest request)
        {
            try
            {
                var config = ConfigManager.ImportConfig(request.FilePath);
                ConfigManager.SaveConfig(config);
                _intercomEngine.ApplyConfig(config);
                await _hubContext.Clients.All.SendAsync("ConfigurationImported");
                return Ok(new { message = "Configuration imported" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // POST api/intercom/presets
        [HttpPost("presets")]
        public ActionResult SavePreset([FromBody] SavePresetRequest request)
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                PresetManager.SavePreset(request.PresetName, config);
                return Ok(new { message = "Preset saved", presetName = request.PresetName });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // GET api/intercom/presets
        [HttpGet("presets")]
        public ActionResult<List<string>> GetPresets()
        {
            return Ok(PresetManager.ListPresets());
        }

        // POST api/intercom/presets/{presetName}/load
        [HttpPost("presets/{presetName}/load")]
        public async Task<ActionResult> LoadPreset(string presetName)
        {
            try
            {
                var config = PresetManager.LoadPreset(presetName);
                ConfigManager.SaveConfig(config);
                _intercomEngine.ApplyConfig(config);
                await _hubContext.Clients.All.SendAsync("PresetLoaded", presetName);
                return Ok(new { message = "Preset loaded", presetName });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // DELETE api/intercom/presets/{presetName}
        [HttpDelete("presets/{presetName}")]
        public ActionResult DeletePreset(string presetName)
        {
            try
            {
                PresetManager.DeletePreset(presetName);
                return Ok(new { message = "Preset deleted", presetName });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class SetGroupRequest
    {
        public int Group { get; set; }
    }

    public class ExportConfigRequest
    {
        /// <summary>File name only (stored under the per-product Exports folder).</summary>
        public string FilePath { get; set; }
    }

    public class ImportConfigRequest
    {
        /// <summary>File name only (read from the per-product Exports folder).</summary>
        public string FilePath { get; set; }
    }

    public class SavePresetRequest
    {
        public string PresetName { get; set; }
    }
}
