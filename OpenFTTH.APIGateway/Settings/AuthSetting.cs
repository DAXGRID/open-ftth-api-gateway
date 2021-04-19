namespace OpenFTTH.APIGateway.Settings
{
    public class AuthSetting
    {
        public string Host { get; set; }
        public bool RequireHttps { get; set; }
        public bool Enable { get; set; }
        public string Audience { get; set; }
    }
}
