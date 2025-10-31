using System;
using System.Windows.Forms;

namespace MKWiseM
{
    public partial class LoginForm : Form
    {
        public LoginForm()
        {
            InitializeComponent();
        }

        private async void LoginForm_Load(object sender, EventArgs e)
        {
            this.textBox1.Focus();
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                button1.PerformClick();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.ToLower() == "wisem")
            {
                this.Hide();
                var form1 = new Form1();
                form1.ShowDialog();
                this.Close();
            }
            else
            {
                MessageBox.Show("WRONG PASSWORD");
                textBox1.Text = "";
                textBox1.Focus();
            }
        }
    }
}
