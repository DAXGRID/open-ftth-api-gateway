using GraphQL;
using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.Addresses.Types;
using OpenFTTH.APIGateway.GraphQL.Installation.Types;
using OpenFTTH.APIGateway.GraphQL.Location.Types;
using OpenFTTH.APIGateway.Specifications;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.Business.RouteElements.Model;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.Graph.Projections;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.GraphQL.Installation.Queries;

public class InstallationQueries : ObjectGraphType
{
    public InstallationQueries(IQueryDispatcher queryDispatcher, IEventStore eventStore, IRouteNetworkState routeNetworkState)
    {
        Description = "GraphQL API for querying installation information";

        Field<ListGraphType<InstallationSearchResponseType>>("nearestUndocumentedInstallations")
                .Arguments(new QueryArguments(
                    new QueryArgument<IdGraphType> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "maxHits" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "searchRadiusMeter" }
                ))
                .ResolveAsync(async (context) =>
                {
                    Guid routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    int maxHits = context.GetArgument<int>("maxHits");
                    int searchRadiusMeter = context.GetArgument<int>("searchRadiusMeter");

                    var utilityNetworkProjection = eventStore.Projections.Get<UtilityNetworkProjection>();
                    var addressProjection = eventStore.Projections.Get<AddressInfoProjection>();
                    var installationProjection = eventStore.Projections.Get<InstallationProjection>();
                    var terminalEquipmentSpecifications = eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;


                    // Find installations that has not yet been added to the utility network
                    List<InstallationRecord> installationsNotRegisteredInNetwork = [];

                    foreach (var inst in installationProjection.InstallationsById.Values)
                    {
                        if (!utilityNetworkProjection.TerminalEquipmentIdByName.ContainsKey(inst.InstallationId))
                        {
                            installationsNotRegisteredInNetwork.Add(inst);
                        }
                        else
                        {
                            // Make sure found equipment is a customer installation, otherwise add installation to candidate list
                            if (inst.InstallationId == "15")
                            {

                            }

                            var installationEquipmentFound = false;

                            var teIds = utilityNetworkProjection.TerminalEquipmentIdByName[inst.InstallationId];

                            foreach (var teId in teIds)
                            {
                                TerminalEquipment te = utilityNetworkProjection.TerminalEquipmentByEquipmentId[teId];
                                TerminalEquipmentSpecification spec = terminalEquipmentSpecifications[te.SpecificationId];

                                if (spec.IsCustomerTermination)
                                {
                                    installationEquipmentFound = true;
                                }
                            }

                            if (!installationEquipmentFound)
                            {
                                installationsNotRegisteredInNetwork.Add(inst);
                            }

                        }
                    }

                    // Find installations within search radius
                    List<InstallationSearchResponse> installationsWithinSearchRadius = new List<InstallationSearchResponse>();

                
                    RouteNode routeNode = (RouteNode)routeNetworkState.GetRouteNetworkElement(routeNodeId);

                    if (routeNode == null)
                        throw new ApplicationException($"Cannot find route node with id: " + routeNodeId);

                    foreach (var unregisteredInst in installationsNotRegisteredInNetwork)
                    {
                        if (unregisteredInst.UnitAddressId != null && addressProjection.UnitAddressesById.ContainsKey((Guid)unregisteredInst.UnitAddressId))
                        {
                            var addressInfo = addressProjection.GetAddressInfo((Guid)unregisteredInst.UnitAddressId);
                            var unitAddress = addressProjection.UnitAddressesById[(Guid)unregisteredInst.UnitAddressId];
                            var accessAddress = addressProjection.AccessAddressesById[(Guid)addressInfo.AccessAddressId];

                            double distanceToInst = routeNode.Distance(accessAddress.EastCoordinate, accessAddress.NorthCoordinate);

                            if (distanceToInst < searchRadiusMeter)
                            {
                                var road = addressProjection.RoadsById[(Guid)accessAddress.RoadId];

                                var addressString = (road.Name + " " + accessAddress.HouseNumber + " " + unitAddress.FloorName + " " + unitAddress.SuitName).Trim();

                                installationsWithinSearchRadius.Add(new InstallationSearchResponse(unregisteredInst.InstallationId, addressString, unregisteredInst.LocationRemark, distanceToInst));
                            }
                        }
                    }

                    return installationsWithinSearchRadius.OrderBy(o => o.Distance).ThenBy(o => o.DisplayAddress);
                });
    }
}
