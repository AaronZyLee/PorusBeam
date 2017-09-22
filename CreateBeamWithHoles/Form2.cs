using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CreateBeamWithHoles
{
    public partial class Form2 : Form
    {
        public String fileName1;
        public String fileName2;
        public String fileName3;

        public Form2()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = @"C:\Users\DELL\Desktop";
            openFileDialog1.Filter = "文本文档(*.txt)|*.txt";
            if (this.openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = System.IO.Path.GetFileName(openFileDialog1.FileName);
                button3.Enabled = true;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.fileName1 = openFileDialog1.FileName;
            this.fileName2 = openFileDialog2.FileName;
            this.fileName3 = openFileDialog3.FileName;
            this.Close();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void Form2_Load(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            openFileDialog2.InitialDirectory = @"C:\Users\DELL\Desktop";
            openFileDialog2.Filter = "文本文档(*.txt)|*.txt";
            if (this.openFileDialog2.ShowDialog() == DialogResult.OK)
            {
                textBox2.Text = System.IO.Path.GetFileName(openFileDialog2.FileName);
                button4.Enabled = true;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            openFileDialog3.InitialDirectory = @"C:\Users\DELL\Desktop";
            openFileDialog3.Filter = "文本文档(*.txt)|*.txt";
            if (this.openFileDialog3.ShowDialog() == DialogResult.OK)
            {
                textBox3.Text = System.IO.Path.GetFileName(openFileDialog3.FileName);
                button2.Enabled = true;
            }
        }
    }
}
