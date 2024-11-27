using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using String = System.String;

namespace MKWiseM
{
    internal static class AppConfigUtil
    {
        public static event EventHandler<MessageEventArgs> MessageDeploy;

        private static readonly Configuration configModifier = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        private static readonly ConnectionStringsSection conSection = (ConnectionStringsSection) configModifier.GetSection("connectionStrings");

        private const string ProviderName = "Microsoft.Data.SqlClient";
        private const string CON_NAME = "WiseM";
        private const string REIDX_T = "ReidxTable";
        public static string ConnectionStrings => conSection.ConnectionStrings[CON_NAME]?.ConnectionString;

        public static string IP => ConnectionStrings?.Split(';')[0].Split('=')[1] ?? "-";

        public static string Catalog
        {
            get
            {
                if (string.IsNullOrEmpty(ConnectionStrings))
                {
                    return "";
                }

                var builder = new SqlConnectionStringBuilder(ConnectionStrings);
                return builder.InitialCatalog ?? "-";
            }
        }

        public static void SetupConnection(string ip, string id, string pw, string catalog = "")
        {
            Console.WriteLine($"{ip} -- {catalog}");
            var conField = conSection.ConnectionStrings[CON_NAME];
            var newConStr = CreateConStr(ip, id, pw, catalog);

            //app.config내에 <connectionStrings><WiseM></></> 필드 없을경우
            if (conField == null)
            {
                conSection.ConnectionStrings.Add(new ConnectionStringSettings(CON_NAME, newConStr, ProviderName));
            }
            else //필드 존재시
            {
                conField.ConnectionString = newConStr;
                conField.ProviderName = ProviderName;
            }
            
            EncryptConStr();
            SaveConfig();

            InvokeMessage("Datasource Saved");
        }

        public static HashSet<String> LoadTables()
        {
            HashSet<String> tableSet;
            string tables = ConfigurationManager.AppSettings[REIDX_T];
            if (!string.IsNullOrEmpty(tables))
            {
                tableSet = new HashSet<String>(tables.Split(',').Select(t => t.Trim()));
            }
            else
            {
                tableSet = new HashSet<String>
                {
                    "WorkOrder", "KeyRelation_Ec", "KeyRelation_Ec_Bkup", "OutputHist", "PackingHist", "Rm_Stock",
                    "Rm_StockHist", "SearchKey", "Stock", "StockHist"
                };
            }

            return tableSet;
        }

        public static void SetTables(HashSet<String> tables)
        {
            var csvTables = string.Join(", ", tables);
            if (configModifier.AppSettings.Settings[REIDX_T] != null) //수정
            {
                configModifier.AppSettings.Settings[REIDX_T].Value = csvTables;
            }
            else //추가
            {
                configModifier.AppSettings.Settings.Add(REIDX_T, csvTables);
            }

            SaveConfig();
        }

        private static void InvokeMessage(string message, string errorLog = "")
        {
            MessageDeploy?.Invoke(null,
                !string.IsNullOrEmpty(errorLog)
                    ? new MessageEventArgs(message, errorLog)
                    : new MessageEventArgs(message)
                );
            
        }

        private static string CreateConStr(string ip, string id, string pw, string catalog = "")
        {

            var conStrBuilder = new SqlConnectionStringBuilder
            {
                DataSource = ip,
                UserID = id,
                Password = pw,
                ConnectTimeout = 5
            };

            if (!string.IsNullOrEmpty(catalog)) conStrBuilder.InitialCatalog = catalog;

            return conStrBuilder.ToString();
        }

        private static void EncryptConStr()
        {
            if (!conSection.SectionInformation.IsProtected)
            {
                conSection.SectionInformation.ProtectSection("DataProtectionConfigurationProvider");
            }
        }

        private static void SaveConfig()
        {
            configModifier.Save(ConfigurationSaveMode.Full);
            //ConfigurationManager.RefreshSection("connectionStrings"); // RefreshSection 불필요
        }
    }
}
