﻿using OpenFTTH.Results;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.DynamicProperties;
using OpenFTTH.APIGateway.GraphQL.DynamicProperties.Types;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.CQRS;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;
using System.Linq;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.Business.Graph.Projections;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.Events.Core.Infos;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class TerminalEquipmentType : ObjectGraphType<TerminalEquipment>
    {
        public TerminalEquipmentType(ILogger<TerminalEquipmentType> logger, IQueryDispatcher queryDispatcher, DynamicPropertiesClient dynamicPropertiesReader, IEventStore eventStore)
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Master Resource Identifier UUID Property");
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Short name");
            Field(x => x.Description, type: typeof(StringGraphType)).Description("Long description");
            Field(x => x.SpecificationId, type: typeof(IdGraphType)).Description("Terminal equipment specification id");
            Field(x => x.ManufacturerId, type: typeof(IdGraphType)).Description("Terminal equipment manufacturer id");

            Field<TerminalEquipmentSpecificationType>("specification")
               .Description("The specification used to create the terminal equipment")
               .ResolveAsync(async context =>
               {
                   var queryResult = await queryDispatcher.HandleAsync<GetTerminalEquipmentSpecifications, Result<LookupCollection<TerminalEquipmentSpecification>>>(
                       new GetTerminalEquipmentSpecifications());

                   return queryResult.Value[context.Source.SpecificationId];
               });

            Field<ManufacturerType>("manufacturer")
                .Description("The manufacturer of the terminal equipment")
                .ResolveAsync(async context =>
                {
                    if (context.Source.ManufacturerId == null || context.Source.ManufacturerId == Guid.Empty)
                        return null;

                    var queryResult = await queryDispatcher.HandleAsync<GetManufacturer, Result<LookupCollection<Manufacturer>>>(new GetManufacturer());

                    return queryResult.Value[context.Source.ManufacturerId.Value];
                });

            Field<SubrackPlacementInfoType>("subrackPlacementInfo")
                .Description("information about where in a rack the terminal equipment is placed")
                .Resolve(context =>
                {
                    var getNodeContainerResult = QueryHelper.GetNodeContainer(queryDispatcher, context.Source.NodeContainerId);

                    if (getNodeContainerResult.IsFailed)
                    {
                        return null;
                    }

                    var nodeContainer = getNodeContainerResult.Value;

                    // If the terminal equipment is placed within a rack in the node container, then return rack information
                    if (nodeContainer.Racks != null && nodeContainer.Racks.Any(r => r.SubrackMounts.Any(m => m.TerminalEquipmentId == context.Source.Id)))
                    {
                        var rack = nodeContainer.Racks.First(r => r.SubrackMounts.Any(m => m.TerminalEquipmentId == context.Source.Id));
                        var mount = rack.SubrackMounts.First(m => m.TerminalEquipmentId == context.Source.Id);

                        return new SubrackPlacementInfo(rack.Id, mount.Position, SubrackPlacmentMethod.BottomUp);
                    }
                    else
                    {
                        return null;
                    }
                });


            Field<ListGraphType<DynamicPropertiesSectionType>>("dynamicProperties")
               .Description("eventually extra dynamic properties defined on this object")
               .Resolve(context =>
               {
                   return dynamicPropertiesReader.ReadProperties(context.Source.Id);
               });

            Field<AddressInfoType>("addressInfo")
              .Description("Address information such as access and unit address id")
              .Resolve(context =>
              {
                  // Try lookup via installation via name (installation id string) first
                  var installations = eventStore.Projections.Get<InstallationProjection>();
               
                  var installationInfo = installations.GetInstallationInfo(context.Source.Name, eventStore.Projections.Get<UtilityNetworkProjection>());

                  if (installationInfo != null && installationInfo.UnitAddressId != null)
                  {
                      var addresses = eventStore.Projections.Get<AddressInfoProjection>();

                      AddressInfo addrInfo = addresses.GetAddressInfo((Guid)installationInfo.UnitAddressId);

                      if (addrInfo != null) 
                      {
                          addrInfo.Remark = installationInfo.LocationRemark;

                          return addrInfo;
                      }
                  }

                  // Revert to use the address set on the terminal equipment
                  return context.Source.AddressInfo;
              });

            Field<InstallationInfoType>("installationInfo")
           .Description("Installation info - only returnd if the object is a customer termination")
           .Resolve(context =>
           {
               // Try lookup via installation via name (installation id string) first
               var installations = eventStore.Projections.Get<InstallationProjection>();

               var installationInfo = installations.GetInstallationInfo(context.Source.Name, eventStore.Projections.Get<UtilityNetworkProjection>());

               if (installationInfo != null )
               {
                    return installationInfo;
               }

               return null;
           });
        }
    }
}
