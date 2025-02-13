using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record InterfaceInfo
    {
        public string InterfaceType { get; }
        public int SlotNumber { get; }
        public int SubSlotNumber { get; }
        public int PortNumber { get; }
        public string CircuitName { get; }

        public InterfaceInfo(string interfaceType, int slotNumber, int subSlotNumber, int portNumber, string circuitName)
        {
            InterfaceType = interfaceType;
            SlotNumber = slotNumber;
            SubSlotNumber = subSlotNumber;
            PortNumber = portNumber;
            CircuitName = circuitName;
        }

        public bool EqualTo(InterfaceInfo? otherInterfaceInfo)
        {
            if (otherInterfaceInfo is null)
                return false;

            if (this.InterfaceType == otherInterfaceInfo.InterfaceType &&
                this.SlotNumber == otherInterfaceInfo.SlotNumber &&
                this.SubSlotNumber == otherInterfaceInfo.SubSlotNumber &&
                this.PortNumber == otherInterfaceInfo.PortNumber &&
                this.CircuitName == otherInterfaceInfo.CircuitName
                )
                return true;

            return false;
        }
    }
}
