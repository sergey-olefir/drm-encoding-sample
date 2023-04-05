using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Brainstorm.QuickHelp.Azure.MediaServices.Interfaces;

using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;
using Microsoft.WindowsAzure.MediaServices.Client.Widevine;

using Newtonsoft.Json;

namespace Brainstorm.QuickHelp.Azure.MediaServices
{
    public class MediaAssetEncryptor : IMediaAssetEncryptor
    {
        private const string TokenAudience = "urn:quickhelp";

        private const string TokenIssuer = "https://quickhelp.com";

        private readonly MediaContextBase mediaContext;

        public MediaAssetEncryptor(MediaContextBase mediaContext)
        {
            this.mediaContext = mediaContext;
        }

        public async Task<string> ApplyDrmToAsset(IAsset asset)
        {
            IContentKey contentKey = this.CreateCommonTypeContentKey(asset);
            var verificationKey = await this.AddTokenRestrictedAuthorizationPolicy(contentKey);
            await this.CreateAssetDeliveryPolicy(asset, contentKey);

            return verificationKey;
        }

        public string GenerateAccessToken(IAsset asset, string verificationKey, int tokenDurationInMinutes)
        {
            var contentKey =
                asset.ContentKeys.FirstOrDefault(k => k.ContentKeyType == ContentKeyType.CommonEncryption);

            var keyBytes = Convert.FromBase64String(verificationKey);
            var tokenTemplate = this.GenerateTokenRequirements(keyBytes);
            Guid rawkey = EncryptionUtils.GetKeyIdAsGuid(contentKey.Id);
            string token = TokenRestrictionTemplateSerializer.GenerateTestToken(tokenTemplate, null, rawkey, DateTime.UtcNow.AddMinutes(tokenDurationInMinutes));

            return $"Bearer={token}";
        }

        private static string ConfigurePlayReadyLicenseTemplate()
        {
            var responseTemplate = new PlayReadyLicenseResponseTemplate();
            var licenseTemplate = new PlayReadyLicenseTemplate();
            licenseTemplate.LicenseType = PlayReadyLicenseType.Nonpersistent;
            licenseTemplate.AllowTestDevices = true;

            responseTemplate.LicenseTemplates.Add(licenseTemplate);

            return MediaServicesLicenseTemplateSerializer.Serialize(responseTemplate);
        }

        private static string ConfigureWidevineLicenseTemplate()
        {
            var template = new WidevineMessage
            {
                allowed_track_types = AllowedTrackTypes.SD_HD,
                content_key_specs = new[]
                {
                    new ContentKeySpecs
                    {
                        required_output_protection = new RequiredOutputProtection { hdcp = Hdcp.HDCP_NONE },
                        security_level = 1,
                        track_type = "SD"
                    }
                },
                policy_overrides = new
                {
                    can_play = true,
                    can_persist = true,
                    can_renew = false
                }
            };

            return JsonConvert.SerializeObject(template);
        }

        private static byte[] GetRandomBuffer(int length)
        {
            var returnValue = new byte[length];

            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(returnValue);
            }

            return returnValue;
        }

        private TokenRestrictionTemplate GenerateTokenRequirements(byte[] keyBytes)
        {
            var template = new TokenRestrictionTemplate(TokenType.SWT)
            {
                PrimaryVerificationKey = new SymmetricVerificationKey(keyBytes),
                Audience = TokenAudience,
                Issuer = TokenIssuer
            };
            template.RequiredClaims.Add(TokenClaim.ContentKeyIdentifierClaim);

            return template;
        }

        private IContentKey CreateCommonTypeContentKey(IAsset asset)
        {
            Guid keyId = Guid.NewGuid();
            byte[] contentKey = GetRandomBuffer(16);

            IContentKey key = this.mediaContext.ContentKeys.Create(
                                    keyId,
                                    contentKey,
                                    "ContentKey",
                                    ContentKeyType.CommonEncryption);

            asset.ContentKeys.Add(key);

            return key;
        }

