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
    public partial class Form1 : Form
    {
        public int row;
        public int column;
        public Radius radius;
        public bool isIdentical;
        public List<unitRow> beamInfo;

        public Form1()
        {
            InitializeComponent();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            string text = textBox2.Text;
            if (text != "")
            {
                if (!isNumeric(text))
                {
                    textBox2.Text = "";
                    textBox2.Clear();
                    MessageBox.Show("请输入数字！");
                }
            }
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {

        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                textBox1.Enabled = true;
                textBox2.Enabled = true;
                comboBox1.Enabled = true;

                textBox3.Enabled = false; textBox4.Enabled = false; textBox5.Enabled = false;
                comboBox2.Enabled = false; comboBox3.Enabled = false; comboBox4.Enabled = false;
                
            }

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                isIdentical = true;
                if (textBox1.Text != "" && textBox2.Text != "" && comboBox1.Text!="")
                {
                    row = int.Parse(textBox1.Text);
                    column = int.Parse(textBox2.Text);
                    if (comboBox1.Text == "175mm")
                        radius = Radius.large;
                    else
                        radius = Radius.small;

                    this.Close();
                }
            }
            else if(radioButton2.Checked)
            {
                isIdentical = false;
                beamInfo = new List<unitRow>();
                if (textBox3.Text != "" && comboBox2.Text != "" && textBox4.Text != "" && comboBox3.Text != "")
                {
                    beamInfo.Add(new unitRow(int.Parse(textBox3.Text), ToEnum(comboBox2.Text)));
                    beamInfo.Add(new unitRow(int.Parse(textBox4.Text), ToEnum(comboBox3.Text)));
                    if (textBox5.Text != "" && comboBox4.Text != "")
                        beamInfo.Add(new unitRow(int.Parse(textBox5.Text), ToEnum(comboBox4.Text)));
                    this.Close();
                }
                else
                    MessageBox.Show("请至少输入两行数据！");
            }
        }

        private Radius ToEnum(string text) {
            if (text == "175mm")
                return Radius.large;
            return Radius.small;
        }
        private void splitContainer1_Panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked)
            {
                textBox1.Enabled = false;
                textBox2.Enabled = false;
                comboBox1.Enabled = false;

                textBox3.Enabled = true; textBox4.Enabled = true; textBox5.Enabled = true;
                comboBox2.Enabled = true; comboBox3.Enabled = true; comboBox4.Enabled = true;

            }
        }

        public bool isNumeric(string str)
        {
            char[] ch = new char[str.Length];
            ch = str.ToCharArray();
            for (int i = 0; i < ch.Length; i++)
            {
                if (ch[i] < 48 || ch[i] > 57)
                {
                    return false;
                }
            }
            return true;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            string text = textBox1.Text;
            if (text != "")
            {
                if (!isNumeric(text))
                {
                    textBox1.Text = "";
                    textBox1.Clear();
                    MessageBox.Show("请输入数字！");
                }
            }
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            string text = textBox3.Text;
            if (text != "")
            {
                if (!isNumeric(text))
                {
                    textBox3.Text = "";
                    textBox3.Clear();
                    MessageBox.Show("请输入数字！");
                }
            }
        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {
            string text = textBox4.Text;
            if (text != "")
            {
                if (!isNumeric(text))
                {
                    textBox4.Text = "";
                    textBox4.Clear();
                    MessageBox.Show("请输入数字！");
                }
            }
        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {
            string text = textBox5.Text;
            if (text != "")
            {
                if (!isNumeric(text))
                {
                    textBox5.Text = "";
                    textBox5.Clear();
                    MessageBox.Show("请输入数字！");
                }
            }
        }
    }
}
