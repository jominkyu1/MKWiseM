using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MKWiseM.DTO;
using MKWiseM.Properties;

#pragma warning disable IDE1006

namespace MKWiseM
{
    public partial class Form1 : Form
    {
        private bool isConnected = false;

        // Re-Idx TableList
        private readonly HashSet<string> reidxList = AppConfigUtil.LoadTables();
        private CancellationTokenSource _cToken;

        public event EventHandler<String> dataCatalogChanged;

        public Form1()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            AppConfigUtil.SetTables(reidxList);
            base.OnClosing(e);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            AppConfigUtil.MessageDeploy += Event_UpdateMessage;
            DBUtil.MessageDeploy += Event_UpdateMessage;
            OleDbUtil.MessageDeploy += Event_UpdateMessage;
            FileUtil.FileProgressChanged += (o, val) => this.progressFile.Value = val;

            GetConnectionStrings();
            LoadSettings();

            SetDoubleBufferDataGridView(dGridReIdx);
            SetDoubleBufferDataGridView(dGridCachedTable);
        }

        private void LoadSettings()
        {
            try
            {
                //Load
                var settings = Settings.Default;
                chkAutoConnect.Checked = settings.chkAutoConnect;
                LoadRecentIP();

                //Setup
                if (Settings.Default.chkAutoConnect) TryOpenConnection();
                
                lblConStatus.Text = AppConfigUtil.IP;
                //lblCatalog.Text = AppConfigUtil.Catalog;

            }
            catch (ConfigurationErrorsException) { }
        }

        private void LoadRecentIP()
        {
            int currIdx = 0;
            if (cbRecentIP.SelectedIndex > 0)
            {
                currIdx = cbRecentIP.SelectedIndex;
            }

            List<IpGroup> ipGroups = IpGroup.GetIpGroups(Settings.Default.cbRecentIP);
            cbRecentIP.DataSource = ipGroups;
            cbRecentIP.DisplayMember = "IP";
            if (cbRecentIP.Items.Count > currIdx)
            {
                cbRecentIP.SelectedIndex = currIdx;
            }
        }

        private void GetConnectionStrings()
        {
            var conStr = new SqlConnectionStringBuilder(AppConfigUtil.ConnectionStrings);
            txtIP.Text = conStr.DataSource;
            txtID.Text = conStr.UserID;
            txtPW.Text = conStr.Password;
        }

        private void Event_UpdateMessage(Object sender, MessageEventArgs e)
        {
            lblMessage.GetCurrentParent()
                .Invoke(new Action(() => lblMessage.Text = $"{DateTime.Now:g} {e.Message}"));
            
            if(!string.IsNullOrEmpty(e.ErrorLog))
            {
                listLog?.Invoke(new Action(() => listLog.Items.Add($"{DateTime.Now:g} {e.ErrorLog}")));
            }
        }

        private void UpdateMessage(string message)
        {
            lblMessage.Text = $"{DateTime.Now:g} {message}";
        }

        private void DeleteRecentIP(string inputIP)
        {
            var recentIps = Settings.Default.cbRecentIP;
            for (var i = 0; i < recentIps.Count; i++)
            {
                var decryptedIpGroup = EncryptUtil.Decrypt(recentIps[i]);
                if (decryptedIpGroup.Contains(inputIP))
                {
                    recentIps.Remove(recentIps[i]);
                }
            }

            Settings.Default.Save();
            LoadRecentIP();
        }

        private void UpdateRecentIP(string inputIP, string inputID, string inputPW)
        {
            var recentIps = Settings.Default.cbRecentIP;
            string encryptedIPGroup = EncryptUtil.Encrypt($"{inputIP};{inputID};{inputPW}");

            bool isUpdated = false;
            for (int i = 0; i < recentIps.Count; i++)
            {
                var decryptedIpGroup = EncryptUtil.Decrypt(recentIps[i]);
                if (decryptedIpGroup.Contains(inputIP)) //이미 IP가 존재하면 UPDATE
                {
                    recentIps[i] = encryptedIPGroup;
                    isUpdated = true;
                    break;
                }
            }

            // ELSE ADD
            if (!isUpdated)
            {
                recentIps.Add(encryptedIPGroup);
            }
            
            Settings.Default.Save();
            LoadRecentIP();
        }

