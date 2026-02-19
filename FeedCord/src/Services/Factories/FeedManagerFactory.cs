using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeedCord.Services.Factories
{
    public class FeedManagerFactory : IFeedManagerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public FeedManagerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IFeedManager Create(Config config, ILogAggregator logAggregator)
        {
            var logger = _serviceProvider.GetRequiredService<ILogger<PostFilterService>>();
            var postFilterService = new PostFilterService(logger, config);

            return ActivatorUtilities.CreateInstance<FeedManager>(_serviceProvider, config, logAggregator, postFilterService);
        }
    }
}
