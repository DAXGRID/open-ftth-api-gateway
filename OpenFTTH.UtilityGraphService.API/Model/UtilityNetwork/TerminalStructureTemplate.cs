using System;
using System.Collections.Generic;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public class TerminalStructureTemplate
    {
        public Guid TerminalStructureSpecificationId { get; }
        public UInt16 Position { get; }
        public TerminalStructureTemplate(Guid terminalStructureSpecificationId, UInt16 position)
        {
            TerminalStructureSpecificationId = terminalStructureSpecificationId;
            Position = position;
        }
    }
}
