using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace WebApplication1.Data
{
    public static class DbHelper
    {
        private static string GetConnectionString()
        {
            string envConn = Services.EnvConfig.SqlConnectionString;
            if (!string.IsNullOrWhiteSpace(envConn)) return envConn;

            if (ConfigurationManager.ConnectionStrings["LmsConnection"] != null &&
                !string.IsNullOrWhiteSpace(ConfigurationManager.ConnectionStrings["LmsConnection"].ConnectionString))
            {
                return ConfigurationManager.ConnectionStrings["LmsConnection"].ConnectionString;
            }

            if (ConfigurationManager.ConnectionStrings["EmployeeDB"] != null &&
                !string.IsNullOrWhiteSpace(ConfigurationManager.ConnectionStrings["EmployeeDB"].ConnectionString))
            {
                return ConfigurationManager.ConnectionStrings["EmployeeDB"].ConnectionString;
            }

            if (ConfigurationManager.ConnectionStrings["DefaultConnection"] != null &&
                !string.IsNullOrWhiteSpace(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
            {
                return ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            }

            // Fallback default
            return "Data Source=.;Initial Catalog=Practice;Integrated Security=True;TrustServerCertificate=True;";
        }

        private static CommandType DetermineCommandType(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText)) return CommandType.StoredProcedure;
            string trimmed = commandText.TrimStart();
            if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(" "))
            {
                return CommandType.Text;
            }
            return CommandType.StoredProcedure;
        }

        public static DataTable ExecuteDataTable(string storedProcedureName, params SqlParameter[] parameters)
        {
            var dt = new DataTable();
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                using (var cmd = new SqlCommand(storedProcedureName, conn))
                {
                    cmd.CommandType = DetermineCommandType(storedProcedureName);
                    if (parameters != null && parameters.Length > 0)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }

                    using (var da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }
            return dt;
        }

        public static int ExecuteNonQuery(string storedProcedureName, params SqlParameter[] parameters)
        {
            int rowsAffected;
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                using (var cmd = new SqlCommand(storedProcedureName, conn))
                {
                    cmd.CommandType = DetermineCommandType(storedProcedureName);
                    if (parameters != null && parameters.Length > 0)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }

                    conn.Open();
                    rowsAffected = cmd.ExecuteNonQuery();
                }
            }
            return rowsAffected;
        }

        public static object ExecuteScalar(string storedProcedureName, params SqlParameter[] parameters)
        {
            object result;
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                using (var cmd = new SqlCommand(storedProcedureName, conn))
                {
                    cmd.CommandType = DetermineCommandType(storedProcedureName);
                    if (parameters != null && parameters.Length > 0)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }

                    conn.Open();
                    result = cmd.ExecuteScalar();
                }
            }
            return result;
        }
    }
}
