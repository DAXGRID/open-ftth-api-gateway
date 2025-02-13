using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record WalkInfo
    {
        public Guid WalkOfInteresId { get; }
        public Guid[] PointOfInterestIds { get; }

        public WalkInfo(Guid walkOfInteresId, Guid[] pointOfInterestIds)
        {
            WalkOfInteresId = walkOfInteresId;
            PointOfInterestIds = pointOfInterestIds;
        }
    }
}
