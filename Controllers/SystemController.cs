using Microsoft.AspNetCore.Mvc;
using NDIIntercom.Core;

namespace NDIIntercom.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : ControllerBase
    {
        private readonly IntercomEngine _intercomEngine;

        public SystemController(IntercomEngine intercomEngine)
        {
            _intercomEngine = intercomEngine;
        }

        // GET api/system/status
        [HttpGet("status")]
        public ActionResult GetStatus()
        {
            bool isRunning = _intercomEngine.IsRunning;

            return Ok(new
            {
                running = isRunning,
                isRunning = isRunning,
                timestamp = DateTime.UtcNow,
                version = "1.4.0"
            });
        }

        // GET api/system/info
        [HttpGet("info")]
        public ActionResult GetInfo()
        {
            return Ok(new
            {
                version = "1.4.0",
                name = "NDI Intercom16",
                channels = _intercomEngine.GetChannels().Count,
                running = _intercomEngine.IsRunning
            });
        }
    }
}
