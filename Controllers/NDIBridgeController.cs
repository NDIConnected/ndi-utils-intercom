using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace NDIIntercom.Controllers
{
    /// <summary>
    /// Controller for NDI Bridge Service integration.
    /// Provides specific endpoints for start/stop/status and a generic proxy
    /// for all Bridge configuration settings (buffer, HX encoding, encryption, etc.).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class NDIBridgeController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        // Whitelist of allowed proxy paths for security
        private static readonly HashSet<string> AllowedProxyPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            // Global
            "auto_start", "gpu_list",
            "connection_test_start", "connection_test_stop", "connection_test_bitrate",
            // Host
            "host/bridge_name", "host/buffer", "host/groups", "host/port",
            "host/hx_encoder", "host/hx_gpu", "host/hx_output", "host/hx_quality",
            "host/hx_ndi4_compatibility_mode",
            "host/set_encryption_key", "host/clear_encryption_key",
            "host/bandwidth_message", "host/gpu_limit", "host/status_message",
            // Join
            "join/bridge_name", "join/buffer", "join/groups", "join/port", "join/ip_address",
            "join/hx_encoder", "join/hx_gpu", "join/hx_output", "join/hx_quality",
            "join/hx_ndi4_compatibility_mode",
            "join/set_encryption_key", "join/clear_encryption_key",
            "join/bandwidth_message", "join/gpu_limit", "join/status_message",
            // Local
            "local/bridge_name", "local/groups", "local/send_groups",
            "local/hx_gpu", "local/hx_output", "local/hx_quality",
            "local/hx_ndi4_compatibility_mode",
            "local/status_message"
        };

        public NDIBridgeController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private string GetBridgeUrl()
        {
            return _configuration["NDIBridge:Url"] ?? "http://localhost:8080";
        }

        private string? GetApiKey()
        {
            return _configuration["NDIBridge:ApiKey"];
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var apiKey = GetApiKey();
            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            }
            return client;
        }

        // ========== GENERIC PROXY ==========

        /// <summary>
        /// Generic GET proxy for Bridge API settings.
        /// Forwards to GET {bridgeUrl}/api/{path} and returns the content value.
        /// </summary>
        [HttpGet("proxy/{**path}")]
        public async Task<ActionResult> ProxyGet(string path)
        {
            if (!AllowedProxyPaths.Contains(path))
                return BadRequest(new { success = false, error = $"Path '{path}' is not allowed." });

            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{GetBridgeUrl()}/api/{path}");
                var rawContent = await response.Content.ReadAsStringAsync();

                // Parse using JsonDocument to preserve the original content type (array, object, string, number)
                using var doc = JsonDocument.Parse(rawContent);
                var root = doc.RootElement;

                bool success = root.TryGetProperty("success", out var successEl) && successEl.GetBoolean();
                string? error = root.TryGetProperty("error", out var errorEl) && errorEl.ValueKind != JsonValueKind.Null
                    ? errorEl.GetString() : null;

                // Extract content - preserve its original type
                object? value = null;
                if (root.TryGetProperty("content", out var contentEl))
                {
                    switch (contentEl.ValueKind)
                    {
                        case JsonValueKind.Array:
                            // Return as JSON array string so JS can JSON.parse() it
                            value = contentEl.GetRawText();
                            break;
                        case JsonValueKind.String:
                            value = contentEl.GetString();
                            break;
                        case JsonValueKind.Number:
                            value = contentEl.GetRawText();
                            break;
                        case JsonValueKind.True:
                            value = "true";
                            break;
                        case JsonValueKind.False:
                            value = "false";
                            break;
                        default:
                            value = contentEl.GetRawText();
                            break;
                    }
                }

                return Ok(new { success, value = value ?? "", error });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Generic POST proxy for Bridge API settings.
        /// Forwards to POST {bridgeUrl}/api/{path} with {"value":"..."} body.
        /// </summary>
        [HttpPost("proxy/{**path}")]
        public async Task<ActionResult> ProxyPost(string path, [FromBody] GenericValueRequest request)
        {
            if (!AllowedProxyPaths.Contains(path))
                return BadRequest(new { success = false, error = $"Path '{path}' is not allowed." });

            try
            {
                var client = CreateClient();
                var body = JsonSerializer.Serialize(new { value = request.Value });
                var response = await client.PostAsync(
                    $"{GetBridgeUrl()}/api/{path}",
                    new StringContent(body, Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<BridgeApiResponse>(content);

                return Ok(new
                {
                    success = json?.Success ?? false,
                    value = json?.Content?.ToString() ?? "",
                    error = json?.Error
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ========== STATUS & CONTROL ==========

        /// <summary>
        /// Test connection to NDI Bridge Service
        /// </summary>
        [HttpGet("test")]
        public async Task<ActionResult> TestConnection()
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{GetBridgeUrl()}/api/uptime");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Ok(new { connected = true, message = "Connected to NDI Bridge Service", uptime = content });
                }

                return Ok(new { connected = false, message = "NDI Bridge Service not responding" });
            }
            catch (Exception ex)
            {
                return Ok(new { connected = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get NDI Bridge Service status
        /// </summary>
        [HttpGet("status")]
        public async Task<ActionResult> GetStatus()
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{GetBridgeUrl()}/api/is_running");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonSerializer.Deserialize<BridgeApiResponse>(content);

                    return Ok(new { isRunning = json?.Content ?? false, connected = true });
                }

                return Ok(new { isRunning = false, connected = false });
            }
            catch
            {
                return Ok(new { isRunning = false, connected = false });
            }
        }

        /// <summary>
        /// Get current run mode (HOST, JOIN, LOCAL, or NONE)
        /// </summary>
        [HttpGet("runmode")]
        public async Task<ActionResult> GetRunMode()
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{GetBridgeUrl()}/api/runmode");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                    string mode = MapRunMode(json?.Content?.ToString());

                    return Ok(new { mode, connected = true });
                }

                return Ok(new { mode = "NONE", connected = false });
            }
            catch
            {
                return Ok(new { mode = "NONE", connected = false });
            }
        }

        private static string MapRunMode(string? value)
        {
            return value?.Trim().ToUpper() switch
            {
                "0" => "NONE",
                "1" or "HOST" => "HOST",
                "2" or "JOIN" => "JOIN",
                "3" or "LOCAL" => "LOCAL",
                "NONE" => "NONE",
                null or "" => "NONE",
                _ => value.ToUpper()
            };
        }

        // ========== START / STOP ==========

        [HttpPost("host/start")]
        public Task<ActionResult> StartHost() => PostCommand("host/start");

        [HttpPost("host/stop")]
        public Task<ActionResult> StopHost() => PostCommand("host/stop");

        [HttpPost("join/start")]
        public Task<ActionResult> StartJoin() => PostCommand("join/start");

        [HttpPost("join/stop")]
        public Task<ActionResult> StopJoin() => PostCommand("join/stop");

        [HttpPost("local/start")]
        public Task<ActionResult> StartLocal() => PostCommand("local/start");

        [HttpPost("local/stop")]
        public Task<ActionResult> StopLocal() => PostCommand("local/stop");

        private async Task<ActionResult> PostCommand(string apiPath)
        {
            try
            {
                var client = CreateClient();
                var response = await client.PostAsync(
                    $"{GetBridgeUrl()}/api/{apiPath}",
                    new StringContent("{}", Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<BridgeApiResponse>(content);

                return Ok(new
                {
                    success = json?.Success ?? false,
                    message = json?.Success == true
                        ? json?.Content?.ToString()
                        : json?.Error ?? json?.Content?.ToString()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ========== LEGACY SPECIFIC ENDPOINTS (backward compatibility) ==========

        [HttpGet("host/groups")]
        public async Task<ActionResult> GetHostGroups()
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{GetBridgeUrl()}/api/host/groups");
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { groups = json?.Content?.ToString() ?? "" });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPost("host/groups")]
        public async Task<ActionResult> SetHostGroups([FromBody] SetGroupsRequest request)
        {
            try
            {
                var client = CreateClient();
                var json = JsonSerializer.Serialize(new { value = request.Groups });
                var response = await client.PostAsync($"{GetBridgeUrl()}/api/host/groups", new StringContent(json, Encoding.UTF8, "application/json"));
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { success = result?.Success ?? false, groups = result?.Content?.ToString() });
            }
            catch (Exception ex) { return BadRequest(new { success = false, error = ex.Message }); }
        }

        [HttpGet("host/port")]
        public async Task<ActionResult> GetHostPort()
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{GetBridgeUrl()}/api/host/port");
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { port = json?.Content?.ToString() ?? "5990" });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPost("host/port")]
        public async Task<ActionResult> SetHostPort([FromBody] SetPortRequest request)
        {
            try
            {
                var client = CreateClient();
                var json = JsonSerializer.Serialize(new { value = request.Port.ToString() });
                var response = await client.PostAsync($"{GetBridgeUrl()}/api/host/port", new StringContent(json, Encoding.UTF8, "application/json"));
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { success = result?.Success ?? false, port = result?.Content?.ToString() });
            }
            catch (Exception ex) { return BadRequest(new { success = false, error = ex.Message }); }
        }

        [HttpGet("join/ip")]
        public async Task<ActionResult> GetJoinIp()
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{GetBridgeUrl()}/api/join/ip_address");
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { ip = json?.Content?.ToString() ?? "" });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPost("join/ip")]
        public async Task<ActionResult> SetJoinIp([FromBody] SetIpRequest request)
        {
            try
            {
                var client = CreateClient();
                var json = JsonSerializer.Serialize(new { value = request.IpAddress });
                var response = await client.PostAsync($"{GetBridgeUrl()}/api/join/ip_address", new StringContent(json, Encoding.UTF8, "application/json"));
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { success = result?.Success ?? false, ip = result?.Content?.ToString() });
            }
            catch (Exception ex) { return BadRequest(new { success = false, error = ex.Message }); }
        }

        [HttpGet("join/port")]
        public async Task<ActionResult> GetJoinPort()
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{GetBridgeUrl()}/api/join/port");
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { port = json?.Content?.ToString() ?? "5990" });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPost("join/port")]
        public async Task<ActionResult> SetJoinPort([FromBody] SetPortRequest request)
        {
            try
            {
                var client = CreateClient();
                var json = JsonSerializer.Serialize(new { value = request.Port.ToString() });
                var response = await client.PostAsync($"{GetBridgeUrl()}/api/join/port", new StringContent(json, Encoding.UTF8, "application/json"));
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { success = result?.Success ?? false, port = result?.Content?.ToString() });
            }
            catch (Exception ex) { return BadRequest(new { success = false, error = ex.Message }); }
        }

        [HttpGet("join/groups")]
        public async Task<ActionResult> GetJoinGroups()
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{GetBridgeUrl()}/api/join/groups");
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { groups = json?.Content?.ToString() ?? "" });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPost("join/groups")]
        public async Task<ActionResult> SetJoinGroups([FromBody] SetGroupsRequest request)
        {
            try
            {
                var client = CreateClient();
                var json = JsonSerializer.Serialize(new { value = request.Groups });
                var response = await client.PostAsync($"{GetBridgeUrl()}/api/join/groups", new StringContent(json, Encoding.UTF8, "application/json"));
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { success = result?.Success ?? false, groups = result?.Content?.ToString() });
            }
            catch (Exception ex) { return BadRequest(new { success = false, error = ex.Message }); }
        }

        [HttpGet("local/groups")]
        public async Task<ActionResult> GetLocalGroups()
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"{GetBridgeUrl()}/api/local/groups");
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { groups = json?.Content?.ToString() ?? "" });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPost("local/groups")]
        public async Task<ActionResult> SetLocalGroups([FromBody] SetGroupsRequest request)
        {
            try
            {
                var client = CreateClient();
                var json = JsonSerializer.Serialize(new { value = request.Groups });
                var response = await client.PostAsync($"{GetBridgeUrl()}/api/local/groups", new StringContent(json, Encoding.UTF8, "application/json"));
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BridgeApiResponse>(content);
                return Ok(new { success = result?.Success ?? false, groups = result?.Content?.ToString() });
            }
            catch (Exception ex) { return BadRequest(new { success = false, error = ex.Message }); }
        }
    }

    // ========== REQUEST / RESPONSE MODELS ==========

    public class BridgeApiResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("success")]
        public bool Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public string? Error { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public object? Content { get; set; }
    }

    public class GenericValueRequest
    {
        public string Value { get; set; } = "";
    }

    public class SetGroupsRequest
    {
        public string Groups { get; set; } = "";
    }

    public class SetPortRequest
    {
        public int Port { get; set; }
    }

    public class SetIpRequest
    {
        public string IpAddress { get; set; } = "";
    }
}
