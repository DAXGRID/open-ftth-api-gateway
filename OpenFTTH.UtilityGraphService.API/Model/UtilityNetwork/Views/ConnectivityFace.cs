using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    public record ConnectivityFace
    {
        public FaceKindEnum FaceKind { get; set; }
        public string FaceName { get; set; }
        public Guid EquipmentId { get; set; }
        public string EquipmentName { get; set; }
        public ConnectivityEquipmentKindEnum EquipmentKind { get; set; }
    }
}
