using DrmEncoding;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace DrmEncodingV2
{
    class MediaServiceBuilder
    {
        public static CloudMediaContext CreateMediaServicesClientAsync(AmsConfig config)
        {

            var tokenCredentials = new AzureAdTokenCredentials(
                config.AadTenantDomain,
                new AzureAdClientSymmetricKey(
                    config.AadClientId,
                    config.AadSecret),
                AzureEnvironments.AzureCloudEnvironment);
            var tokenProvider = new AzureAdTokenProvider(tokenCredentials);

            return new CloudMediaContext(new Uri(config.AadUrl), tokenProvider);
        }
    }
}