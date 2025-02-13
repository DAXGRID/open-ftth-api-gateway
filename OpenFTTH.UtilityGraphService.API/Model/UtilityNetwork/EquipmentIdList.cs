using System;
using System.Collections.Generic;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public class EquipmentIdList : List<Guid>
    {
        public EquipmentIdList()
        {

        }

        public EquipmentIdList(List<Guid> guids)
        {
            this.AddRange(guids);
        }

        public EquipmentIdList(Guid[] guids)
        {
            this.AddRange(guids);
        }

        public EquipmentIdList(IEnumerable<Guid> guids)
        {
            this.AddRange(guids);
        }
    }
}
