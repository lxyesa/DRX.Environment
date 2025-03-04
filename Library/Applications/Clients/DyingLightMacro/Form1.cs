using Drx.Sdk.Input;
using Drx.Sdk.Json;
using Drx.Sdk.Script;

namespace DyingLightMacro
{
    public partial class Form1 : Form
    {
        KeyboardListener? keyboardListener;
        private KeyboardMacro? _keyboardMacro;
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
                // ע���������е��ȼ���
                keyboardListener.Reset();

                // ע���������ȼ�
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
                MessageBox.Show($"ע���ȼ�ʱ������{ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                // ��ȡѡ���������
                int selectedIndex = listBox1.SelectedIndex;

                // ����Ƿ���ѡ����
                if (selectedIndex != -1)
                {
                    // �� keysConfig.script ���Ƴ���Ӧ��������
                    keysConfig.script.RemoveAt(selectedIndex);

                    // �� listBox1 ���Ƴ�ѡ����
                    listBox1.Items.RemoveAt(selectedIndex);

                    // ע���ȼ�����Ϊ�ű��Ѹ��£�
                    RegisterGlobalHotkey();
                }
                else
                {
                    MessageBox.Show("����ѡ��Ҫ�Ƴ�����", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"�Ƴ�ѡ����ʱ������{ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                // ��ȡ�ļ���
                string fileName = textBox1.Text.Trim();

                // ����ļ����Ƿ�Ϊ��
                if (string.IsNullOrEmpty(fileName))
                {
                    MessageBox.Show("�������ļ���", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // ��ȡӦ�ó���Ŀ¼�µ������ļ�·��
                string configPath = Path.Combine(Application.StartupPath, $"{fileName}.cfg");

                // ����Ƿ����ͬ���ļ�
                if (File.Exists(configPath))
                {
                    // ��ʾ�û��Ƿ񸲸�
                    DialogResult result = MessageBox.Show("�ļ��Ѵ��ڣ��Ƿ񸲸ǣ�", "��ʾ", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.No)
                    {
                        return;
                    }
                }

                // ���л������浽�ļ�
                JsonFile.WriteToFile(keysConfig, configPath);
                MessageBox.Show("�����ѱ���", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"��������ʱ������{ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateConfigList();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                // ��ȡѡ���������
                int selectedIndex = listBox1.SelectedIndex;

                // ����Ƿ���ѡ�����Ҳ��ǵ�һ����
                if (selectedIndex > 0)
                {
                    // ���� keysConfig.script ���������λ��
                    var temp = keysConfig.script[selectedIndex];
                    keysConfig.script[selectedIndex] = keysConfig.script[selectedIndex - 1];
                    keysConfig.script[selectedIndex - 1] = temp;

                    // ���� listBox1 ���������λ��
                    var item = listBox1.SelectedItem;
                    listBox1.Items[selectedIndex] = listBox1.Items[selectedIndex - 1];
                    listBox1.Items[selectedIndex - 1] = item;

                    // ����ѡ���������
                    listBox1.SelectedIndex = selectedIndex - 1;

                    // ע���ȼ�����Ϊ�ű��Ѹ��£�
                    RegisterGlobalHotkey();
                }
                else
                {
                    MessageBox.Show("�޷�����ѡ�е���", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"����ѡ����ʱ������{ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            try
            {
                // ��ȡѡ���������
                int selectedIndex = listBox1.SelectedIndex;

                // ����Ƿ���ѡ�����Ҳ������һ����
                if (selectedIndex < listBox1.Items.Count - 1 && selectedIndex != -1)
                {
                    // ���� keysConfig.script ���������λ��
                    var temp = keysConfig.script[selectedIndex];
                    keysConfig.script[selectedIndex] = keysConfig.script[selectedIndex + 1];
                    keysConfig.script[selectedIndex + 1] = temp;

                    // ���� listBox1 ���������λ��
                    var item = listBox1.SelectedItem;
                    listBox1.Items[selectedIndex] = listBox1.Items[selectedIndex + 1];
                    listBox1.Items[selectedIndex + 1] = item;

                    // ����ѡ���������
                    listBox1.SelectedIndex = selectedIndex + 1;

                    // ע���ȼ�����Ϊ�ű��Ѹ��£�
                    RegisterGlobalHotkey();
                }
                else
                {
                    MessageBox.Show("�޷�����ѡ�е���", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"����ѡ����ʱ������{ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            try
            {
                // ��ȡѡ���������
                string selectedItem = listBox2.SelectedItem?.ToString();

                // ����Ƿ���ѡ����
                if (string.IsNullOrEmpty(selectedItem))
                {
                    MessageBox.Show("����ѡ��Ҫ���ص�������", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // ��ȡӦ�ó���Ŀ¼�µ������ļ�·��
                string configPath = Path.Combine(Application.StartupPath, $"{selectedItem}.cfg");

                // ����ļ��Ƿ����
                if (!File.Exists(configPath))
                {
                    MessageBox.Show("�����ļ�������", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // �����л������ص� keysConfig ��
                keysConfig = JsonFile.ReadFromFile<KeysConfig>(configPath);

                // ִ�� UpdateComboBox() ����
                UpdateComboBox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"��������ʱ������{ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                // ��ȡѡ���������
                string selectedItem = listBox2.SelectedItem?.ToString();

                // ����Ƿ���ѡ����
                if (string.IsNullOrEmpty(selectedItem))
                {
                    MessageBox.Show("����ѡ��Ҫ�Ƴ���������", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // ��ȡӦ�ó���Ŀ¼�µ������ļ�·��
                string configPath = Path.Combine(Application.StartupPath, $"{selectedItem}.cfg");

                // ����ļ��Ƿ����
                if (!File.Exists(configPath))
                {
                    MessageBox.Show("�����ļ�������", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // ɾ�������ļ�
                File.Delete(configPath);

                // �� listBox2 ���Ƴ�ѡ����
                listBox2.Items.Remove(selectedItem);

                MessageBox.Show("�����ļ����Ƴ�", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"�Ƴ������ļ�ʱ������{ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
