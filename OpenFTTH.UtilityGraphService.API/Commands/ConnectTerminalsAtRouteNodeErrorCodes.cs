namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public enum ConnectTerminalsAtRouteNodeErrorCodes
    {
        INVALID_ROUTE_NODE_ID_CANNOT_BE_EMPTY,
        INVALID_FROM_TERMINAL_ID_CANNOT_BE_EMPTY,
        INVALID_TO_TERMINAL_ID_CANNOT_BE_EMPTY,
        TERMINAL_ID_NOT_FOUND,
        TERMINAL_ALREADY_CONNECTED,
        TERMINAL_NOT_CONNECTED,
    }
}
