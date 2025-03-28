using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MKWiseM
{
    internal static class DBUtil
    {
        public static event EventHandler<MessageEventArgs> MessageDeploy;

        public static async Task<bool> IsValidConnectionAsync()
        {
            var conStr = AppConfigUtil.ConnectionStrings;
            if (string.IsNullOrEmpty(conStr)) return false;
            
            try
            {
                using (var connection = new SqlConnection(conStr))
                {
                    await connection.OpenAsync();
                    InvokeMessage("Connected");
                }
                return true;
            }
            catch (SqlException ex)
            {
                Console.WriteLine(ex.StackTrace);
                InvokeMessage("Failed to connect database", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                InvokeMessage("Unknown Error", ex.Message);
                return false;
            }
        }

        private static DataSet CallProcedure(
            string procedureName, 
            SqlParameter[] inputParams,
            SqlParameter[] outputParams,
            bool handlePrintEvent = false
            )
        {
            using (var sqlConnection = new SqlConnection(AppConfigUtil.ConnectionStrings))
            {
                if (handlePrintEvent)
                {
                    sqlConnection.InfoMessage += (sender, args) =>
                    {
                        foreach (SqlError argsError in args.Errors)
                        {
                            InvokeMessage(argsError.Message, argsError.Message);
                        }
                    };
                }

                using (var cmd = new SqlCommand(procedureName, sqlConnection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 0;

                    //INPUT
                    if (inputParams != null)
                    {
                        foreach (SqlParameter inputParam in inputParams)
                        {
                            inputParam.Direction = ParameterDirection.Input;
                            cmd.Parameters.Add(inputParam);
                        }
                    }

                    //OUTPUT
                    if (outputParams != null)
                    {
                        foreach (SqlParameter outputParam in outputParams)
                        {
                            outputParam.Direction = ParameterDirection.Output;
                            cmd.Parameters.Add(outputParam);
                        }
                    }

                    
                    //Execute
                    try
                    {
                        sqlConnection.Open();
                        var ds = new DataSet();

                        using (var da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(ds);
                        }

                        return ds;
                    }
                    catch (SqlException ex)
                    {
                        Console.WriteLine("NUMBER:::" + ex.Number);
                        Console.WriteLine(ex);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }
            }
        }

        public static object ExecuteScalar(string query)
        {
            try
            {
                var dt = GetDataTable(query);
                return dt.Rows.Count != 1 ? null : dt.Rows[0][0];
            }
            catch (Exception e)
            {
                InvokeMessage(e.Message, e.Message);
            }

            return null;
        }
        public static async Task InsertBinaryFile(string query, byte[] rawData)
        {
            await Task.Run(() => ExecuteNonQuery(query, rawData, true));
        }

        /// <summary>
        /// DataReader가 CLOSE될때 Connection도 같이 CLOSE (CommandBehavior.CloseConnection)
        /// </summary>
        public static async Task<SqlDataReader> GetDataReaderAsync(string query)
        {
            var connection = new SqlConnection(AppConfigUtil.ConnectionStrings);
            connection.Open();
            var cmd = new SqlCommand(query, connection);

            return await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection | CommandBehavior.SequentialAccess);
        }
        
        private static void ExecuteNonQuery(string query, byte[] rawData = null, bool invokePrint = false)
        {
            using (var sqlConnection = new SqlConnection(AppConfigUtil.ConnectionStrings))
            {
                if (invokePrint)
                {
                    sqlConnection.InfoMessage += (sender, args) =>
                    {
                        foreach (SqlError argsError in args.Errors)
                        {
                            InvokeMessage(argsError.Message, argsError.Message);
                        }
                    };
                }
                
                sqlConnection.Open();

                using (var cmd = new SqlCommand(query, sqlConnection))
                {
                    if(rawData == null) cmd.ExecuteNonQuery();
                    else
                    {
                        cmd.Parameters.Add("@rawData", SqlDbType.VarBinary).Value = rawData;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        } 
        private static DataTable GetDataTable(string query)
        {
            var dt = new DataTable();
            try
            {
                using (var sqlConnection = new SqlConnection(AppConfigUtil.ConnectionStrings))
                {
                    sqlConnection.Open();

                    using (var cmd = new SqlCommand(query, sqlConnection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                InvokeMessage(ex.Message, ex.Message);
            }

            return dt;
        }

        /// <summary>
        /// .NET Framework 4.0 ++
        /// </summary>
        public static async Task<DataTable> GetDataTableAsync(string query)
        {
            var dt = new DataTable();

            try
            {
                using (var sqlConnection = new SqlConnection(AppConfigUtil.ConnectionStrings))
                {
                    await sqlConnection.OpenAsync();

                    using (var cmd = new SqlCommand(query, sqlConnection))
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(true))
                    {
                        await Task.Run(() => dt.Load(reader));
                    }
                }
            }
            catch (Exception e)
            {
                InvokeMessage(e.Message, e.Message);
            }

            return dt;
        }

        /// <summary>
        /// .NET Framework 3.5 ++ 
        /// </summary>
        public static void GetDataTableTask(string query, Action<DataTable> onCompleted)
        {
            Task.Run(() =>
            {
                try
                {
                    var dt = GetDataTable(query);
                    onCompleted?.Invoke(dt);
                    //onCompleted(dt); // 문제 없음.. 그러나 문제발생 가능성 ( NULL + 크로스 스레드 예외 등 )
                }
                catch (Exception ex)
                {
                    InvokeMessage(ex.Message, ex.Message);
                }
            });
        }

        /// <summary>
        /// .NET Framework 3.5 ++ 
        /// </summary>
        public static void CallExecuteReindex(List<String> tables, Action<DataTable> onCompleted)
        {
            if (tables.Count == 0)
            {
                InvokeMessage(nameof(CallExecuteReindex) +": NO TABLES");
                return;
            }

            var csvTables = string.Join(",", tables);
            Task.Run(() =>
            {
                try
                {
                    var inputParam = new SqlParameter("@TableNames", SqlDbType.NVarChar)
                    {
                        Value = csvTables
                    };

                    var outputParam = new SqlParameter("@ReturnMessage", SqlDbType.NVarChar, -1)
                    {
                        Direction = ParameterDirection.Output
                    };

                    var ds = CallProcedure("dbo.ExecuteReIndex",
                        inputParams: new[] { inputParam },
                        outputParams: new[] { outputParam },
                        handlePrintEvent: true);

                    onCompleted?.Invoke(ds.Tables[0]);
                    InvokeMessage("ReIndex 완료", outputParam.Value?.ToString());
                }
                catch (Exception ex)
                {
                    onCompleted?.Invoke(null);
                    InvokeMessage(ex.Message, ex.Message);
                }
            });
        }

        public static void CallGetScanDensity(List<String> tables, Action<DataTable> onCompleted)
        {
            if (tables.Count == 0)
            {
                InvokeMessage(nameof(CallGetScanDensity) + ": NO TABLES");
                return;
            }

            var csvTables = string.Join(",", tables);
            Task.Run(() =>
            {
                try
                {
                    var inputParam = new SqlParameter("@TableNames", SqlDbType.NVarChar)
                    {
                        Value = csvTables
                    };

                    DataSet ds = CallProcedure("dbo.GetScanDensity", new[] { inputParam }, null, true);
                    onCompleted?.Invoke(ds.Tables[0]);
                    InvokeMessage("Table Loaded.");
                }
                catch (Exception ex)
                {
                    onCompleted?.Invoke(null);
                    InvokeMessage(ex.Message, ex.Message);
                }
            });
        }

        public static List<string> TableToList(DataTable dt, int colPos = 0)
        {
            var list = dt.AsEnumerable()
                .Select(row => row[colPos].ToString())
                .ToList();
            return list;
        }

        /// <summary>
        /// .NET Framework 3.5 ++ 
        /// </summary>
        public static void InstallProcdureTask(Action onCompleted)
        {
            Task.Run(() =>
            {
                try
                {
                    ExecuteNonQuery(StoredProcedureInstaller.ExecuteReIndex(),null, true);
                    ExecuteNonQuery(StoredProcedureInstaller.GetScanDensity(),null, true);
                    onCompleted?.Invoke();
                }
                catch (Exception ex)
                {
                    InvokeMessage(ex.Message, ex.Message);
                }
            });
        }

        /// <summary>
        /// .NET Framework 3.5 ++ 
        /// </summary>
        public static Task ClearLogTask()
        {
            return Task.Run(() =>
            {
                try
                {
                    ExecuteNonQuery(LongQuery.ClearLogQuery(), null, true);
                }
                catch (Exception ex)
                {
                    InvokeMessage(ex.Message, ex.Message);
                }
            });
        }

        public static List<string> GetDiskInfo()
        {
            try
            {
                EnableCLR();
                var dt = GetDataTable(LongQuery.DiskInfoQuery());

                return (from DataRow row in dt.Rows
                        select row["Output"].ToString()).ToList();
            }
            finally
            {
                DisableCLR();
            }
        }

        private static void EnableCLR()
        {
            const string query = @"
                EXEC sp_configure 'show advanced options', 1;
                RECONFIGURE;
                EXEC sp_configure 'xp_cmdshell', 1;
                RECONFIGURE;
            ";

            ExecuteNonQuery(query);
        }

        private static void DisableCLR()
        {
            const string query = @"
                EXEC sp_configure 'xp_cmdshell', 0;
                RECONFIGURE;
                EXEC sp_configure 'show advanced options', 0;
                RECONFIGURE;
            ";

            ExecuteNonQuery(query);
        }

        private static void InvokeMessage(string message, string errorLog = "")
        {
            MessageDeploy?.Invoke(null,
                !string.IsNullOrEmpty(errorLog)
                    ? new MessageEventArgs(message, errorLog)
                    : new MessageEventArgs(message));
        }
    }
}
