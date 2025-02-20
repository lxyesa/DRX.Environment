using Drx.Sdk.Input;
using Drx.Sdk.Json;
using System.Windows.Interop;

namespace DyingLightMacro
{
    public partial class Form1 : Form
    {
        KeyboardListener keyboardListener;
        private KeyboardMacro _keyboardMacro;
        public Form1()
        {
            InitializeComponent();
        }

        private void RegisterGlobalHotkey()
        {
            var handle = this.Handle;
            if (keyboardListener == null)
            {
                keyboardListener = new KeyboardListener(handle);
            }

            try
            {
                // 注销所有现有的热键绑定
                keyboardListener.Reset();

                // 注册切弩宏的热键
                keyboardListener.RegisterCustomHotkey(
                    async () =>
                    {
                        await _keyboardMacro.SendKeySequenceAsync(new[]
                        {
                            VirtualKeyCode.KEY_E,
                            VirtualKeyCode.DELAY_100,
                            VirtualKeyCode.DELAY_100,
                            keysConfig.Inventory,
                            VirtualKeyCode.DELAY_100,
                            VirtualKeyCode.SPACE,
                            VirtualKeyCode.KEY_D,
                            VirtualKeyCode.KEY_S,
                            VirtualKeyCode.SPACE,
                            keysConfig.Inventory,
                        }, 30);
                    },
                    (uint)keysConfig.Crossbow
                );

                keyboardListener.RegisterCustomHotkey(
                    async () =>
                    {
                        await _keyboardMacro.SendKeySequenceAsync(keysConfig.script.ToArray(), 30);
                    },
                    (uint)keysConfig.DoubleCrossbow
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"注册热键时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                // 获取选中项的索引
                int selectedIndex = listBox1.SelectedIndex;

                // 检查是否有选中项
                if (selectedIndex != -1)
                {
                    // 从 keysConfig.script 中移除对应索引的项
                    keysConfig.script.RemoveAt(selectedIndex);

                    // 从 listBox1 中移除选中项
                    listBox1.Items.RemoveAt(selectedIndex);

                    // 注册热键（因为脚本已更新）
                    RegisterGlobalHotkey();
                }
                else
                {
                    MessageBox.Show("请先选择要移除的项", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"移除选中项时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                // 获取文件名
                string fileName = textBox1.Text.Trim();

                // 检查文件名是否为空
                if (string.IsNullOrEmpty(fileName))
                {
                    MessageBox.Show("请输入文件名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 获取应用程序目录下的配置文件路径
                string configPath = Path.Combine(Application.StartupPath, $"{fileName}.cfg");

                // 检查是否存在同名文件
                if (File.Exists(configPath))
                {
                    // 提示用户是否覆盖
                    DialogResult result = MessageBox.Show("文件已存在，是否覆盖？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.No)
                    {
                        return;
                    }
                }

                // 序列化并保存到文件
                JsonFile.WriteToFile(keysConfig, configPath);
                MessageBox.Show("配置已保存", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateConfigList();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                // 获取选中项的索引
                int selectedIndex = listBox1.SelectedIndex;

                // 检查是否有选中项且不是第一个项
                if (selectedIndex > 0)
                {
                    // 交换 keysConfig.script 中相邻项的位置
                    var temp = keysConfig.script[selectedIndex];
                    keysConfig.script[selectedIndex] = keysConfig.script[selectedIndex - 1];
                    keysConfig.script[selectedIndex - 1] = temp;

                    // 交换 listBox1 中相邻项的位置
                    var item = listBox1.SelectedItem;
                    listBox1.Items[selectedIndex] = listBox1.Items[selectedIndex - 1];
                    listBox1.Items[selectedIndex - 1] = item;

                    // 更新选中项的索引
                    listBox1.SelectedIndex = selectedIndex - 1;

                    // 注册热键（因为脚本已更新）
                    RegisterGlobalHotkey();
                }
                else
                {
                    MessageBox.Show("无法上移选中的项", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"上移选中项时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            try
            {
                // 获取选中项的索引
                int selectedIndex = listBox1.SelectedIndex;

                // 检查是否有选中项且不是最后一个项
                if (selectedIndex < listBox1.Items.Count - 1 && selectedIndex != -1)
                {
                    // 交换 keysConfig.script 中相邻项的位置
                    var temp = keysConfig.script[selectedIndex];
                    keysConfig.script[selectedIndex] = keysConfig.script[selectedIndex + 1];
                    keysConfig.script[selectedIndex + 1] = temp;

                    // 交换 listBox1 中相邻项的位置
                    var item = listBox1.SelectedItem;
                    listBox1.Items[selectedIndex] = listBox1.Items[selectedIndex + 1];
                    listBox1.Items[selectedIndex + 1] = item;

                    // 更新选中项的索引
                    listBox1.SelectedIndex = selectedIndex + 1;

                    // 注册热键（因为脚本已更新）
                    RegisterGlobalHotkey();
                }
                else
                {
                    MessageBox.Show("无法下移选中的项", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下移选中项时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            try
            {
                // 获取选中项的名字
                string selectedItem = listBox2.SelectedItem?.ToString();

                // 检查是否有选中项
                if (string.IsNullOrEmpty(selectedItem))
                {
                    MessageBox.Show("请先选择要加载的配置项", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 获取应用程序目录下的配置文件路径
                string configPath = Path.Combine(Application.StartupPath, $"{selectedItem}.cfg");

                // 检查文件是否存在
                if (!File.Exists(configPath))
                {
                    MessageBox.Show("配置文件不存在", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 反序列化并加载到 keysConfig 中
                keysConfig = JsonFile.ReadFromFile<KeysConfig>(configPath);

                // 执行 UpdateComboBox() 方法
                UpdateComboBox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void label8_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button8_Click(object sender, EventArgs e)
        {
            try
            {
                // 获取选中项的名字
                string selectedItem = listBox2.SelectedItem?.ToString();

                // 检查是否有选中项
                if (string.IsNullOrEmpty(selectedItem))
                {
                    MessageBox.Show("请先选择要移除的配置项", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 获取应用程序目录下的配置文件路径
                string configPath = Path.Combine(Application.StartupPath, $"{selectedItem}.cfg");

                // 检查文件是否存在
                if (!File.Exists(configPath))
                {
                    MessageBox.Show("配置文件不存在", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 删除配置文件
                File.Delete(configPath);

                // 从 listBox2 中移除选中项
                listBox2.Items.Remove(selectedItem);

                MessageBox.Show("配置文件已移除", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"移除配置文件时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
