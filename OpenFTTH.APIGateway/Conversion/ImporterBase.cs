using Npgsql;
using OpenFTTH.APIGateway.Settings;
using System;
using System.Data;
using System.Linq;

namespace OpenFTTH.APIGateway.Conversion
{
    public abstract class ImporterBase
    {
        protected GeoDatabaseSetting _geoDatabaseSetting;

        public ImporterBase(GeoDatabaseSetting geoDatabaseSetting)
        {
            _geoDatabaseSetting = geoDatabaseSetting;
        }

        protected void CreateTableLogColumn(string tableName)
        {
            var conn = GetConnection();

            var logCmd = conn.CreateCommand();

            // Add status column
            try
            {
                logCmd.CommandText = "ALTER TABLE " + tableName + " ADD COLUMN IF NOT EXISTS status varchar";
                logCmd.ExecuteNonQuery();
            }
            catch (Exception ex) { }

            logCmd.Dispose();
        }

        protected void LogStatus(NpgsqlCommand cmd, string tableName, string statusText, string externalId)
        {
            if (cmd != null)
            {
                cmd.CommandText = @"UPDATE " + tableName + " set status = @statusText where external_id ='" + externalId + "' and status is null";
                cmd.Parameters.Clear();

                cmd.Parameters.AddWithValue("statusText", statusText);

                cmd.ExecuteNonQuery();
            }
        }

        protected void LogStatus(NpgsqlCommand cmd, string tableName, string keyColumnName, string key, OpenFTTH.Results.Result result)
        {
            cmd.CommandText = @"UPDATE " + tableName + " set status = @statusText where " + keyColumnName + "='" + key + "' and status is null";
            cmd.Parameters.Clear();

            if (result.IsSuccess)
                cmd.Parameters.AddWithValue("statusText", "OK");
            else
                cmd.Parameters.AddWithValue("statusText", result.Errors.First().Message);

            cmd.ExecuteNonQuery();
        }

        protected void LogStatus(NpgsqlCommand cmd, string tableName, string keyColumnName, string key, string message)
        {
            cmd.CommandText = @"UPDATE " + tableName + " set status = @statusText where " + keyColumnName + "='" + key + "' and status is null";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("statusText", message);
            cmd.ExecuteNonQuery();
        }

        protected IDbConnection GetConnection()
        {
            var conn = new NpgsqlConnection(_geoDatabaseSetting.PostgresConnectionString);
            conn.Open();
            return conn;
        }

        protected bool CheckIfConversionTableExists(string tableName)
        {
            using var dbConn = GetConnection();

            using var dbCmd = dbConn.CreateCommand();
            dbCmd.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname = 'conversion' AND tablename = '" + tableName.Split('.').Last() + "';";

            using var dbReader = dbCmd.ExecuteReader();

            if (dbReader.Read())
                return true;
            else
                return false;
        }
    }
}