        private async Task<string> AddTokenRestrictedAuthorizationPolicy(IContentKey contentKey)
        {
            var verivicationKey = GetRandomBuffer(64);
            string tokenTemplateString = TokenRestrictionTemplateSerializer.Serialize(this.GenerateTokenRequirements(verivicationKey));

            List<ContentKeyAuthorizationPolicyRestriction> restrictions = new List<ContentKeyAuthorizationPolicyRestriction>
            {
                new ContentKeyAuthorizationPolicyRestriction
                {
                    Name = "Token Authorization Policy",
                    KeyRestrictionType = (int)ContentKeyRestrictionType.TokenRestricted,
                    Requirements = tokenTemplateString,
                }
            };

            // Configure PlayReady and Widevine license templates.
            string playReadyLicenseTemplate = ConfigurePlayReadyLicenseTemplate();
            string widevineLicenseTemplate = ConfigureWidevineLicenseTemplate();

            IContentKeyAuthorizationPolicyOption playReadyPolicy =
                this.mediaContext.ContentKeyAuthorizationPolicyOptions.Create(
                    "Token option",
                    ContentKeyDeliveryType.PlayReadyLicense,
                    restrictions,
                    playReadyLicenseTemplate);

            IContentKeyAuthorizationPolicyOption widevinePolicy =
                this.mediaContext.ContentKeyAuthorizationPolicyOptions.Create(
                    "Token option",
                    ContentKeyDeliveryType.Widevine,
                    restrictions,
                    widevineLicenseTemplate);

            IContentKeyAuthorizationPolicy contentKeyAuthorizationPolicy =
                await
                this.mediaContext.ContentKeyAuthorizationPolicies.CreateAsync(
                    "Deliver Common Content Key with token restrictions");

            contentKeyAuthorizationPolicy.Options.Add(playReadyPolicy);
            contentKeyAuthorizationPolicy.Options.Add(widevinePolicy);

            // Associate the content key authorization policy with the content key
            contentKey.AuthorizationPolicyId = contentKeyAuthorizationPolicy.Id;
            contentKey = await contentKey.UpdateAsync();

            return Convert.ToBase64String(verivicationKey);
        }

        private async Task CreateAssetDeliveryPolicy(IAsset asset, IContentKey key)
        {
            Uri acquisitionUrl = key.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense);

            Uri widevineUrl = key.GetKeyDeliveryUrl(ContentKeyDeliveryType.Widevine);
            UriBuilder uriBuilder = new UriBuilder(widevineUrl);
            uriBuilder.Query = string.Empty;
            widevineUrl = uriBuilder.Uri;

            Dictionary<AssetDeliveryPolicyConfigurationKey, string> assetDeliveryPolicyConfiguration =
                new Dictionary<AssetDeliveryPolicyConfigurationKey, string>
                    {
                        {
                            AssetDeliveryPolicyConfigurationKey
                            .PlayReadyLicenseAcquisitionUrl,
                            acquisitionUrl.ToString()
                        },
                        {
                            AssetDeliveryPolicyConfigurationKey
                            .WidevineBaseLicenseAcquisitionUrl,
                            widevineUrl.ToString()
                        }
                    };

            IAssetDeliveryPolicy dashPolicy =
                await
                this.mediaContext.AssetDeliveryPolicies.CreateAsync(
                    "DashAssetDeliveryPolicy",
                    AssetDeliveryPolicyType.DynamicCommonEncryption,
                    AssetDeliveryProtocol.Dash,
                    assetDeliveryPolicyConfiguration);

            IAssetDeliveryPolicy hlsPolicy =
                await
                this.mediaContext.AssetDeliveryPolicies.CreateAsync(
                    "HlsAssetDeliveryPolicy",
                    AssetDeliveryPolicyType.NoDynamicEncryption,
                    AssetDeliveryProtocol.HLS,
                    null);

            asset.DeliveryPolicies.Add(dashPolicy);
            asset.DeliveryPolicies.Add(hlsPolicy);

            await asset.UpdateAsync();
        }
    }
}