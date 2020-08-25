using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Driver
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebJobs(b =>
                {
                    b.AddHttp();
                })
                .ConfigureLogging((context, b) =>
                {
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
