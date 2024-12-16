using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;


namespace MKWiseM
{
    public partial class Form1
    {
        private byte[] rawData = null;

        private void tabUpload_Enter(object sender, EventArgs e)
        {
            if (IsDbInitialized())
            {
                LoadProgramTables();
                EnableComps();
            }
            else
            {
                DisableComps();
            }
        }

        private void EnableComps()
        {
            this.panelProgramInsert.Enabled = true;
            this.btnProgramLoad.Enabled = true;
        }

        private void DisableComps()
        {
            this.cbBunch.DataSource = null;
            this.cbUploadTable.DataSource = null;
            this.dGridCurrentProgram.DataSource = null;

            this.btnProgramLoad.Enabled = false;
            this.panelProgramInsert.Visible = false;
        }
        private void cbUploadTable_SelectionChangeCommitted(object sender, EventArgs e)
        {
        }
        private void cbUploadTable_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadProgramBunch();
        }

        private async void btnProgramLoad_Click(object sender, EventArgs e)
        {
            var sTable = cbUploadTable.SelectedValue?.ToString() ?? string.Empty;
            var sBunch = cbBunch.SelectedValue?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(sTable) || string.IsNullOrEmpty(sBunch))
            {
                MessageBox.Show("NOT SELECTED");
            } 
            else
            {
                string loadQuery = $@"
                SELECT
                       concat(datalength(data) / ( 1024 * 1024 ), ' MB') as FileSize
                     , filename, bunch, updated, version
                FROM {sTable}
                WHERE bunch = '{sBunch}'
                ORDER BY updated desc, version DESC
                ";

                var dt = await DBUtil.GetDataTableAsync(loadQuery);
                dGridCurrentProgram.DataSource = dt;
                var latestRow = GetLatestView(dt);

                SetupLatestLabel(latestRow);
                SetupSelectedLabel(latestRow);

                this.panelProgramInsert.Visible = true;
            }

        }
        private async void btnSelectFile_Click(object sender, EventArgs e)
        {
            string fileName = GetFileNameWithDialog(true);
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }
            string safeFileName = fileName.Split('\\').Last().Trim();

            lblSelectedFileName.Text = safeFileName;
            this.rawData = await FileUtil.ToRawdataAsync(fileName);
            //this.rawData = FileUtil.ToRawData(fileName);
            UpdateMessage("File Loaded");
            MessageBox.Show("File loaded");

            lblSelectedFileSize.Text = $"{FileUtil.ByteToMB(rawData)} MB";
        }

        private async void btnFileUpload_Click(object sender, EventArgs e)
        {

            #region Ask
            if (this.rawData == null)
            {
                MessageBox.Show("File Not Selected");
                return;
            }

            string infoMessage = $@"
            \t#####INSERT#####
            FileName\t\t{lblSelectedFileName.Text.Trim()}
            Bunch\t\t{lblSelectedBunch.Text.Trim()}
            Version\t\t{txtSelectedVersion.Text.Trim()}

            \t#####UPDATE#####
            FileName\t\t{lblFileName.Text.Trim()}
            To \t\t {txtTransFilename.Text.Trim()}
            Bunch\t\t{lblBunch.Text.Trim()}
            Version\t\t{lblFileVersion.Text.Trim()}
             ".Replace(" ", "").Replace("\\t", "\t");

            var result = 
                MessageBox.Show(infoMessage, "File Upload", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.No) return;
            #endregion

            string targetTable = cbUploadTable.SelectedValue.ToString();

            #region TranQuery
            string tranQuery = $@"
            BEGIN TRY
                BEGIN TRAN
                INSERT INTO {targetTable} (data, filename, Bunch, Updated, Version)
                VALUES (@rawData, 
                        '{lblSelectedFileName.Text.Trim()}', 
                        '{lblSelectedBunch.Text.Trim()}', 
                        GETDATE(), 
                        '{txtSelectedVersion.Text.Trim()}' 
                       )

                UPDATE {targetTable} SET filename = '{txtTransFilename.Text.Trim()}' 
                WHERE filename = '{lblFileName.Text.Trim()}' 
                    AND version = '{lblFileVersion.Text.Trim()}' 
                PRINT '파일 업로드 완료'

              COMMIT TRAN
            END TRY
            BEGIN CATCH
                ROLLBACK TRAN
                PRINT ERROR_MESSAGE()
            END CATCH
            ";
            #endregion

            try
            {
                this.progressFile.Value = 50;
                await DBUtil.InsertBinaryFile(tranQuery, this.rawData);
                MessageBox.Show("File Uploaded");
                this.progressFile.Value = 100;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetFileNameWithDialog(bool containPath = false)
        {
            var fileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "모든 파일(*.*)|*.*"
            };

            string str = "";
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                str = containPath ? 
                      fileDialog.FileName 
                    : fileDialog.SafeFileName;
            }

            return str;
        }

        private void LoadProgramBunch()
        {
            if (!string.IsNullOrEmpty(cbUploadTable?.SelectedValue.ToString()))
            {
                string bunchQuery = $"SELECT bunch FROM {cbUploadTable.SelectedValue} Group by bunch";

                DBUtil.GetDataTableTask(bunchQuery,
                    onCompleted: dtBunch =>
                    {
                        cbBunch?.Invoke(new Action(() =>
                        {
                            cbBunch.DataSource = DBUtil.TableToList(dtBunch);
                        }));
                    });
            }
            else
            {
                MessageBox.Show("TABLE NOT SELECTED");
            }
        }

        private void LoadProgramTables()
        {

            const string tableQuery = 
                "SELECT DISTINCT TABLE_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME like '%Upload%'";

            DBUtil.GetDataTableTask(tableQuery, 
                onCompleted: dt =>
                {
                    cbUploadTable?.Invoke(new Action(() =>
                    {
                        cbUploadTable.DataSource = DBUtil.TableToList(dt);
                        if (cbUploadTable.Items.Count == 0) DisableComps();
                    }));
                });
        }

        private DataRowView GetLatestView(DataTable dt)
        {
            var dView = new DataView(dt);
            dView.Sort = "version desc";

            return dView[0];
        }

        private void SetupSelectedLabel(DataRowView selected)
        {
            lblSelectedBunch.Text = selected["Bunch"].ToString();
            txtSelectedVersion.Text = FileUtil.CreateVersion(selected);
        }

        private void SetupLatestLabel(DataRowView selected)
        {

            lblFileName.Text = selected["Filename"].ToString();
            var currentVer = selected["Version"].ToString();
            lblFileVersion.Text = currentVer;
            lblFileSize.Text = selected["Filesize"].ToString();
            lblBunch.Text = selected["Bunch"].ToString();

            //FileNameLike -> Filename_20241029_2024-10-23-1.apk ...
            var split = lblFileName.Text.Split('.');
            txtTransFilename.Text = 
                split.First() + "_" + DateTime.Now.ToString("yyyyMMdd") + "_" + currentVer + "." + split.Last();
        }

        private async void btnFileDownload_Click(object sender, EventArgs e)
        {
            try
            {
                //ex ) app-debug.apk, program.exe ...
                var serverFilename = lblFileName.Text;
                //ex ) exe, apk ...
                var fileExt = serverFilename.Substring(serverFilename.LastIndexOf('.') + 1);
                var askResult = MessageBox.Show($"Download?\nFilename: {serverFilename}",
                    "Information", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (askResult != DialogResult.Yes) return;

                var sDialog = new SaveFileDialog()
                {
                    FileName = serverFilename,
                    DefaultExt = fileExt,
                    Filter = $"{fileExt.ToUpper()} Files (*.{fileExt})| *.{fileExt}"
                };

                if (sDialog.ShowDialog() == DialogResult.OK)
                {
                    var sTable = cbUploadTable.SelectedValue?.ToString() ?? string.Empty;
                    var sBunch = cbBunch.SelectedValue?.ToString() ?? string.Empty;

                    var fullPath = sDialog.FileName;
                    var query =
                        $@"SELECT TOP 1 data FROM {sTable} WHERE bunch = '{sBunch}' and filename = '{serverFilename}'";

                    var dResult = await FileUtil.DownloadRawData(fullPath, query);
                    MessageBox.Show(dResult ? "다운로드 완료" : "다운로드 실패");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dGridCurrentProgram_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var clickedRow = dGridCurrentProgram.Rows[e.RowIndex].DataBoundItem as DataRowView;

            SetupLatestLabel(clickedRow);
        }
    }
}
