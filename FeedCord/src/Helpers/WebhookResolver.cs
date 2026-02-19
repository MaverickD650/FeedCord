using FeedCord.Common;

namespace FeedCord.Helpers
{
    /// <summary>
    /// Resolves webhook URLs from environment variables when prefixed with "env:"
    /// Allows users to define webhooks via environment variables instead of hardcoding in config
    /// </summary>
    public static class WebhookResolver
    {
        private const string ENV_PREFIX = "env:";

        /// <summary>
        /// Resolves all webhook URLs in the provided configurations.
        /// If a webhook URL starts with "env:", it extracts the environment variable name and retrieves its value.
        /// </summary>
        /// <param name="configs">List of configurations to resolve webhooks for</param>
        /// <param name="logAction">Optional action for logging resolved webhooks</param>
        /// <exception cref="InvalidOperationException">Thrown if an environment variable is referenced but not set</exception>
        public static void ResolveWebhooks(List<Config> configs, Action<string>? logAction = null)
        {
            if (configs == null || configs.Count == 0)
                return;

            foreach (var config in configs)
            {
                if (string.IsNullOrWhiteSpace(config.DiscordWebhookUrl))
                {
                    throw new InvalidOperationException(
                        $"Configuration '{config.Id}' has no DiscordWebhookUrl configured. " +
                        $"Provide a direct URL or reference an environment variable using 'env:VAR_NAME' syntax.");
                }

                if (config.DiscordWebhookUrl.StartsWith(ENV_PREFIX, StringComparison.OrdinalIgnoreCase))
                {
                    var envVarName = config.DiscordWebhookUrl.Substring(ENV_PREFIX.Length).Trim();

                    if (string.IsNullOrWhiteSpace(envVarName))
                    {
                        throw new InvalidOperationException(
                            $"Configuration '{config.Id}' has malformed webhook reference 'env:'. " +
                            $"Expected format: 'env:VARIABLE_NAME'");
                    }

                    var envValue = Environment.GetEnvironmentVariable(envVarName);

                    if (string.IsNullOrWhiteSpace(envValue))
                    {
                        throw new InvalidOperationException(
                            $"Configuration '{config.Id}' references environment variable '{envVarName}' " +
                            $"which is not set or is empty.");
                    }

                    config.DiscordWebhookUrl = envValue;
                    logAction?.Invoke(
                        $"Resolved webhook for configuration '{config.Id}' from environment variable '{envVarName}'");
                }
            }
        }
    }
}
