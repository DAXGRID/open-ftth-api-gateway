using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.API.Model
{
    public record RouteNetworkElementInterestRelation
    {
        public Guid RefId { get;}
        public RouteNetworkInterestRelationKindEnum RelationKind { get; }

        public RouteNetworkElementInterestRelation(Guid refId, RouteNetworkInterestRelationKindEnum relationKind)
        {
            this.RefId = refId;
            this.RelationKind = relationKind;
        }
    }
}
