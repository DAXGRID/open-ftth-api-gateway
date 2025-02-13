﻿namespace OpenFTTH.RouteNetwork.API.Commands
{
    public enum RegisterNodeOfInterestErrorCodes
    {
        INVALID_INTEREST_KIND_MUST_BE_NODE_OF_INTEREST,
        INVALID_INTEREST_ID_CANNOT_BE_EMPTY,
        INVALID_INTEREST_ALREADY_EXISTS,
        INVALID_ROUTE_NODE_ID_CANNOT_BE_EMPTY,
        INVALID_ROUTE_NODE_ID_MUST_BE_ROUTE_NODE,
        INVALID_ROUTE_NODE_ID_CANNOT_FIND_ROUTE_NETWORK_ELEMENT
    }
}
