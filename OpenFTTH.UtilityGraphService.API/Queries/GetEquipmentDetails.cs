using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetEquipmentDetails : IQuery<Result<GetEquipmentDetailsResult>>
    {
        public InterestIdList InterestIdsToQuery { get; }

        public EquipmentIdList EquipmentIdsToQuery { get; }

        public string EquipmentNameToQuery { get; }

        #region Equipment Details Filter Options
        private EquipmentDetailsFilterOptions _equipmentDetailsFilterOptions =
            new EquipmentDetailsFilterOptions()
            {
                IncludeRouteNetworkTrace = false
            };

        public EquipmentDetailsFilterOptions EquipmentDetailsFilter
        {
            get { return _equipmentDetailsFilterOptions; }
            init { _equipmentDetailsFilterOptions = value; }
        }

        #endregion


        /// <summary>
        /// Use this contructor, if you want to query by equipment ids
        /// </summary>
        /// <param name="equipmentIds"></param>
        public GetEquipmentDetails(EquipmentIdList equipmentIds)
        {
            if (equipmentIds == null || equipmentIds.Count == 0)
                throw new ArgumentException("At least one equipment id must be specified");

            this.InterestIdsToQuery = new InterestIdList();

            this.EquipmentIdsToQuery = equipmentIds;
        }


        /// <summary>
        /// Use this contructor, if you want to query by interest ids
        /// </summary>
        /// <param name="interestIds"></param>
        public GetEquipmentDetails(InterestIdList interestIds)
        {
            if (interestIds == null || interestIds.Count == 0)
                throw new ArgumentException($"At least one interest id must be specified: {interestIds?.Count}");

            this.EquipmentIdsToQuery = new EquipmentIdList();

            this.InterestIdsToQuery = interestIds;
        }

        /// <summary>
        /// Use this contructor, if you want to query by equipment name
        /// Only non-null and non-black equipments can be searched for
        /// </summary>
        /// <param name="name"></param>
        public GetEquipmentDetails(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"Search string cannot be null or empty");
            }

            this.EquipmentNameToQuery =name;
        }
    }
}
