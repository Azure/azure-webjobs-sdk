using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Driver
{
    public static class Functions
    {
        [NoAutomaticTrigger]
        public static Task Simple(ILogger logger)
        {
            logger.LogInformation("Hello from WebJobs!");

            return Task.CompletedTask;
        }

        public static IActionResult Http([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new ObjectResult("Hello from WebJobs!");
        }
    }
}
