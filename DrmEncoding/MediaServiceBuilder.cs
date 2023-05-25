using Common;
using Microsoft.Azure.Management.Media;
using Microsoft.Identity.Client;
using Microsoft.Rest;

namespace DrmEncoding
{
    public class MediaServiceBuilder
    {
        private static readonly string TokenType = "Bearer";

        public async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync(AmsConfig config)
        {
            var credentials = await GetCredentialsAsync(config);

            return new AzureMediaServicesClient(config.ArmEndpoint, credentials)
            {
                SubscriptionId = config.SubscriptionId,
            };
        }

        private async Task<ServiceClientCredentials> GetCredentialsAsync(AmsConfig config)
        {
            // Use ApplicationTokenProvider.LoginSilentWithCertificateAsync or UserTokenProvider.LoginSilentAsync to get a token using service principal with certificate
            //// ClientAssertionCertificate
            //// ApplicationTokenProvider.LoginSilentWithCertificateAsync

            // Use ApplicationTokenProvider.LoginSilentAsync to get a token using a service principal with symmetric key
            var app = ConfidentialClientApplicationBuilder.Create(config.AadClientId)
                .WithClientSecret(config.AadSecret)
                .WithAuthority(AzureCloudInstance.AzurePublic, config.AadTenantId)
                .Build();

            var scopes = new[] { config.ArmAadAudience + "/.default" };

            var authResult = await app.AcquireTokenForClient(scopes)
                .ExecuteAsync()
                .ConfigureAwait(false);

            return new TokenCredentials(authResult.AccessToken, TokenType);
        }
    }
}