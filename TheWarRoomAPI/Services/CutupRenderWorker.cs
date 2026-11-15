namespace TheWarRoomAPI.Services
{
    // Runs for as long as the API runs and makes the videos of cutups, one at a time,
    // in the order coaches asked for them. Making a video takes too long to be done inside a request.
    public class CutupRenderWorker : BackgroundService
    {
        private static readonly TimeSpan LookAgainAfter = TimeSpan.FromSeconds(2);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CutupRenderWorker> _logger;

        public CutupRenderWorker(IServiceScopeFactory scopeFactory, ILogger<CutupRenderWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // What was being made when the API last stopped is made again
                using (var scope = _scopeFactory.CreateScope())
                {
                    int requeued = await scope.ServiceProvider.GetRequiredService<CutupRenderer>().RequeueInterruptedAsync(stoppingToken);
                    if (requeued > 0)
                    {
                        _logger.LogInformation("{Count} cutup(s) were being made when the API stopped and are back in the queue", requeued);
                    }
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    bool worked = false;

                    try
                    {
                        // Every cutup gets a scope of its own, and with it its own database context
                        using var scope = _scopeFactory.CreateScope();
                        worked = await scope.ServiceProvider.GetRequiredService<CutupRenderer>().RenderNextAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "The cutup queue could not be read");
                    }

                    if (!worked)
                    {
                        await Task.Delay(LookAgainAfter, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // The API is stopping
            }
        }
    }
}
