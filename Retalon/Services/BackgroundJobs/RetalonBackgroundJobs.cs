using Serilog;

namespace Retalon.Services.BackgroundJobs;

public class RetalonBackgroundJobs
{
    public void TestJob()
    {
        Log.Information("Hangfire TestJob executed successfully at {Time}",
            DateTime.UtcNow);
    }
}