        private void btnSaveConStr_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtIP.Text) || string.IsNullOrEmpty(txtID.Text) ||
                string.IsNullOrEmpty(txtPW.Text))
            {
                MessageBox.Show("모든 항목을 입력해주세요.", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AppConfigUtil.SetupConnection(
                txtIP.Text.Trim(), 
                txtID.Text.Trim(), 
                txtPW.Text.Trim(),
                AppConfigUtil.Catalog
                );

            UpdateRecentIP(txtIP.Text.Trim(), txtID.Text.Trim(), txtPW.Text.Trim());
        }

        private void txtPW_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) btnSaveConStr_Click(null, EventArgs.Empty);
        }

        private void btnConnectDb_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtIP.Text) || string.IsNullOrEmpty(txtID.Text) ||
                string.IsNullOrEmpty(txtPW.Text))
            {
                MessageBox.Show("모든 항목을 입력해주세요.", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AppConfigUtil.SetupConnection(
                txtIP.Text.Trim(),
                txtID.Text.Trim(),
                txtPW.Text.Trim()
                //, AppConfigUtil.Catalog
            );

            TryOpenConnection();

            lblConStatus.Text = AppConfigUtil.IP;
            UpdateRecentIP(txtIP.Text.Trim(), txtID.Text.Trim(), txtPW.Text.Trim());
        }

        private async void TryOpenConnection()
        {
            try
            {
                UpdateMessage("Try to connect...");
                var result = await DBUtil.IsValidConnectionAsync();
                if (result)
                {
                    lblConStatus.Image = Resources.conon;
                    this.isConnected = true;
                    btnLoadList_Click(this, EventArgs.Empty);
                }
                else
                {
                    lblConStatus.Image = Resources.conoff;
                    this.isConnected = false;
                }
            }
            catch (Exception)
            {
                lblConStatus.Image = Resources.conoff;
                this.isConnected = false;
            }
        }

        private void chkAutoConnect_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.chkAutoConnect = chkAutoConnect.Checked;
            Settings.Default.Save();
        }

        private void listLog_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl + C
            if (e.Control && e.KeyCode == Keys.C)
            {
                if (listLog.SelectedItems.Count > 0)
                {
                    var items = listLog.SelectedItems.Cast<String>();
                    var result = string.Join(Environment.NewLine, items);
                    Clipboard.SetText(result);

                    UpdateMessage("Copied to clipboard.");
                }
            }
        }

        private void SetupCatalogList()
        {
            if (!isConnected)
            {
                return;
            }
            // .NET framework 3.5 ++
            DBUtil.GetDataTableTask("SELECT name FROM sys.databases WHERE database_id > 4",
                onCompleted: dt =>
                {
                    cbDbList?.Invoke(new Action(() =>
                    {
                        cbDbList.DataSource = dt;
                        cbDbList.DisplayMember = "name";
                        cbDbList.ValueMember = "name";

                        cbDbList.SelectedIndex = -1;
                    }));
                });
        }

        private static string BytesToGB(long bytes)
        {
            var totalGb = (bytes / (1024 * 1024 * 1024.0));
            if (totalGb >= 1000)
            {
                var totalTb = totalGb / 1024.0;

                return totalTb.ToString("F") + " TB";
            }
            else
                return totalGb.ToString("N0") + " GB";
        }


        private void cbRecentIP_SelectionChangeCommitted(object sender, EventArgs e)
        {
            var selectedItem = cbRecentIP.SelectedItem as IpGroup;

            txtIP.Text = selectedItem?.IP;
            txtID.Text = selectedItem?.ID;
            txtPW.Text = selectedItem?.Password;
        }

        private void btnDeleteConStr_Click(object sender, EventArgs e)
        {
            DeleteRecentIP(txtIP.Text);
        }
        private void cbDbList_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (cbDbList.SelectedIndex == -1 || string.IsNullOrEmpty(cbDbList.SelectedValue.ToString())) return;
            string catalog = cbDbList.SelectedValue.ToString();

            AppConfigUtil.SetupConnection(
                txtIP.Text.Trim(),
                txtID.Text.Trim(),
                txtPW.Text.Trim(),
                catalog
            );

            lblCatalog.Text = catalog;
            btnLoadList_Click(sender, EventArgs.Empty);
            dataCatalogChanged?.Invoke(this, catalog);
        }

        private void lblSelectedFileName_DoubleClick(object sender, EventArgs e)
        {
            Clipboard.SetText(this.lblSelectedFileName.Text);
            MessageBox.Show(this.lblSelectedFileName.Text + "\n\nCopied!");
        }


        private bool IsDbInitialized()
        {
            return (isConnected && !string.IsNullOrEmpty(AppConfigUtil.Catalog));
        }

        private async void lblMessage_TextChanged(object sender, EventArgs e)
        {
            try
            {
                _cToken?.Cancel();
                _cToken = new CancellationTokenSource();

                await Task.Delay(5000, _cToken.Token);
                this.lblMessage.Text = "";
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void btnConfigPath_Click(object sender, EventArgs e)
        {
            string configPath = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
            MessageBox.Show($"Copied to clipboard.\n\n{configPath}");
            Clipboard.SetText(configPath);
        }

        private void btnInstallProcedure_Click(object sender, EventArgs e)
        {
            DBUtil.InstallProcdureTask(() => 
            {
                MessageBox.Show("Procedure Installed.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        private void SetDoubleBufferDataGridView(DataGridView dataGridView)
        {
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.SetProperty, null, dataGridView,
                new object[] { true });
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            tsPbar.Visible = true;
            await Task.Delay(3000);
            tsPbar.Visible = false;
        }

        
    }
}
