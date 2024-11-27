using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MKWiseM.Properties;
using static System.ComponentModel.Design.ObjectSelectorEditor;
using Timer = System.Windows.Forms.Timer;

#pragma warning disable IDE1006

namespace MKWiseM
{
    public partial class Form1 : Form
    {
        //TODO IP/ID/PW Combobox List

        private bool isConnected = false;
        // Re-Idx TableList
        private readonly HashSet<string> reidxList = AppConfigUtil.LoadTables();
        private CancellationTokenSource _cToken;

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
            FileUtil.FileProgressChanged += (o, val) => this.progressFile.Value = val;

            GetConnectionStrings();
            LoadSettings();
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
                lblCatalog.Text = AppConfigUtil.Catalog;

            }
            catch (ConfigurationErrorsException ignored) { }
        }

        private void LoadRecentIP()
        {
            cbRecentIP.Items.Clear();
            cbRecentIP.Items.AddRange(Settings.Default.cbRecentIP.Cast<object>().ToArray());
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

        private void UpdateRecentIP(string ip, bool isDelete = false)
        {
            var recentIps = Settings.Default.cbRecentIP;
            if (!recentIps.Contains(ip) && !isDelete)
                recentIps.Add(ip);
            else if (recentIps.Contains(ip) && isDelete)
            {
                recentIps.Remove(ip);
            }

            Settings.Default.Save();
            LoadRecentIP();
        }

        private void btnSaveConStr_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtIP.Text) || string.IsNullOrEmpty(txtID.Text) ||
                string.IsNullOrEmpty(txtPW.Text)) return;

            AppConfigUtil.SetupConnection(
                txtIP.Text.Trim(), 
                txtID.Text.Trim(), 
                txtPW.Text.Trim(),
                AppConfigUtil.Catalog
                );
        }

        private void txtPW_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) btnSaveConStr_Click(null, EventArgs.Empty);
        }

        private void btnConnectDb_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtIP.Text) || string.IsNullOrEmpty(txtID.Text) ||
                string.IsNullOrEmpty(txtPW.Text)) return;

            TryOpenConnection();

            lblConStatus.Text = AppConfigUtil.IP;
            UpdateRecentIP(txtIP.Text);
        }

        private async void TryOpenConnection()
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
            txtIP.Text = cbRecentIP.SelectedItem.ToString();
        }

        private void btnDeleteConStr_Click(object sender, EventArgs e)
        {
            UpdateRecentIP(txtIP.Text, true);
        }

        private void cbDbList_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (cbDbList.SelectedIndex == -1 || string.IsNullOrEmpty(cbDbList.SelectedValue.ToString())) return;

            AppConfigUtil.SetupConnection(
                txtIP.Text.Trim(),
                txtID.Text.Trim(),
                txtPW.Text.Trim(),
                cbDbList.SelectedValue.ToString()
            );

            lblCatalog.Text = cbDbList.SelectedValue.ToString();
            btnLoadList_Click(sender, EventArgs.Empty);
        }

        private void lblSelectedFileName_DoubleClick(object sender, EventArgs e)
        {
            Clipboard.SetText(this.lblSelectedFileName.Text);
            MessageBox.Show(this.lblSelectedFileName.Text + "\n\nCopied!");
        }


        private bool IsInitialized()
        {
            return (isConnected && !string.IsNullOrEmpty(AppConfigUtil.Catalog));
        }

        private async void lblMessage_TextChanged(object sender, EventArgs e)
        {
            _cToken?.Cancel();
            _cToken = new CancellationTokenSource();
            try
            {
                await Task.Delay(5000, _cToken.Token);
                this.lblMessage.Text = "";
            }
            catch (TaskCanceledException) { }
        }


    }
}
