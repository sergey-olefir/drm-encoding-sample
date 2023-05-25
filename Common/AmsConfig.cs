using Microsoft.Extensions.Configuration;

namespace Common
{
    public class AmsConfig
    {
        private readonly IConfiguration _config;

        public AmsConfig(IConfiguration config)
        {
            this._config = config;
        }

        public string SubscriptionId => this._config["SubscriptionId"];

        public string ResourceGroup => this._config["ResourceGroup"];

        public string AccountName => this._config["AccountName"];

        public string AadTenantId => this._config["AadTenantId"];

        public string AadTenantDomain => this._config["AadTenantDomain"];

        public string AadUrl => this._config["AadUrl"];

        public string AadClientId => this._config["AadClientId"];

        public string AadSecret => this._config["AadSecret"];

        public Uri ArmAadAudience => new Uri(this._config["ArmAadAudience"]);

        public Uri AadEndpoint => new Uri(this._config["AadEndpoint"]);

        public Uri ArmEndpoint => new Uri(this._config["ArmEndpoint"]);
    }
}