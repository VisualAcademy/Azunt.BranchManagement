using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Azunt.BranchManagement
{
    public class BranchesTableBuilder
    {
        private readonly string _masterConnectionString;
        private readonly ILogger<BranchesTableBuilder> _logger;

        public BranchesTableBuilder(string masterConnectionString, ILogger<BranchesTableBuilder> logger)
        {
            _masterConnectionString = masterConnectionString;
            _logger = logger;
        }

        public void BuildTenantDatabases()
        {
            var tenantConnectionStrings = GetTenantConnectionStrings();

            foreach (var connStr in tenantConnectionStrings)
            {
                try
                {
                    EnsureBranchesTable(connStr);
                    _logger.LogInformation($"Branches table processed (tenant DB): {connStr}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[{connStr}] Error processing tenant DB");
                }
            }
        }

        public void BuildMasterDatabase()
        {
            try
            {
                EnsureBranchesTable(_masterConnectionString);
                _logger.LogInformation("Branches table processed (master DB)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing master DB");
            }
        }

        private List<string> GetTenantConnectionStrings()
        {
            var result = new List<string>();

            using (var connection = new SqlConnection(_masterConnectionString))
            {
                connection.Open();
                var cmd = new SqlCommand("SELECT ConnectionString FROM dbo.Tenants", connection);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var connectionString = reader["ConnectionString"]?.ToString();
                        if (!string.IsNullOrEmpty(connectionString))
                        {
                            result.Add(connectionString);
                        }
                    }
                }
            }

            return result;
        }

        private void EnsureBranchesTable(string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Check if 'Branches' table exists
                var cmdCheck = new SqlCommand(@"
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_NAME = 'Branches'", connection);

                int tableCount = (int)cmdCheck.ExecuteScalar();

                if (tableCount == 0)
                {
                    // Create 'Branches' table if it doesn't exist
                    var cmdCreate = new SqlCommand(@"
                        -- [0][0] 브랜치: Branches 
                        CREATE TABLE [dbo].[Branches] (
                            [Id]              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,         -- 자동 증가하는 고유 ID
                            [BranchName]      NVARCHAR(100) NULL,                             -- 지점 이름
                            [Location]        NVARCHAR(255) NULL,                             -- 지점 위치 (주소 또는 지역)
                            [ContactNumber]   NVARCHAR(20) NULL,                              -- 지점 연락처 번호
                            [EstablishedDate] DATE NULL,                                      -- 지점 설립일
                            [IsActive]        BIT NULL DEFAULT(1)                             -- 지점 활성 상태 (1: Active, 0: Inactive)
                        );
                    ", connection);

                    cmdCreate.ExecuteNonQuery();
                    _logger.LogInformation("Branches table created.");
                }

                // Check and add missing columns (if any)
                var expectedColumns = new Dictionary<string, string>
                {
                    // All columns from the Alls table
                    ["IsActive"] = "BIT DEFAULT 1 NULL"
                };

                foreach (var (columnName, columnDefinition) in expectedColumns)
                {
                    var cmdColCheck = new SqlCommand(@"
                        SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = 'Branches' AND COLUMN_NAME = @ColumnName", connection);
                    cmdColCheck.Parameters.AddWithValue("@ColumnName", columnName);

                    int columnExists = (int)cmdColCheck.ExecuteScalar();

                    if (columnExists == 0)
                    {
                        var cmdAlter = new SqlCommand($@"
                            ALTER TABLE [dbo].[Branches] 
                            ADD [{columnName}] {columnDefinition}", connection);
                        cmdAlter.ExecuteNonQuery();

                        _logger.LogInformation($"Column added to Branches: {columnName} ({columnDefinition})");
                    }
                }

                // Insert default rows if the table is empty
                // Insert default rows if the Branches table is empty
                var cmdCountRows = new SqlCommand("SELECT COUNT(*) FROM [dbo].[Branches]", connection);
                int rowCount = (int)cmdCountRows.ExecuteScalar();

                if (rowCount == 0)
                {
                    var cmdInsertDefaults = new SqlCommand(@"
                        INSERT INTO [dbo].[Branches] (BranchName, Location, ContactNumber, EstablishedDate, IsActive)
                        VALUES
                            (N'Head Office', N'Initial City A', N'123-456-7890', '2020-01-01', 1),
                            (N'Branch #2', N'Initial City B', N'987-654-3210', '2022-05-15', 1);
                    ", connection);

                    int inserted = cmdInsertDefaults.ExecuteNonQuery();
                    _logger.LogInformation($"Inserted default branches: {inserted} rows.");
                }
            }
        }

        // Run method to call EnhanceMasterDatabase or EnhanceTenantDatabases
        public static void Run(IServiceProvider services, bool forMaster, string? optionalConnectionString = null)
        {
            try
            {
                var logger = services.GetRequiredService<ILogger<BranchesTableBuilder>>();
                var config = services.GetRequiredService<IConfiguration>();

                string connectionString;

                if (!string.IsNullOrWhiteSpace(optionalConnectionString))
                {
                    connectionString = optionalConnectionString;
                }
                else
                {
                    var tempConnectionString = config.GetConnectionString("DefaultConnection");
                    if (string.IsNullOrEmpty(tempConnectionString))
                    {
                        throw new InvalidOperationException("DefaultConnection is not configured in appsettings.json.");
                    }

                    connectionString = tempConnectionString;
                }

                var builder = new BranchesTableBuilder(connectionString, logger);

                if (forMaster)
                {
                    builder.BuildMasterDatabase();
                }
                else
                {
                    builder.BuildTenantDatabases();
                }
            }
            catch (Exception ex)
            {
                var fallbackLogger = services.GetService<ILogger<BranchesTableBuilder>>();
                fallbackLogger?.LogError(ex, "Error while processing Branches table.");
            }
        }
    }
}