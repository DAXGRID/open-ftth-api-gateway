namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record TerminalTemplate
    {
        public string Name { get; }
        public TerminalDirectionEnum Direction { get; }
        public bool IsPigtail { get; }
        public bool IsSplice { get; }
        public string? ConnectorType { get; init; }
        public string? InternalConnectivityNode { get; init; }

        public TerminalTemplate(string name, TerminalDirectionEnum direction, bool isPigtail, bool isSplice)
        {
            Name = name;
            Direction = direction;
            IsPigtail = isPigtail;
            IsSplice = isSplice;
        }
    }
}
