using System;
using System.Windows.Forms;

namespace ProductionModel
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var form1 = new ForwardChainingForm();

            form1.Show();
        }
    }
}
