using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BigFilesSorting.Startup
{
    public class StartupService : IStartupService
    {
        private readonly ILogger<StartupService> _logger;
        private readonly IConfiguration _config;

        public StartupService(ILogger<StartupService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public void Run()
        {
            _logger.LogInformation("Startup service started");
        }
    }
}
