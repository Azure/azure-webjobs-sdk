using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;

namespace Driver.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class Invoke : ControllerBase
    {
        private IJobHost _jobHost;

        public Invoke(IJobHost jobHost)
        {
            _jobHost = jobHost;
        }

        [HttpGet]
        public async Task<IActionResult> Get(string scenario)
        {
            IDictionary<string, object> arguments = null;

            if (string.Compare(scenario, nameof(Functions.Http), StringComparison.OrdinalIgnoreCase) == 0)
            {
                arguments = new Dictionary<string, object>
                {
                    { "req", HttpContext.Request }
                };
            }

            await _jobHost.CallAsync(scenario, arguments);

            return new OkResult();
        }
    }
}
