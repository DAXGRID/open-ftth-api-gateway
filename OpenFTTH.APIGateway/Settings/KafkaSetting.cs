namespace OpenFTTH.APIGateway.Settings
{
    public class KafkaSetting
    {
        public string Server { get; set; }
        public string CertificateFilename { get; set; }
        public string RouteNetworkEventTopic { get; set; }
        public string UtilityNetworkNotificationsTopic { get; set; }
    }
}
