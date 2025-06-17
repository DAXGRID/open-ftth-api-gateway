using OpenFTTH.Results;
using GraphQL;
using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.Addresses.Queries;
using OpenFTTH.APIGateway.GraphQL.Addresses.Types;
using OpenFTTH.Address.API.Queries;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using System;
using System.Linq;
using OpenFTTH.APIGateway.Util;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class AddressInfoType : ObjectGraphType<AddressInfo>
    {
        public AddressInfoType(IQueryDispatcher queryDispatcher, UTM32WGS84Converter coordinateConverter)
        {
            Field(x => x.AccessAddressId, type: typeof(IdGraphType)).Description("Internal or external access address id");
            Field(x => x.UnitAddressId, type: typeof(IdGraphType)).Description("Internal or external unit address id");
            Field(x => x.Remark, type: typeof(StringGraphType)).Description("Additional address information remark");

            Field<AccessAddressType>("accessAddress")
               .Description("Access address and its containing unit addresses")
               .Resolve(context =>
               {
                   if (context.Source.AccessAddressId != null && context.Source.AccessAddressId != Guid.Empty)
                   {
                       var getAddressInfoQuery = new GetAddressInfo(new Guid[] { context.Source.AccessAddressId.Value });

                       var result = queryDispatcher.HandleAsync<GetAddressInfo, Result<GetAddressInfoResult>>(getAddressInfoQuery).Result;

                       if (result.IsFailed)
                       {
                           context.Errors.Add(new ExecutionError(result.Errors.First().Message));
                           return null;
                       }

                       // If no address is found with that id, just return null
                       if (result.Value.AccessAddresses.Count != 1)
                       {
                           return null;
                       }

                       return AddressServiceQueries.MapAccessAddress(result.Value.AccessAddresses.First().Id, result.Value, coordinateConverter);
                   }
                   else
                       return null;
               });

            Field<UnitAddressType>("unitAddress")
              .Description("Access address and its containing unit addresses")
              .Resolve(context =>
              {
                  if (context.Source.UnitAddressId != null && context.Source.UnitAddressId != Guid.Empty)
                  {
                      var getAddressInfoQuery = new GetAddressInfo(new Guid[] { context.Source.UnitAddressId.Value });

                      var result = queryDispatcher.HandleAsync<GetAddressInfo, Result<GetAddressInfoResult>>(getAddressInfoQuery).Result;

                      if (result.IsFailed)
                      {
                          context.Errors.Add(new ExecutionError(result.Errors.First().Message));
                          return null;
                      }

                      // If no address is found with that id, just return null
                      if (result.Value.UnitAddresses.Count != 1)
                      {
                          context.Errors.Add(new ExecutionError($"Problem find unit address with id {context.Source.UnitAddressId.Value} in address database. Expected one hit but gut {result.Value.UnitAddresses.Count}"));
                          return null;
                      }

                      return result.Value.UnitAddresses.First();
                  }
                  else
                      return null;
              });


        }
    }
}
