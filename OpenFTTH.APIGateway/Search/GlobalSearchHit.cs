using System;

namespace OpenFTTH.APIGateway.Search
{
    public record GlobalSearchHit
    {
        public Guid Id { get; }
        public string ObjectType { get; }
        public string Label { get; }
        public double Xwgs { get; }
        public double Ywgs { get; }
        public double Xetrs { get; }
        public double Yetrs { get; }
        public long TextMatch { get; }

        public GlobalSearchHit(Guid id, string objectType, string label, double xwgs, double ywgs, double xetrs, double yetrs, long textMatch)
        {
            Id = id;
            ObjectType = objectType;
            Label = label;
            Xwgs = xwgs;
            Ywgs = ywgs;
            Xetrs = xetrs;
            Yetrs = yetrs;
            TextMatch = textMatch;
        }
    }
}
