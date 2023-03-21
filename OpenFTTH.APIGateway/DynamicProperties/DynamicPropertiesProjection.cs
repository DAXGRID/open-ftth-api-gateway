using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.APIGateway.DynamicProperties
{
    public class DynamicPropertiesProjection : ProjectionBase
    {
        private readonly string _schemaName = "custom";
        private readonly string _newInstallationProcName = "sp_upsert_installation";

        private readonly ILogger<DynamicPropertiesProjection> _logger;
        private readonly GeoDatabaseSetting _geoDatabaseSetting;

        private HashSet<Guid> _customerTerminationSpecifications = new();
        private HashSet<Guid> _customerTerminations = new();
        private Dictionary<Guid, Guid> _nodeContainerToRouteNodeId = new();

        private bool _isInitialized = false;
        private bool _newInstallationStoredProcedureExists = false;

        public DynamicPropertiesProjection(ILogger<DynamicPropertiesProjection> logger, IOptions<GeoDatabaseSetting> geoDatabaseSetting)
        {
            _logger = logger;
            _geoDatabaseSetting = geoDatabaseSetting.Value;

            Initialize();

            ProjectEventAsync<TerminalEquipmentPlacedInNodeContainer>(ProjectAsync);
            ProjectEventAsync<TerminalEquipmentSpecificationAdded>(ProjectAsync);
            ProjectEventAsync<NodeContainerPlacedInRouteNetwork>(ProjectAsync);
            ProjectEventAsync<TerminalEquipmentAddressInfoChanged>(ProjectAsync);
        }

        private void Initialize()
        {
            if (_isInitialized)
                return;

            if (CheckIfStoredProcedureExists(_schemaName, _newInstallationProcName))
            {
                _newInstallationStoredProcedureExists = true;
            }

            _isInitialized = true;
        }

        private Task ProjectAsync(IEventEnvelope eventEnvelope)
        {
            if (_newInstallationStoredProcedureExists)
            {
                switch (eventEnvelope.Data)
                {
                    case (TerminalEquipmentSpecificationAdded @event):

                        if (@event.Specification.IsCustomerTermination)
                            _customerTerminationSpecifications.Add(@event.Specification.Id);

                        break;

                    case (TerminalEquipmentPlacedInNodeContainer @event):

                        if (_customerTerminationSpecifications.Contains(@event.Equipment.SpecificationId))
                        {
                            _customerTerminations.Add(@event.Equipment.Id);
                            CallNewInstallationStoredProcedure(@event);
                        }

                        break;

                    case (TerminalEquipmentAddressInfoChanged @event):

                        if (_customerTerminations.Contains(@event.TerminalEquipmentId))
                        {
                            CallNewInstallationStoredProcedure(@event);
                        }

                        break;

                    case (NodeContainerPlacedInRouteNetwork @event):

                        if (!_nodeContainerToRouteNodeId.ContainsKey(@event.Container.Id))
                            _nodeContainerToRouteNodeId.Add(@event.Container.Id, @event.Container.RouteNodeId);

                        break;

                }
            }

            return Task.CompletedTask;
        }

        private void CallNewInstallationStoredProcedure(TerminalEquipmentPlacedInNodeContainer @event)
        {
            if (_newInstallationStoredProcedureExists && _nodeContainerToRouteNodeId.ContainsKey(@event.Equipment.NodeContainerId))
            {
                using var dbConn = GetConnection();

                using var dbCmd = dbConn.CreateCommand() as NpgsqlCommand;

                dbCmd.Parameters.AddWithValue("@equipment_id", @event.Equipment.Id);
                dbCmd.Parameters.AddWithValue("@route_node_id", _nodeContainerToRouteNodeId[@event.Equipment.NodeContainerId]);
                dbCmd.Parameters.AddWithValue("@access_address_id", @event.Equipment.AddressInfo != null && @event.Equipment.AddressInfo.AccessAddressId != null ? @event.Equipment.AddressInfo.AccessAddressId : DBNull.Value);
                dbCmd.Parameters.AddWithValue("@unit_address_id", @event.Equipment.AddressInfo != null && @event.Equipment.AddressInfo.UnitAddressId != null ? @event.Equipment.AddressInfo.UnitAddressId : DBNull.Value);
                dbCmd.Parameters.AddWithValue("@address_location_info", @event.Equipment.AddressInfo != null && @event.Equipment.AddressInfo.Remark != null ? @event.Equipment.AddressInfo.Remark : DBNull.Value);

                dbCmd.CommandText = $"CALL {_schemaName}.{_newInstallationProcName}(@equipment_id, @route_node_id, @access_address_id, @unit_address_id, @address_location_info);";

                dbCmd.ExecuteNonQuery();
            }
        }

        private void CallNewInstallationStoredProcedure(TerminalEquipmentAddressInfoChanged @event)
        {
            if (_newInstallationStoredProcedureExists)
            {
                using var dbConn = GetConnection();

                using var dbCmd = dbConn.CreateCommand() as NpgsqlCommand;

                dbCmd.Parameters.AddWithValue("@equipment_id", @event.TerminalEquipmentId);
                dbCmd.Parameters.AddWithValue("@route_node_id", DBNull.Value);
                dbCmd.Parameters.AddWithValue("@access_address_id", @event.AddressInfo != null && @event.AddressInfo.AccessAddressId != null ? @event.AddressInfo.AccessAddressId : DBNull.Value);
                dbCmd.Parameters.AddWithValue("@unit_address_id", @event.AddressInfo != null && @event.AddressInfo.UnitAddressId != null ? @event.AddressInfo.UnitAddressId : DBNull.Value);
                dbCmd.Parameters.AddWithValue("@address_location_info", @event.AddressInfo != null && @event.AddressInfo.Remark != null ? @event.AddressInfo.Remark : DBNull.Value);

                dbCmd.CommandText = $"CALL {_schemaName}.{_newInstallationProcName}(@equipment_id, @route_node_id, @access_address_id, @unit_address_id, @address_location_info);";

                dbCmd.ExecuteNonQuery();
            }
        }

        protected bool CheckIfStoredProcedureExists(string schemaName, string procName)
        {
            using var dbConn = GetConnection();

            using var dbCmd = dbConn.CreateCommand();
            dbCmd.CommandText = $"SELECT * FROM pg_catalog.pg_proc JOIN pg_namespace ON pg_catalog.pg_proc.pronamespace = pg_namespace.oid WHERE proname = '{procName.Split('.').Last()}' AND pg_namespace.nspname = '{schemaName}';";

            using var dbReader = dbCmd.ExecuteReader();

            if (dbReader.Read())
                return true;
            else
                return false;
        }

        private IDbConnection GetConnection()
        {
            var conn = new NpgsqlConnection(_geoDatabaseSetting.PostgresConnectionString);
            conn.Open();
            return conn;
        }
    }
}
