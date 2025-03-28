using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MKWiseM
{
    public partial class Form1
    {
        private bool isManualOutReady = false;
        private string _rmBarcodeColumnName = "Rm_Barcode";


        private async void btnLoadExcel_Click(object sender, EventArgs e)
        {
            string filepath = OpenFileDialog();
            if (string.IsNullOrEmpty(filepath))
                return;

            DataTable dt = await OleDbUtil.LoadExcelToDataTable(filepath);
            dGridFromExcel.DataSource = dt;
            lblExcelRowsCount.Text = dGridFromExcel.Rows.Count.ToString();
        }

        private bool IsBarcodesValidated(List<string> rmBarcodes)
        {
            // Length not 51
            bool isBcdLengthWrong = rmBarcodes.Any(bcd => bcd.Length != 51);
            if (isBcdLengthWrong)
            {
                MessageBox.Show("문자열 길이가 51자가 아닌것이 존재합니다.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // RawMaterial GroupBy
            List<string> groupByRawMaterial = rmBarcodes.GroupBy(bcd => bcd.Substring(0, 10))
                .Select(group => group.Key)
                .ToList();
            if (groupByRawMaterial.Count > 1)
            {
                string groupStrJoin = string.Join("\r\n", groupByRawMaterial);
                MessageBox.Show("서로 다른 RawMaterial 발견!\r\n\r\n" + groupStrJoin, "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        private async void btnErpCheck_Click(object sender, EventArgs e)
        {
            isManualOutReady = false;

            #region Validate Excel
            //////Validate Excel//////
            try
            {
                List<string> rmBarcodes = dGridFromExcel.Rows.Cast<DataGridViewRow>()
                    .Select(row => row.Cells[_rmBarcodeColumnName].Value.ToString().Trim())
                    .ToList();

                if (IsBarcodesValidated(rmBarcodes) == false)
                    return;


                // TotalQty
                string totalQty = rmBarcodes.Select(bcd => Convert.ToDecimal(bcd.Substring(38, 9))).Sum().ToString("F2");
                lblStockTotalQty.Text = totalQty;

                // Stock Check
                string rmBarcodesCsv = string.Join(", ", rmBarcodes);
                string checkQuery = $@"
                    DECLARE @Bcds VARCHAR(MAX) = '{rmBarcodesCsv}'
                    DECLARE @XMLBcds XML
                    SELECT @XMLBcds = CONVERT(XML, '<Bcd>' + REPLACE(@Bcds, ',', '</Bcd><Bcd>') + '</Bcd>')


                    ;With BcdTable AS (
                    SELECT RTRIM(LTRIM(Bcds.Bcd.value('.', 'NVARCHAR(255)'))) as Bcd
	                    FROM @XMLBcds.nodes('/Bcd') as Bcds(Bcd)
									                    )
                    SELECT
	                    (
		                    SELECT
			                    COUNT(*)
		                    FROM Rm_Stock WITH(NOLOCK)
		                    WHERE Rm_Barcode IN ( SELECT Bcd FROM BcdTable )
	                    ) AS [Rm_Stock_Count]
                    , (
		                    SELECT
			                    COUNT(*)
		                    FROM Rm_StockHist sh WITH(NOLOCK)
		                    WHERE Rm_Barcode IN ( SELECT Bcd FROM BcdTable )
	                      AND NOT EXISTS (
	    							                     SELECT 1
	    							                     FROM Rm_StockHist WITH(NOLOCK)
	    							                     WHERE Rm_Barcode = sh.Rm_Barcode
	    							                     AND   Rm_Io_Type = 'OUT'
	    							                     AND   Rm_Updated > sh.Rm_Updated
									                     )
	                    ) AS [Rm_StockHist_Count]
                    , (
                            SELECT TOP 1 Rm_Material FROM Rm_Stock WITH(NOLOCK) WHERE Rm_Barcode IN ( SELECT Bcd FROM BcdTable )
                      )   AS [Rm_Material]
                 ";
                DataTable dt = await DBUtil.GetDataTableAsync(checkQuery);
                if (dt.Rows.Count == 1)
                {
                    lblRmStockCount.Text = dt.Rows[0]["Rm_Stock_Count"].ToString();
                    lblRmStockHistCount.Text = dt.Rows[0]["Rm_StockHist_Count"].ToString();
                    lblTargetRawMaterial.Text = dt.Rows[0]["Rm_Material"].ToString();
                }
                else
                {
                    lblRmStockCount.Text = "ERROR";
                    lblRmStockHistCount.Text = "ERROR";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateMessage(ex.Message);
                lblRmStockCount.Text = "ERROR";
                lblRmStockHistCount.Text = "ERROR";
                lblStockTotalQty.Text = "ERROR";
                return;
            }

            if (
                !int.TryParse(lblExcelRowsCount.Text, out int excelRowsCount) ||
                !int.TryParse(lblRmStockCount.Text, out int rmStockCount) ||
                !int.TryParse(lblRmStockHistCount.Text, out int rmStockHistCount)
            )
            {
                MessageBox.Show("Wrong Count", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (excelRowsCount == rmStockCount &&
                excelRowsCount == rmStockHistCount)
            {
                lblStockCountCheck.ForeColor = System.Drawing.Color.Green;
                lblStockCountCheck.Text = "EXCEL OK";
            }
            else
            {
                lblStockCountCheck.ForeColor = System.Drawing.Color.Red;
                lblStockCountCheck.Text = "EXCEL NG";
                return;
            }

            #endregion

            #region Validate ERP

            string inputErp = txtErpCheck.Text.Trim();
            string targetRawMaterial = lblTargetRawMaterial.Text;

            string erpCheckAndGetReqQty = $@"
                    SELECT CONVERT(DECIMAL(18,2), req_qty_1) as req_qty_1 
                    FROM MESDB.DBO.AINFMOVREQ
                    WHERE i_apply_status = 'R' AND
			              req_id = '{inputErp}' AND
			              inv_mat_id = '{targetRawMaterial}'
                ";
            try
            {
                string reqQty = DBUtil.ExecuteScalar(erpCheckAndGetReqQty)?.ToString() ??
                                throw new Exception($"{inputErp}, {targetRawMaterial} -> No Results");

                if (string.IsNullOrEmpty(reqQty))
                {
                    lblErpCheckResult.ForeColor = System.Drawing.Color.Red;
                    lblErpCheckResult.Text = "ERP NG";
                    lblErpReqQty.Text = "0";
                }
                else
                {
                    lblErpCheckResult.ForeColor = System.Drawing.Color.Green;
                    lblErpCheckResult.Text = "ERP OK";
                    lblErpReqQty.Text = reqQty;

                    isManualOutReady = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateMessage(ex.Message);
                lblErpCheckResult.ForeColor = System.Drawing.Color.Red;
                lblErpCheckResult.Text = "ERP NG";
                lblErpReqQty.Text = "0";
            }

            #endregion
        }

        private bool ProcessManualStockOut()
        {
            return false;
        }

        private void btnManualOutSave_Click(object sender, EventArgs e)
        {
            if (isManualOutReady == false)
            {
                MessageBox.Show("Validation failed", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool isOutQtyDifferent = decimal.Compare(Convert.ToDecimal(lblStockTotalQty.Text), Convert.ToDecimal(lblErpReqQty.Text)) != 0;
            if (isOutQtyDifferent)
            {
                var msgResult = MessageBox.Show("지시수량과 출고수량이 다릅니다. \r\n출고처리 하시겠습니까?", "Ask", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (msgResult != DialogResult.Yes) return;
            }

            //TODO Save Query
            MessageBox.Show("Todo Save Query");
            ProcessManualStockOut();
        }

        private void txtTargetColumn_TextChanged(object sender, EventArgs e)
        {
            this._rmBarcodeColumnName = txtTargetColumn.Text.Trim();
        }


        private void ClearCountField()
        {
            lblExcelRowsCount.Text = "";
            lblRmStockCount.Text = "";
            lblRmStockHistCount.Text = "";
        }
        private string OpenFileDialog()
        {
            string filepath = string.Empty;
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Multiselect = false;
                ofd.Filter = "Excel Files|*.xls;*.xlsx";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    filepath = ofd.FileName;
                }
            }

            return filepath;
        }

        private void txtErpCheck_TextChanged(object sender, EventArgs e)
        {
            isManualOutReady = false;
            lblErpCheckResult.ForeColor = System.Drawing.Color.Red;
            lblErpCheckResult.Text = "ERP NG";
            lblErpReqQty.Text = "0";
        }
    }
}
