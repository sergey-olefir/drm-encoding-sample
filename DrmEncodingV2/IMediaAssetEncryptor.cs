using Microsoft.WindowsAzure.MediaServices.Client;

namespace DrmEncodingV2
{
    public interface IMediaAssetEncryptor
    {
        Task<string> ApplyDrmToAsset(IAsset asset);

        string GenerateAccessToken(IAsset asset, string verificationKey, int tokenDurationInMinutes);
    }
}
