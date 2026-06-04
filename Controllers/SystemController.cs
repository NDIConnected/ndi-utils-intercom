using Microsoft.AspNetCore.Mvc;
using NDIIntercom.Core;
using NDIIntercom.Models;

namespace NDIIntercom.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : ControllerBase
    {
        private readonly IntercomEngine _intercomEngine;
        private static readonly string AppVersion =
            typeof(SystemController).Assembly.GetName().Version?.ToString() ?? "unknown";

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
                version = AppVersion
            });
        }

        // GET api/system/info
        [HttpGet("info")]
        public ActionResult GetInfo()
        {
            return Ok(new
            {
                version = AppVersion,
                name = IntercomRuntime.Product.ProductDisplayName,
                channels = _intercomEngine.GetChannels().Count,
                running = _intercomEngine.IsRunning
            });
        }
    }
}
