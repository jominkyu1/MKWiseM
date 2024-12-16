using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace MKWiseM
{
    public partial class Form1
    {
        private Dictionary<int, string> compLevel = null;

        private void tabInformation_Enter(object sender, EventArgs e)
        {
            if (IsDbInitialized())
            {
                SetupCompLevel();
                SetupLabels();
            }
            else
            {
                DisableLabels();
            }
        }

        private void SetupLabels()
        {
            const string versionQuery = "SELECT @@version";
            string compQuery = $"SELECT compatibility_level FROM sys.databases WHERE name = '{AppConfigUtil.Catalog}'";
            var compVersion = int.Parse(DBUtil.ExecuteScalar(compQuery).ToString());

            this.lblSqlVersion.Text = DBUtil.ExecuteScalar(versionQuery).ToString();
            this.lblCompLevel.Text = $"[{compVersion}] {compLevel[compVersion]}";
        }

        private void DisableLabels()
        {
            this.lblSqlVersion.Text = "";
            this.lblCompLevel.Text = "";
        }

        private async void txtFindColumn_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                await ExecuteFindColumnAsync();
            }
        }

        private async void btnFindColumn_Click(object sender, EventArgs e)
        {
            await ExecuteFindColumnAsync();
        }

        private async Task ExecuteFindColumnAsync()
        {
            this.btnFindColumn.Enabled = false;
            this.txtFindColumn.Enabled = false;
            this.lblFinding.Visible = true;
            try
            {
                var text = txtFindColumn.Text.Trim();
                dGridFindColumn.DataSource = await FindColumn(text);
            }
            finally
            {
                this.btnFindColumn.Enabled = true;
                this.txtFindColumn.Enabled = true;
                this.lblFinding.Visible = false;

                this.txtFindColumn.Focus();
            }
        }

        private async Task<DataTable> FindColumn(string column)
        {
            if (string.IsNullOrEmpty(column)) return new DataTable();
            return await DBUtil.GetDataTableAsync(LongQuery.ColumnFindQuery(column));
        }

        private void dGridFindColumn_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            int rowCount = this.dGridFindColumn.Rows.Count;
            this.lblFindColumnRows.Text = rowCount.ToString();
        }

        /// <summary>
        /// <a href="https://learn.microsoft.com/ko-kr/sql/t-sql/statements/alter-database-transact-sql-compatibility-level?view=sql-server-ver16#supported-dbcompats">
        /// 공식문서
        /// </a>
        /// </summary>
        private void SetupCompLevel()
        {
            if (compLevel == null)
            {
                compLevel = new Dictionary<int, string>
                {
                    {160, "SQL Server 2022 (16.x)"},
                    {150, "SQL Server 2019 (15.x)"},
                    {140, "SQL Server 2017 (14.x)"},
                    {130, "SQL Server 2016 (13.x)"},
                    {120, "SQL Server 2014 (12.x)"},
                    {110, "SQL Server 2012 (11.x)"},
                    {100, "SQL Server 2008 (10.0.x)"},
                    {90, "SQL Server 2005 (9.x)"},
                    {80, "SQL Server 2000 (8.x)"},
                };
            }
        }
    }
}
