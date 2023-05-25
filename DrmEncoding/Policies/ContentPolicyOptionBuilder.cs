using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using DrmEncoding.WidevineConfig;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.IdentityModel.Tokens;

namespace DrmEncoding.Policies
{
    public class ContentPolicyOptionBuilder
    {
        private readonly string _base64;
        private const string Issuer = "iss";
        private const string Audience = "aud";

        public ContentPolicyOptionBuilder()
        {
            this._base64 = @"MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAtAc7v06Ud4YL+6AYjiKP
                nLpx7TtfBZfozQVi5pRCi5FM5NBDjtg7+F+mUjyHHDPw4AyQdyVTuFlyMcRI+NCm
                Jdi+kPgko4x3jx+vEcVcsEZeZsRmarfvlpleRQvf0/RxDr7d0GjTUKGlIEUFacxI
                DGC2menfD3TB/8RMILIR6E8cQIW/cGBjo/DWGBcgl2EbnXB35vqLE/bzuIo4Qt9G
                Hz94io6r93MVlwEeCMqFvBIhIf4A18EJOtEJZFKLl49NTe5xJSD2+TjQX4z4sl/t
                LoYXvC9vPCWZLswWD+TCg6FqPnJJzeUgSt0ultzrOBLiSrtcWFChJp2ahvHPj1V4
                vQIDAQAB";
        }

        public List<ContentKeyPolicyOption> ContentKeyPolicyOptions()
        {
            var crt = new X509Certificate2("verification2037.pfx", "password$1");
            var key = new ContentKeyPolicyX509CertificateTokenKey(crt.RawData);

            var requiredClaims = new List<ContentKeyPolicyTokenClaim>();
            var alternateKeys = new List<ContentKeyPolicyRestrictionTokenKey>
            {
                new ContentKeyPolicySymmetricTokenKey(Convert.FromBase64String(this._base64))
            };

            var restriction = new ContentKeyPolicyTokenRestriction(
                Issuer,
                Audience,
                key,
                ContentKeyPolicyRestrictionTokenType.Jwt,
                alternateKeys,
                requiredClaims);

            var playReadyConfig = ConfigurePlayReadyLicenseTemplate();
            var widevineConfig = ConfigureWidevineLicenseTemplate();

            return new List<ContentKeyPolicyOption>
            {
                new () { Configuration = playReadyConfig, Restriction = restriction },
                new () { Configuration = widevineConfig, Restriction = restriction }
            };
        }

        public string GeneratePrimaryToken(string keyIdentifier)
        {
            var crt = new X509Certificate2("verification2037.pfx", "password$1");
            var privateKey = new X509SecurityKey(crt);

            SigningCredentials cred = new SigningCredentials(
                privateKey,
                SecurityAlgorithms.RsaSha256);

            var claims = new List<Claim>
            {
                new Claim(ContentKeyPolicyTokenClaim.ContentKeyIdentifierClaim.ClaimType, keyIdentifier)
            };

            JwtSecurityToken tokenDescriptor = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                notBefore: DateTime.Now.AddMinutes(-5),
                expires: DateTime.Now.AddMinutes(60),
                signingCredentials: cred);

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();

            return handler.WriteToken(tokenDescriptor);
        }

        public string GenerateSecondaryToken()
        {
            var tokenSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(this._base64));

            SigningCredentials cred = new SigningCredentials(
                tokenSigningKey,
                SecurityAlgorithms.HmacSha256,
                SecurityAlgorithms.Sha256Digest);

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                notBefore: DateTime.Now.AddMinutes(-5),
                expires: DateTime.Now.AddMinutes(60),
                signingCredentials: cred);

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();

            return handler.WriteToken(token);
        }

        /// <summary>
        /// Configures PlayReady license template.
        /// </summary>
        /// <returns></returns>
        //<ConfigurePlayReadyLicenseTemplate>
        private static ContentKeyPolicyPlayReadyConfiguration ConfigurePlayReadyLicenseTemplate()
        {
            var objContentKeyPolicyPlayReadyLicense = new ContentKeyPolicyPlayReadyLicense
            {
                AllowTestDevices = true,
                BeginDate = new DateTime(2016, 1, 1),
                ContentKeyLocation = new ContentKeyPolicyPlayReadyContentEncryptionKeyFromHeader(),
                ContentType = ContentKeyPolicyPlayReadyContentType.UltraVioletStreaming,
                LicenseType = ContentKeyPolicyPlayReadyLicenseType.Persistent,
                PlayRight = new ContentKeyPolicyPlayReadyPlayRight
                {
                    ImageConstraintForAnalogComponentVideoRestriction = true,
                    ExplicitAnalogTelevisionOutputRestriction = new ContentKeyPolicyPlayReadyExplicitAnalogTelevisionRestriction(true, 2),
                    AllowPassingVideoContentToUnknownOutput = ContentKeyPolicyPlayReadyUnknownOutputPassingOption.Allowed
                }
            };

            var objContentKeyPolicyPlayReadyConfiguration = new ContentKeyPolicyPlayReadyConfiguration
            {
                Licenses = new List<ContentKeyPolicyPlayReadyLicense> { objContentKeyPolicyPlayReadyLicense },
            };

            return objContentKeyPolicyPlayReadyConfiguration;
        }

        /// <summary>
        /// Configures Widevine license template.
        /// </summary>
        /// <returns></returns>
        // <ConfigureWidevineLicenseTemplate>
        private static ContentKeyPolicyWidevineConfiguration ConfigureWidevineLicenseTemplate()
        {
            WidevineTemplate template = new WidevineTemplate()
            {
                AllowedTrackTypes = "SD_HD",
                ContentKeySpecs = new ContentKeySpec[]
                {
                    new ContentKeySpec()
                    {
                        TrackType = "SD",
                        SecurityLevel = 1,
                        RequiredOutputProtection = new OutputProtection()
                        {
                            HDCP = "HDCP_NONE"
                        }
                    }
                },
                PolicyOverrides = new PolicyOverrides()
                {
                    CanPlay = true,
                    CanPersist = true,
                    CanRenew = false,
                    RentalDurationSeconds = 2592000,
                    PlaybackDurationSeconds = 10800,
                    LicenseDurationSeconds = 604800,
                }
            };

            var objContentKeyPolicyWidevineConfiguration = new ContentKeyPolicyWidevineConfiguration
            {
                WidevineTemplate = Newtonsoft.Json.JsonConvert.SerializeObject(template)
            };
            return objContentKeyPolicyWidevineConfiguration;
        }
    }
}