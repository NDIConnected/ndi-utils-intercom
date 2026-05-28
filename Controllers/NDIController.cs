using Microsoft.AspNetCore.Mvc;
using NDIIntercom.Core;
using NDIIntercom.Models;

namespace NDIIntercom.Controllers
{
    [ApiController]
    [Route("api/ndi")]
    public class NDIController : ControllerBase
    {
        private readonly NDIDiscoveryService _discoveryService;
        private readonly NDISenderDiscoveryService _senderDiscoveryService;

        public NDIController(NDIDiscoveryService discoveryService, NDISenderDiscoveryService senderDiscoveryService)
        {
            _discoveryService = discoveryService;
            _senderDiscoveryService = senderDiscoveryService;
        }

        /// <summary>
        /// Get all receivers registered on the NDI Discovery Server
        /// </summary>
        /// <returns>List of receivers with current source information</returns>
        [HttpGet("receivers")]
        public ActionResult<List<NDIReceiverInfo>> GetReceivers()
        {
            var receivers = _discoveryService.GetReceivers();
            return Ok(receivers);
        }

        /// <summary>
        /// Get a specific receiver by UUID
        /// </summary>
        /// <param name="id">Receiver UUID</param>
        /// <returns>Receiver information or 404 if not found</returns>
        [HttpGet("receivers/{id}")]
        public ActionResult<NDIReceiverInfo> GetReceiverById(string id)
        {
            var receiver = _discoveryService.GetReceiverById(id);
            if (receiver == null)
            {
                return NotFound(new { error = "Receiver not found" });
            }
            return Ok(receiver);
        }

        /// <summary>
        /// Get information about all NDI senders registered on the Discovery Server
        /// </summary>
        /// <returns>List of sender stream information including address, port, metadata, and groups</returns>
        [HttpGet("senders")]
        public ActionResult<List<NDISenderStreamInfo>> GetSenders()
        {
            var senders = _senderDiscoveryService.GetSenders();
            return Ok(senders);
        }
    }
}
