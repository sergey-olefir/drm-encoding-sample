using DrmEncoding;
using DrmEncodingV2;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.MediaServices.Client;

var config = new AmsConfig(new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build());

var client = MediaServiceBuilder.CreateMediaServicesClientAsync(config);
Console.WriteLine("connected");

var key = Guid.NewGuid().ToString()[..4];
var policyName = $"drm-async-{key}";

Console.WriteLine($"Key -> {key}");

const string outputAsset = "asset-51902-outputs";
var locatorName = $"asset-51902-outputs-locator-async-{key}";

client.Assets.FirstOrDefault();


IAsset asset = client.Assets.First(x => x.Name == outputAsset);