using Drx.Sdk.Input;
using Drx.Sdk.Json;

namespace DyingLightMacro
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private KeysConfig keysConfig = new KeysConfig();

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            comboBox1 = new ComboBox();
            label1 = new Label();
            label2 = new Label();
            comboBox2 = new ComboBox();
            comboBox3 = new ComboBox();
            label3 = new Label();
            label4 = new Label();
            comboBox4 = new ComboBox();
            label5 = new Label();
            comboBox5 = new ComboBox();
            label6 = new Label();
            button1 = new Button();
            listBox1 = new ListBox();
            label7 = new Label();
            button2 = new Button();
            button3 = new Button();
            button4 = new Button();
            button5 = new Button();
            button6 = new Button();
            textBox1 = new TextBox();
            button7 = new Button();
            listBox2 = new ListBox();
            label8 = new Label();
            button8 = new Button();
            SuspendLayout();
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Location = new Point(12, 38);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(121, 25);
            comboBox1.TabIndex = 0;
            comboBox1.SelectedIndexChanged += ComboboxChange;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 18);
            label1.Name = "label1";
            label1.Size = new Size(68, 17);
            label1.TabIndex = 1;
            label1.Text = "切弩快捷键";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(139, 18);
            label2.Name = "label2";
            label2.Size = new Size(68, 17);
            label2.TabIndex = 2;
            label2.Text = "副宏快捷键";
            // 
            // comboBox2
            // 
            comboBox2.FormattingEnabled = true;
            comboBox2.Location = new Point(139, 38);
            comboBox2.Name = "comboBox2";
            comboBox2.Size = new Size(121, 25);
            comboBox2.TabIndex = 3;
            comboBox2.SelectedIndexChanged += ComboboxChange;
            // 
            // comboBox3
            // 
            comboBox3.FormattingEnabled = true;
            comboBox3.Location = new Point(12, 86);
            comboBox3.Name = "comboBox3";
            comboBox3.Size = new Size(121, 25);
            comboBox3.TabIndex = 4;
            comboBox3.SelectedIndexChanged += ComboboxChange;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 66);
            label3.Name = "label3";
            label3.Size = new Size(80, 17);
            label3.TabIndex = 5;
            label3.Text = "刀的物品按键";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(314, 18);
            label4.Name = "label4";
            label4.Size = new Size(462, 187);
            label4.TabIndex = 6;
            label4.Text = resources.GetString("label4.Text");
            // 
            // comboBox4
            // 
            comboBox4.FormattingEnabled = true;
            comboBox4.Location = new Point(12, 134);
            comboBox4.Name = "comboBox4";
            comboBox4.Size = new Size(121, 25);
            comboBox4.TabIndex = 7;
            comboBox4.SelectedIndexChanged += ComboboxChange;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(12, 114);
            label5.Name = "label5";
            label5.Size = new Size(80, 17);
            label5.TabIndex = 8;
            label5.Text = "物品栏快捷键";
            // 
            // comboBox5
            // 
            comboBox5.FormattingEnabled = true;
            comboBox5.Location = new Point(12, 182);
            comboBox5.Name = "comboBox5";
            comboBox5.Size = new Size(121, 25);
            comboBox5.TabIndex = 9;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(12, 162);
            label6.Name = "label6";
            label6.Size = new Size(44, 17);
            label6.TabIndex = 10;
            label6.Text = "按键组";
            // 
            // button1
            // 
            button1.Location = new Point(139, 182);
            button1.Name = "button1";
            button1.Size = new Size(121, 26);
            button1.TabIndex = 11;
            button1.Text = "添加到宏";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // listBox1
            // 
            listBox1.FormattingEnabled = true;
            listBox1.Location = new Point(12, 238);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(248, 259);
            listBox1.TabIndex = 12;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(12, 218);
            label7.Name = "label7";
            label7.Size = new Size(44, 17);
            label7.TabIndex = 13;
            label7.Text = "宏脚本";
            // 
            // button2
            // 
            button2.Location = new Point(266, 471);
            button2.Name = "button2";
            button2.Size = new Size(121, 26);
            button2.TabIndex = 14;
            button2.Text = "移除选中";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // button3
            // 
            button3.Location = new Point(266, 439);
            button3.Name = "button3";
            button3.Size = new Size(121, 26);
            button3.TabIndex = 15;
            button3.Text = "复制选中";
            button3.UseVisualStyleBackColor = true;
            // 
            // button4
            // 
            button4.Location = new Point(266, 407);
            button4.Name = "button4";
            button4.Size = new Size(121, 26);
            button4.TabIndex = 16;
            button4.Text = "保存脚本";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
            // 
            // button5
            // 
            button5.Location = new Point(266, 238);
            button5.Name = "button5";
            button5.Size = new Size(121, 26);
            button5.TabIndex = 17;
            button5.Text = "上移";
            button5.UseVisualStyleBackColor = true;
            button5.Click += button5_Click;
            // 
            // button6
            // 
            button6.Location = new Point(266, 270);
            button6.Name = "button6";
            button6.Size = new Size(121, 26);
            button6.TabIndex = 18;
            button6.Text = "下降";
            button6.UseVisualStyleBackColor = true;
            button6.Click += button6_Click;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(266, 378);
            textBox1.Name = "textBox1";
            textBox1.PlaceholderText = "保存为..";
            textBox1.Size = new Size(121, 23);
            textBox1.TabIndex = 19;
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // button7
            // 
            button7.Location = new Point(667, 471);
            button7.Name = "button7";
            button7.Size = new Size(121, 26);
            button7.TabIndex = 20;
            button7.Text = "从保存的配置中加载";
            button7.UseVisualStyleBackColor = true;
            button7.Click += button7_Click;
            // 
            // listBox2
            // 
            listBox2.FormattingEnabled = true;
            listBox2.Location = new Point(393, 238);
            listBox2.Name = "listBox2";
            listBox2.Size = new Size(268, 259);
            listBox2.TabIndex = 21;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(393, 218);
            label8.Name = "label8";
            label8.Size = new Size(92, 17);
            label8.TabIndex = 22;
            label8.Text = "已保存的配置项";
            label8.Click += label8_Click;
            // 
            // button8
            // 
            button8.Location = new Point(667, 439);
            button8.Name = "button8";
            button8.Size = new Size(121, 26);
            button8.TabIndex = 23;
            button8.Text = "移除保存的配置项";
            button8.UseVisualStyleBackColor = true;
            button8.Click += button8_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 509);
            Controls.Add(button8);
            Controls.Add(label8);
            Controls.Add(listBox2);
            Controls.Add(button7);
            Controls.Add(textBox1);
            Controls.Add(button6);
            Controls.Add(button5);
            Controls.Add(button4);
            Controls.Add(button3);
            Controls.Add(button2);
            Controls.Add(label7);
            Controls.Add(listBox1);
            Controls.Add(button1);
            Controls.Add(label6);
            Controls.Add(comboBox5);
            Controls.Add(label5);
            Controls.Add(comboBox4);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(comboBox3);
            Controls.Add(comboBox2);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(comboBox1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Form1";
            Text = "Form1";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        private void ComboboxChange(object sender, EventArgs e)
        {
            try
            {
                if ((sender as ComboBox).SelectedItem != null)
                {
                    // 获取触发事件的 ComboBox 的名字
                    var comboBoxName = (sender as ComboBox).Name;
                    // 如果名字为：comboBox1, 则更新切弩按键如果名字为：comboBox2, 则更新侧键2按键
                    switch (comboBoxName)
                    {
                        case "comboBox1":
                            keysConfig.Crossbow = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), (sender as ComboBox).SelectedItem.ToString());
                            break;
                        case "comboBox2":
                            keysConfig.DoubleCrossbow = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), (sender as ComboBox).SelectedItem.ToString());
                            break;
                        case "comboBox3":
                            keysConfig.Item1 = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), (sender as ComboBox).SelectedItem.ToString());
                            break;
                        case "comboBox4":
                            keysConfig.Inventory = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), (sender as ComboBox).SelectedItem.ToString());
                            break;
                        default:
                            break;
                    }

                    // 重新注册热键
                    if (_keyboardMacro != null)
                    {
                        RegisterGlobalHotkey();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新按键设置时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                // 获取应用程序目录下的配置文件路径
                string configPath = Path.Combine(Application.StartupPath, "last.cfg");

                // 序列化并保存到文件
                JsonFile.WriteToFile(keysConfig, configPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            Loading();
        }

        public void Loading()
        {
            try
            {
                comboBox1.Items.Clear();
                comboBox2.Items.Clear();
                comboBox3.Items.Clear();
                comboBox4.Items.Clear();
                comboBox5.Items.Clear();

                comboBox1.Items.AddRange(Enum.GetNames(typeof(VirtualKeyCode)));
                comboBox2.Items.AddRange(Enum.GetNames(typeof(VirtualKeyCode)));
                comboBox3.Items.AddRange(Enum.GetNames(typeof(VirtualKeyCode)));
                comboBox4.Items.AddRange(Enum.GetNames(typeof(VirtualKeyCode)));
                comboBox5.Items.AddRange(Enum.GetNames(typeof(VirtualKeyCode)));

                // 获取配置文件路径
                string configPath = Path.Combine(Application.StartupPath, "last.cfg");

                // 检查配置文件是否存在
                if (File.Exists(configPath))
                {
                    // 读取配置文件并反序列化
                    var loadedConfig = JsonFile.ReadFromFile<KeysConfig>(configPath);
                    if (loadedConfig != null)
                    {
                        // 更新 keysConfig
                        keysConfig = loadedConfig;

                        // 设置 comboBox1 的选中项
                        UpdateComboBox();
                        UpdateConfigList();
                    }
                }
                else
                {
                    // 如果配置文件不存在，使用默认值
                    keysConfig = new KeysConfig
                    {
                        Crossbow = VirtualKeyCode.F2,
                        Inventory = VirtualKeyCode.F1,
                        Item1 = VirtualKeyCode.KEY_1,
                        Item2 = VirtualKeyCode.KEY_2,
                        Item3 = VirtualKeyCode.KEY_3,
                        Item4 = VirtualKeyCode.KEY_4,
                        DoubleCrossbow = VirtualKeyCode.F3
                    };

                    // 设置默认选中项
                    UpdateComboBox();
                    UpdateConfigList();
                }

                _keyboardMacro = new KeyboardMacro();
                RegisterGlobalHotkey();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void button1_Click(object sender, EventArgs e)
        {
            // 将 comboBox5 的值转换为 VirtualKeyCode 枚举，然后添加到 keysConfig.script 中
            keysConfig.script.Add((VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), comboBox5.SelectedItem.ToString()));

            // 将 KeysConfig 的 script 字段的值遍历，并根据顺序添加到 listBox1 中
            // 清除 listBox1 的所有项
            listBox1.Items.Clear();
            foreach (var key in keysConfig.script)
            {
                listBox1.Items.Add(key);
            }
        }

        private void UpdateComboBox()
        {
            comboBox1.SelectedItem = keysConfig.Crossbow.ToString();
            comboBox2.SelectedItem = keysConfig.DoubleCrossbow.ToString();
            comboBox3.SelectedItem = keysConfig.Item1.ToString();
            comboBox4.SelectedItem = keysConfig.Inventory.ToString();

            listBox1.Items.Clear();
            foreach (var key in keysConfig.script)
            {
                listBox1.Items.Add(key);
            }
        }

        private void UpdateConfigList()
        {
            try
            {
                // 清空 listBox2 的所有项
                listBox2.Items.Clear();

                // 获取应用程序运行目录下的所有 .json 文件
                string[] configFiles = Directory.GetFiles(Application.StartupPath, "*.cfg");

                // 将文件名添加到 listBox2 中
                foreach (string filePath in configFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    listBox2.Items.Add(fileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新配置列表时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        private ComboBox comboBox1;
        private Label label1;
        private Label label2;
        private ComboBox comboBox2;
        private ComboBox comboBox3;
        private Label label3;
        private Label label4;
        private ComboBox comboBox4;
        private Label label5;
        private ComboBox comboBox5;
        private Label label6;
        private Button button1;
        private ListBox listBox1;
        private Label label7;
        private Button button2;
        private Button button3;
        private Button button4;
        private Button button5;
        private Button button6;
        private TextBox textBox1;
        private Button button7;
        private ListBox listBox2;
        private Label label8;
        private Button button8;
    }

    // 用于序列化的配置模型类
    public class KeysConfig
    {
        public VirtualKeyCode Crossbow { get; set; }
        public VirtualKeyCode Inventory { get; set; }
        public VirtualKeyCode Item1 { get; set; }
        public VirtualKeyCode Item2 { get; set; }
        public VirtualKeyCode Item3 { get; set; }
        public VirtualKeyCode Item4 { get; set; }
        public VirtualKeyCode DoubleCrossbow { get; set; }
        public List<VirtualKeyCode> script { get; set; }

        public KeysConfig()
        {
            script = new List<VirtualKeyCode>();
        }
    }
}
