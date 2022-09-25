﻿using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class ConnectivityTraceViewHopInfoType : ObjectGraphType<ConnectivityTraceViewHopInfo>
    {
        public ConnectivityTraceViewHopInfoType(ILogger<TerminalEquipmentAZConnectivityViewLineInfoType> logger, UTM32WGS84Converter coordinateConverter)
        {
            Field(x => x.Level, type: typeof(IntGraphType)).Description("Level used to display tree like structure where hops are grouped by splitters etc.");
            Field(x => x.IsSplitter, type: typeof(BooleanGraphType)).Description("Whether the hop is a splitter out");
            Field(x => x.IsTraceSource, type: typeof(BooleanGraphType)).Description("Whether the hop is where the source of the upstream/downstream trace");
            Field(x => x.Node, type: typeof(StringGraphType)).Description("The name of route node in the trace hop");
            Field(x => x.Equipment, type: typeof(StringGraphType)).Description("Equipment info");
            Field(x => x.TerminalStructure, type: typeof(StringGraphType)).Description("Equipment card/slot/tray info");
            Field(x => x.Terminal, type: typeof(StringGraphType)).Description("Terminal info");
            Field(x => x.ConnectionInfo, type: typeof(StringGraphType)).Description("Connection info");

            Field(x => x.RouteSegmentIds, type: typeof(ListGraphType<IdGraphType>)).Description("Route network segment ids of the span segment traversal");

            Field<FloatGraphType>(
                name: "TotalLength",
                description: "Length in meters",
                resolve: context =>
                {
                    return Math.Round(context.Source.TotalLength, 2);
                }
            );
     
            Field<ListGraphType<StringGraphType>>(
              name: "RouteSegmentGeometries",
              description: "Route network segment geometries of the span segment traversal",
              resolve: context =>
              {
                  return coordinateConverter.ConvertGeoJsonLineStringsToWgs84(context.Source.RouteSegmentGeometries).WGS84GeoJsonStrings;
              }
           );


            Field(x => x.HopSeqNo, type: typeof(IntGraphType)).Description("Sequence number of the hop");
        }
    }
}
