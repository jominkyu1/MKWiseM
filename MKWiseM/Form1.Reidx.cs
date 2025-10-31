using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MKWiseM
{
    public partial class Form1
    {

        private void btnAddTable_Click(object sender, EventArgs e)
        {
            var inputT = txtAddTable.Text.Trim();
            if (string.IsNullOrEmpty(inputT) || !isConnected) return;

            const string pattern = @"^[\w\d_]+$";
            if (!Regex.IsMatch(inputT, pattern))
            {
                MessageBox.Show("Not allowed text (ex. ', %, ==)");
                txtAddTable.Text = "";
                txtAddTable.Focus();
                return;
            }

            var result = this.reidxList.Add(inputT);
            if (result)
            {
                btnLoadList_Click(this, EventArgs.Empty);
                MessageBox.Show($"ADDED {inputT}");
                txtAddTable.Text = "";
                txtAddTable.Focus();
            }
        }

        private void txtAddTable_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnAddTable_Click(this, EventArgs.Empty);
            }
        }

        private void btnDeleteTable_Click(object sender, EventArgs e)
        {
            var selected = txtAddTable.Text.Trim();

            var result = this.reidxList.RemoveWhere(s => s.ToLower() == selected.ToLower());
            if (result > 0)
            {
                MessageBox.Show($"DELETED {selected}");
                btnLoadList_Click(this, EventArgs.Empty);
                txtAddTable.Text = "";
                txtAddTable.Focus();
            }
            else
            {
                MessageBox.Show("NOT FOUND");
            }
        }


        private void btnTableInformation_Click(object sender, EventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("DB Not Connected");
                return;
            }

            var items = chkListTables.CheckedItems;
            if (items.Count == 0)
            {
                MessageBox.Show("Table Not Selected");
                return;
            }

            HashSet<String> targetList = new HashSet<String>();
            foreach (DataRowView item in items)
            {
                targetList.Add(item["TableName"].ToString());
            }

            btnTableInformation.Enabled = false;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            //프로시저 호출
            DBUtil.CallGetScanDensity(targetList.ToList(),
                onCompleted: ds =>
                {
                    dGridReIdx?.Invoke(new Action(() =>
                    {
                        if (ds != null)
                        {
                            dGridReIdx.DataSource = ds;
                            dGridReIdx.Columns[1].DefaultCellStyle.Format = "N0";
                        }
                    }));
                    stopwatch.Stop();

                    var eTime = stopwatch.ElapsedMilliseconds;
                    lblElapsedTime?.Invoke(new Action(() => lblElapsedTime.Text = $"{eTime} ms"));
                    btnTableInformation?.Invoke(new Action(() => btnTableInformation.Enabled = true));
                });
        }

        private void btnReIdx_Click(object sender, EventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("DB Not Connected");
                return;
            }

            var items = chkListTables.CheckedItems;
            if (items.Count == 0)
            {
                MessageBox.Show("Table Not Selected");
                return;
            }

            HashSet<String> targetList = new HashSet<String>();
            foreach (DataRowView item in items)
            {
                targetList.Add(item["TableName"].ToString());
            }

            btnReIdx.Enabled = false;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            //프로시저 호출
            DBUtil.CallExecuteReindex(targetList.ToList(),
                onCompleted: ds =>
                {
                    dGridReIdx?.Invoke(new Action(() =>
                    {
                        if (ds != null)
                        {
                            dGridReIdx.DataSource = ds;
                            dGridReIdx.Columns[1].DefaultCellStyle.Format = "N0";
                        }
                    }));
                    stopwatch.Stop();

                    var eTime = stopwatch.ElapsedMilliseconds;
                    lblElapsedTime?.Invoke(new Action(() => lblElapsedTime.Text = $"{eTime} ms"));
                    btnReIdx?.Invoke(new Action(() => btnReIdx.Enabled = true));
                });
        }


        private async void btnClearLog_Click(object sender, EventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("DB Not Connected");
                return;
            }

            await DBUtil.ClearLogTask();

        }

        private async void btnLoadList_Click(object sender, EventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("DB Not connected");
                return;
            }

            var query = LongQuery.TableExistsQuery(reidxList.ToList());

            var table = await DBUtil.GetDataTableAsync(query);
            if (table.Rows.Count == 0) return;

            var filteredRows = table.Select("Result = 1");
            var dt = filteredRows.Any() ? filteredRows.CopyToDataTable() : null;

            chkListTables.DataSource = dt;
            chkListTables.DisplayMember = "TableName";
            chkListTables.ValueMember = "TableName";

            var noTableList = table.AsEnumerable()
                .Where(it => it["Result"].ToString() == "0")
                .Select(it => it["TableName"].ToString())
                .ToList();

            lblNoTables.Text = noTableList.Any() ? string.Join(", ", noTableList) : "-";

            CheckAllItems();

            bool isCombobox = sender is ComboBox;
            if (!isCombobox)
                SetupCatalogList();
        }

        private void CheckAllItems()
        {
            for (int i = 0; i < chkListTables.Items.Count; i++)
            {
                chkListTables.SetItemChecked(i, true);
            }
        }

        private static DataView GetDiskInfo()
        {
            var dt = new DataTable();

            List<string> temp = DBUtil.GetDiskInfo();
            if (temp.Count > 1)
            {
                //split header(ex. DeviceID, FreeSpace, Size)
                var splitHeaders = temp[0].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string s in splitHeaders)
                {
                    dt.Columns.Add(s);
                }

                //Body (ex. C:, 9999, 9999)
                for (int i = 1; i < temp.Count; i++)
                {
                    var splitBodys = temp[i].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (splitHeaders.Length == splitBodys.Length)
                    {
                        object[] rowVal = new Object[splitBodys.Length];

                        for (int j = 0; j < rowVal.Length; j++)
                        {
                            if (long.TryParse(splitBodys[j], out long pVal))
                            {
                                rowVal[j] = BytesToGB(pVal);
                            }
                            else
                            {
                                rowVal[j] = splitBodys[j];
                            }
                        }

                        dt.Rows.Add(rowVal);
                    }
                }
            }
            else
            {
                MessageBox.Show("No Drive found");
            }

            return new DataView(dt) { Sort = "DeviceID ASC" };
        }

        private void btnGetDrive_Click(object sender, EventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("DB Not Connected");
                return;
            }
            var dView = GetDiskInfo();
            dGridDiskInfo.DataSource = dView;
        }
    }
}
