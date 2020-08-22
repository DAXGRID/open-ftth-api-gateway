﻿using GraphQL.Types;
using OpenFTTH.Events.Core.Infos;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFTTH.APIGateway.RouteNetwork.GraphQL.Types
{
    public class DeploymentStateEnumType : EnumerationGraphType<DeploymentStateEnum>
    {
        public DeploymentStateEnumType()
        {
            Name = "DeploymentStateEnum";
            Description = @"Possible states of asset deployment.";
        }
    }
}