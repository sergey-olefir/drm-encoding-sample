using DrmEncoding.Policies;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;


namespace DrmEncoding
{
    class Program
    {
        // Set this variable to true if you want to authenticate Interactively through the browser using your Azure user account

        public static async Task Main(string[] args)
        {
            var config = new AmsConfig(new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build());

            var client = await MediaServiceBuilder.CreateMediaServicesClientAsync(config);
            Console.WriteLine("connected");

            //var tokenAsync = new ContentPolicyOptionsSync().GetTokenAsync("9dc30944-3d73-4a04-b9c4-2c227eeeed8a");
            //Console.WriteLine(tokenAsync);

            var key = Guid.NewGuid().ToString()[..4];
            var policyName = $"drm-async-{key}";

            Console.WriteLine($"Key -> {key}");
            await GetOrCreateContentKeyPolicyAsync(client, config.ResourceGroup, config.AccountName, policyName);

            const string outputAsset = "asset-51902-outputs";
            var locatorName = $"asset-51902-outputs-locator-async-{key}";
            var locator = await CreateStreamingLocatorAsync(client, config.ResourceGroup, config.AccountName, outputAsset, locatorName, policyName);

            // In this example, we want to play the PlayReady (CENC) encrypted stream.
            // We need to get the key identifier of the content key where its type is CommonEncryptionCenc.
             string keyIdentifier = locator.ContentKeys.First(k => k.Type == StreamingLocatorContentKeyType.CommonEncryptionCenc).Id.ToString();

             Console.WriteLine($"KeyIdentifier = {keyIdentifier}");

            // In order to generate our test token we must get the ContentKeyId to put in the ContentKeyIdentifierClaim claim.
            //string token = GetTokenAsync(Issuer, Audience, keyIdentifier, _tokenSigningKey);

            string dashPath = await GetDASHStreamingUrlAsync(client, config.ResourceGroup, config.AccountName, locator.Name);

            Console.WriteLine("Copy and paste the following URL in your browser to play back the file in the Azure Media Player.");
            Console.WriteLine("You can use Edge/IE11 for PlayReady and Chrome/Firefox for Widevine.");

            Console.WriteLine();

            // Console.WriteLine($"https://ampdemo.azureedge.net/?url={dashPath}&playready=true&widevine=true&token=Bearer%3D{token}");
            Console.WriteLine($"https://ampdemo.azureedge.net/?url={dashPath}&playready=true&widevine=true");
            Console.WriteLine();

            Console.WriteLine($"Bearer {new ContentPolicyOptionsAsync().GeneratePrimaryToken(4)}");
            Console.WriteLine($"Bearer {new ContentPolicyOptionsAsync().GenerateSecondaryToken()}");

            Console.WriteLine("When finished testing press enter to cleanup.");
            await Console.Out.FlushAsync();
            Console.ReadLine();
        }
        // </RunAsync>

        /// <summary>
        /// Create the content key policy that configures how the content key is delivered to end clients
        /// via the Key Delivery component of Azure Media Services.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="contentKeyPolicyName">The name of the content key policy resource.</param>
        /// <returns></returns>
        // <GetOrCreateContentKeyPolicy>
        private static async Task GetOrCreateContentKeyPolicyAsync(
            IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string contentKeyPolicyName)
        {
            //var options = new ContentPolicyOptionsAsync().ContentKeyPolicyOptions();
            var options = new ContentPolicyOptionsAsync().ContentKeyPolicyOptions();

            await client.ContentKeyPolicies.CreateOrUpdateAsync(resourceGroupName, accountName, contentKeyPolicyName, options);
        }

        // </GetOrCreateContentKeyPolicy>


        /// <summary>
        /// Creates a StreamingLocator for the specified asset and with the specified streaming policy name.
        /// Once the StreamingLocator is created the output asset is available to clients for playback.
        ///
        /// This StreamingLocator uses "Predefined_MultiDrmCencStreaming"
        /// because this sample encrypts with PlayReady and Widevine (CENC encryption).
        ///
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroup">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="assetName">The name of the output asset.</param>
        /// <param name="locatorName">The StreamingLocator name (unique in this case).</param>
        /// <returns></returns>
        // <CreateStreamingLocator>
        private static async Task<StreamingLocator> CreateStreamingLocatorAsync(
            IAzureMediaServicesClient client,
            string resourceGroup,
            string accountName,
            string assetName,
            string locatorName,
            string contentPolicyName)
        {
            // If you also added FairPlay, use "Predefined_MultiDrmStreaming
            StreamingLocator locator = await client.StreamingLocators.CreateAsync(
                resourceGroup,
                accountName,
                locatorName,
                new StreamingLocator
                {
                    AssetName = assetName,
                    // "Predefined_MultiDrmCencStreaming" policy supports envelope and cenc encryption
                    // And sets two content keys on the StreamingLocator
                    StreamingPolicyName = "Predefined_MultiDrmCencStreaming",
                    DefaultContentKeyPolicyName = contentPolicyName
                });

            return locator;
        }
        // </CreateStreamingLocator>

        /// <summary>
        /// Checks if the "default" streaming endpoint is in the running state,
        /// if not, starts it.
        /// Then, builds the streaming URLs.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="locatorName">The name of the StreamingLocator that was created.</param>
        /// <returns></returns>
        // <GetMPEGStreamingUrl>
        private static async Task<string> GetDASHStreamingUrlAsync(
            IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string locatorName)
        {
            const string DefaultStreamingEndpointName = "default";
            string dashPath = "";

            StreamingEndpoint streamingEndpoint = await client.StreamingEndpoints.GetAsync(resourceGroupName, accountName, DefaultStreamingEndpointName);

            if (streamingEndpoint.ResourceState != StreamingEndpointResourceState.Running)
            {
                await client.StreamingEndpoints.StartAsync(resourceGroupName, accountName, DefaultStreamingEndpointName);
            }

            ListPathsResponse paths = await client.StreamingLocators.ListPathsAsync(resourceGroupName, accountName, locatorName);

            foreach (StreamingPath path in paths.StreamingPaths)
            {
                UriBuilder uriBuilder = new UriBuilder
                {
                    Scheme = "https",
                    Host = streamingEndpoint.HostName
                };

                // Look for just the DASH path and generate a URL for the Azure Media Player to playback the content with the AES token to decrypt.
                // Note that the JWT token is set to expire in 1 hour.
                if (path.StreamingProtocol == StreamingPolicyStreamingProtocol.Dash)
                {
                    uriBuilder.Path = path.Paths[0];

                    dashPath = uriBuilder.ToString();

                }
            }

            return dashPath;
        }
        // </GetMPEGStreamingUrl>
    }
}