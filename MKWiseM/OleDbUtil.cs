using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MKWiseM
{
    internal static class OleDbUtil
    {
        private const string OleJetProvider = "Microsoft.Jet.OLEDB.4.0";
        private const string OleAceProvider = "Microsoft.ACE.OLEDB.12.0";

        public static event EventHandler<MessageEventArgs> MessageDeploy;

        public static async Task<DataTable> LoadExcelToDataTable(string filepath)
        {
            var dt = new DataTable();

            try
            {
                string extension = Path.GetExtension(filepath);
                string connectionString = "";
                switch (extension)
                {
                    case ".xls":
                        connectionString = $"Provider={OleJetProvider};Data Source={filepath};Extended Properties='Excel 8.0;HDR=Yes;IMEX=1;'";
                        break;
                    case ".xlsx":
                        connectionString = $"Provider={OleAceProvider};Data Source={filepath};Extended Properties='Excel 12.0;HDR=Yes;IMEX=1;'";
                        break;
                    default:
                        InvokeMessage("Invalid file extension", "Invalid file extension");
                        return dt;
                }

                using (var oleDbConnection = new OleDbConnection(connectionString))
                {
                    await oleDbConnection.OpenAsync();

                    DataTable sheets = oleDbConnection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
                    if (sheets == null || sheets.Rows.Count == 0)
                    {
                        InvokeMessage("No Sheets in Excel", "No Sheets in Excel");
                        return dt;
                    }

                    string sheetName = sheets.Rows[0]["TABLE_NAME"].ToString();
                    string query = $"SELECT * FROM [{sheetName}]";
                    OleDbDataAdapter adapter = new OleDbDataAdapter(query, oleDbConnection);
                    await Task.Run(() => adapter.Fill(dt));
                }

                return dt;
            }
            catch (Exception ex)
            {
                InvokeMessage(ex.Message, ex.Message);
                return dt;
            }
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
