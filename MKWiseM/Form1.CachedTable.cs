using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKWiseM
{
    public partial class Form1
    {
        private readonly DataTable _dtCachedTable = new DataTable
        {
            Columns =
            { 
                "SchemaName",
                "TableName",
                "TotalMB",
                "CachedMB",
                "MB_Percent",
                "TotalPage",
                "CachedPage"
            }
        };

        private bool _subscribedCachedTable = false;
        private void tabCachedT_Enter(object sender, EventArgs e)
        {
            if (IsDbInitialized())
            {
                EnableCompsCachedTable();
                if (!_subscribedCachedTable)
                {
                    this.dataCatalogChanged += (_, catalog) =>
                    {
                        _dtCachedTable.Rows.Clear();
                    };
                    _subscribedCachedTable = true;
                }
            }
            else
            {
                DisableCompsCachedTable();
            }
        }

        private void EnableCompsCachedTable()
        {
            btnCachedTableLoad.Enabled = true;
        }

        private void DisableCompsCachedTable()
        {
            btnCachedTableLoad.Enabled = false;
            _dtCachedTable.Rows.Clear();
        }

        private async void btnCachedTableLoad_Click(object sender, EventArgs e)
        {
            _dtCachedTable.Rows.Clear();

            tsPbar.Visible = true;
            var fetchedDt = await GetCachedTable();

            _dtCachedTable.BeginLoadData();
            try
            {
                foreach (DataRow fetchedDtRow in fetchedDt.Rows)
                {
                    _dtCachedTable.LoadDataRow(fetchedDtRow.ItemArray, false);
                }
            }
            finally
            {
                _dtCachedTable.EndLoadData();
                tsPbar.Visible = false;
            }
            
            dGridCachedTable.DataSource = this._dtCachedTable;
        }

        private async Task<DataTable> GetCachedTable()
        {
            return await DBUtil.GetDataTableAsync(LongQuery.LoadCachedTableQuery());
        }

        private void chkCachedOnly_CheckedChanged(object sender, EventArgs e)
        {
            var dView = _dtCachedTable.DefaultView;
            dView.RowFilter = chkCachedOnly.Checked ? "CachedMB IS NOT NULL" : string.Empty;
        }
    }
}
