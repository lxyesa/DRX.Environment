// 饥荒动画反编译工具 - C#实现版本
// 从Python脚本转换而来
// 支持双向转换: bin <-> xml

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace DSTools
{
    class Program
    {
        // 常量定义
        private const int BUILDVERSION = 6;
        private const int ANIMVERSION = 4;
        private const int EXPORT_DEPTH = 10;

        // 方向常量
        private const byte FACING_RIGHT = 1 << 0;
        private const byte FACING_UP = 1 << 1;
        private const byte FACING_LEFT = 1 << 2;
        private const byte FACING_DOWN = 1 << 3;
        private const byte FACING_UPRIGHT = 1 << 4;
        private const byte FACING_UPLEFT = 1 << 5;
        private const byte FACING_DOWNRIGHT = 1 << 6;
        private const byte FACING_DOWNLEFT = 1 << 7;

        /// <summary>
        /// 计算字符串哈希值
        /// </summary>
        private static uint StrHash(string str, Dictionary<uint, string> hashCollection)
        {
            uint hash = 0;
            foreach (char c in str)
            {
                byte v = (byte)char.ToLower(c);
                hash = (uint)(v + (hash << 6) + (hash << 16) - hash) & 0xFFFFFFFF;
            }
            hashCollection[hash] = str;
            return hash;
        }

        /// <summary>
        /// 写入带长度前缀的字符串
        /// </summary>
        private static void WritePrefixedString(BinaryWriter writer, string value, bool isLittleEndian)
        {
            byte[] stringBytes = Encoding.ASCII.GetBytes(value);
            WriteInt32(writer, stringBytes.Length, isLittleEndian);
            writer.Write(stringBytes);
        }

        /// <summary>
        /// 写入32位无符号整数
        /// </summary>
        private static void WriteUInt32(BinaryWriter writer, uint value, bool isLittleEndian)
        {
            if (isLittleEndian)
                writer.Write(value);
            else
                writer.Write(BitConverter.GetBytes(value).Reverse().ToArray());
        }

        /// <summary>
        /// 写入32位浮点数
        /// </summary>
        private static void WriteFloat(BinaryWriter writer, float value, bool isLittleEndian)
        {
            if (isLittleEndian)
                writer.Write(value);
            else
                writer.Write(BitConverter.GetBytes(value).Reverse().ToArray());
        }

        /// <summary>
        /// 写入32位有符号整数
        /// </summary>
        private static void WriteInt32(BinaryWriter writer, int value, bool isLittleEndian)
        {
            if (isLittleEndian)
                writer.Write(value);
            else
                writer.Write(BitConverter.GetBytes(value).Reverse().ToArray());
        }

        /// <summary>
        /// 读取带长度前缀的字符串
        /// </summary>
        private static string ReadPrefixedString(BinaryReader reader, bool isLittleEndian)
        {
            int length = isLittleEndian ? reader.ReadInt32() : BitConverter.ToInt32(BitConverter.GetBytes(reader.ReadInt32()).Reverse().ToArray());
            byte[] stringBytes = reader.ReadBytes(length);
            return Encoding.ASCII.GetString(stringBytes);
        }

        /// <summary>
        /// 读取32位整数
        /// </summary>
        private static uint ReadUInt32(BinaryReader reader, bool isLittleEndian)
        {
            if (isLittleEndian)
                return reader.ReadUInt32();

            return BitConverter.ToUInt32(BitConverter.GetBytes(reader.ReadUInt32()).Reverse().ToArray());
        }

        /// <summary>
        /// 读取32位浮点数
        /// </summary>
        private static float ReadFloat(BinaryReader reader, bool isLittleEndian)
        {
            if (isLittleEndian)
                return reader.ReadSingle();

            return BitConverter.ToSingle(BitConverter.GetBytes(reader.ReadSingle()).Reverse().ToArray());
        }

        /// <summary>
        /// 读取32位有符号整数
        /// </summary>
        private static int ReadInt32(BinaryReader reader, bool isLittleEndian)
        {
            if (isLittleEndian)
                return reader.ReadInt32();

            return BitConverter.ToInt32(BitConverter.GetBytes(reader.ReadInt32()).Reverse().ToArray());
        }        /// <summary>
                 /// 将XML动画数据导入为二进制格式（.bin文件）
                 /// </summary>
        private static void ImportAnim(bool isLittleEndian, string xmlData, Stream outStream)
        {
            Dictionary<uint, string> hashCollection = new Dictionary<uint, string>();

            // 解析XML
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlData);

            using (BinaryWriter writer = new BinaryWriter(outStream))
            {
                // 写入文件头
                writer.Write(Encoding.ASCII.GetBytes("ANIM"));
                WriteInt32(writer, ANIMVERSION, isLittleEndian);

                // 计算和写入元素、帧、事件和动画的数量
                int elementCount = doc.GetElementsByTagName("element").Count;
                int frameCount = doc.GetElementsByTagName("frame").Count;
                int eventCount = doc.GetElementsByTagName("event").Count;
                int animCount = doc.GetElementsByTagName("anim").Count;

                WriteUInt32(writer, (uint)elementCount, isLittleEndian);
                WriteUInt32(writer, (uint)frameCount, isLittleEndian);
                WriteUInt32(writer, (uint)eventCount, isLittleEndian);
                WriteUInt32(writer, (uint)animCount, isLittleEndian);

                // 处理每个动画节点
                XmlNodeList animNodes = doc.GetElementsByTagName("anim");
                foreach (XmlNode animNode in animNodes)
                {
                    ProcessAnimNode(writer, (XmlElement)animNode, hashCollection, isLittleEndian);
                }

                // 写入哈希表
                WriteUInt32(writer, (uint)hashCollection.Count, isLittleEndian);
                foreach (var pair in hashCollection)
                {
                    WriteUInt32(writer, pair.Key, isLittleEndian);
                    WritePrefixedString(writer, pair.Value, isLittleEndian);
                }
            }
        }

        /// <summary>
        /// 处理动画节点
        /// </summary>
        private static void ProcessAnimNode(BinaryWriter writer, XmlElement animNode, Dictionary<uint, string> hashCollection, bool isLittleEndian)
        {
            string name = animNode.GetAttribute("name");

            // 处理方向类型
            byte facingByte = FACING_RIGHT | FACING_LEFT | FACING_UP | FACING_DOWN | FACING_UPLEFT | FACING_UPRIGHT | FACING_DOWNLEFT | FACING_DOWNRIGHT;

            // 检测方向后缀
            if (name.EndsWith("_up"))
            {
                name = name.Substring(0, name.Length - 3);
                facingByte = FACING_UP;
            }
            else if (name.EndsWith("_down"))
            {
                name = name.Substring(0, name.Length - 5);
                facingByte = FACING_DOWN;
            }
            else if (name.EndsWith("_side"))
            {
                name = name.Substring(0, name.Length - 5);
                facingByte = FACING_LEFT | FACING_RIGHT;
            }
            else if (name.EndsWith("_left"))
            {
                name = name.Substring(0, name.Length - 5);
                facingByte = FACING_LEFT;
            }
            else if (name.EndsWith("_right"))
            {
                name = name.Substring(0, name.Length - 6);
                facingByte = FACING_RIGHT;
            }
            else if (name.EndsWith("_upside"))
            {
                name = name.Substring(0, name.Length - 7);
                facingByte = FACING_UPLEFT | FACING_UPRIGHT;
            }
            else if (name.EndsWith("_downside"))
            {
                name = name.Substring(0, name.Length - 9);
                facingByte = FACING_DOWNLEFT | FACING_DOWNRIGHT;
            }
            else if (name.EndsWith("_upleft"))
            {
                name = name.Substring(0, name.Length - 7);
                facingByte = FACING_UPLEFT;
            }
            else if (name.EndsWith("_upright"))
            {
                name = name.Substring(0, name.Length - 8);
                facingByte = FACING_UPRIGHT;
            }
            else if (name.EndsWith("_downleft"))
            {
                name = name.Substring(0, name.Length - 9);
                facingByte = FACING_DOWNLEFT;
            }
            else if (name.EndsWith("_downright"))
            {
                name = name.Substring(0, name.Length - 10);
                facingByte = FACING_DOWNRIGHT;
            }
            else if (name.EndsWith("_45s"))
            {
                name = name.Substring(0, name.Length - 4);
                facingByte = FACING_UPLEFT | FACING_UPRIGHT | FACING_DOWNLEFT | FACING_DOWNRIGHT;
            }
            else if (name.EndsWith("_90s"))
            {
                name = name.Substring(0, name.Length - 4);
                facingByte = FACING_UP | FACING_DOWN | FACING_LEFT | FACING_RIGHT;
            }

            // 写入动画名称和方向
            WritePrefixedString(writer, name, isLittleEndian);
            writer.Write(facingByte);

            // 写入root哈希、帧率和帧数
            string root = animNode.GetAttribute("root");
            uint rootHash = StrHash(root, hashCollection);
            WriteUInt32(writer, rootHash, isLittleEndian);

            float frameRate = float.Parse(animNode.GetAttribute("framerate"));
            WriteFloat(writer, frameRate, isLittleEndian);

            XmlNodeList frameNodes = animNode.GetElementsByTagName("frame");
            WriteUInt32(writer, (uint)frameNodes.Count, isLittleEndian);

            // 处理每个帧
            foreach (XmlNode frameNode in frameNodes)
            {
                ProcessFrameNode(writer, (XmlElement)frameNode, hashCollection, isLittleEndian);
            }
        }

        /// <summary>
        /// 处理帧节点
        /// </summary>
        private static void ProcessFrameNode(BinaryWriter writer, XmlElement frameNode, Dictionary<uint, string> hashCollection, bool isLittleEndian)
        {
            // 写入帧属性
            float x = float.Parse(frameNode.GetAttribute("x"));
            float y = float.Parse(frameNode.GetAttribute("y"));
            float w = float.Parse(frameNode.GetAttribute("w"));
            float h = float.Parse(frameNode.GetAttribute("h"));

            WriteFloat(writer, x, isLittleEndian);
            WriteFloat(writer, y, isLittleEndian);
            WriteFloat(writer, w, isLittleEndian);
            WriteFloat(writer, h, isLittleEndian);

            // 处理事件
            XmlNodeList eventNodes = frameNode.GetElementsByTagName("event");
            WriteUInt32(writer, (uint)eventNodes.Count, isLittleEndian);

            foreach (XmlNode eventNode in eventNodes)
            {
                string name = ((XmlElement)eventNode).GetAttribute("name");
                uint nameHash = StrHash(name, hashCollection);
                WriteUInt32(writer, nameHash, isLittleEndian);
            }

            // 处理元素
            XmlNodeList elementNodes = frameNode.GetElementsByTagName("element");

            // 按z_index排序
            List<XmlElement> sortedElements = new List<XmlElement>();
            foreach (XmlNode elementNode in elementNodes)
            {
                sortedElements.Add((XmlElement)elementNode);
            }
            sortedElements.Sort((a, b) => int.Parse(a.GetAttribute("z_index")).CompareTo(int.Parse(b.GetAttribute("z_index"))));

            WriteUInt32(writer, (uint)sortedElements.Count, isLittleEndian);

            int eidx = 0;
            foreach (XmlElement elementNode in sortedElements)
            {
                // 写入元素属性
                string name = elementNode.GetAttribute("name");
                uint nameHash = StrHash(name, hashCollection);
                WriteUInt32(writer, nameHash, isLittleEndian);

                int frame = int.Parse(elementNode.GetAttribute("frame"));
                WriteUInt32(writer, (uint)frame, isLittleEndian);

                string layername = elementNode.GetAttribute("layername");
                if (layername.Contains("/"))
                {
                    layername = layername.Split('/').Last();
                }
                uint layernameHash = StrHash(layername, hashCollection);
                WriteUInt32(writer, layernameHash, isLittleEndian);

                // 写入变换矩阵
                float m_a = float.Parse(elementNode.GetAttribute("m_a"));
                float m_b = float.Parse(elementNode.GetAttribute("m_b"));
                float m_c = float.Parse(elementNode.GetAttribute("m_c"));
                float m_d = float.Parse(elementNode.GetAttribute("m_d"));
                float m_tx = float.Parse(elementNode.GetAttribute("m_tx"));
                float m_ty = float.Parse(elementNode.GetAttribute("m_ty"));

                // 计算z值
                float z = (eidx / (float)sortedElements.Count) * EXPORT_DEPTH - (EXPORT_DEPTH * 0.5f);

                WriteFloat(writer, m_a, isLittleEndian);
                WriteFloat(writer, m_b, isLittleEndian);
                WriteFloat(writer, m_c, isLittleEndian);
                WriteFloat(writer, m_d, isLittleEndian);
                WriteFloat(writer, m_tx, isLittleEndian);
                WriteFloat(writer, m_ty, isLittleEndian);
                WriteFloat(writer, z, isLittleEndian);

                eidx++;
            }
        }

        /// <summary>
        /// 将二进制动画数据导出为XML
        /// </summary>
        private static void ExportAnim(bool isLittleEndian, byte[] animData, TextWriter outFile, bool ignoreExceptions)
        {
            Dictionary<uint, string> hashCollection = new Dictionary<uint, string>();

            using (MemoryStream ms = new MemoryStream(animData))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                // 读取头部信息
                byte[] header = reader.ReadBytes(8);
                Console.WriteLine($"Header: {Encoding.ASCII.GetString(header, 0, 4)}, Version: {BitConverter.ToInt32(header, 4)}");

                uint elements = ReadUInt32(reader, isLittleEndian);
                uint frames = ReadUInt32(reader, isLittleEndian);
                uint events = ReadUInt32(reader, isLittleEndian);
                uint anims = ReadUInt32(reader, isLittleEndian);
                Console.WriteLine($"Elements: {elements}, Frames: {frames}, Events: {events}, Anims: {anims}");

                string[] dir = { "_up", "_down", "_side", "_left", "_right", "_upside", "_downside", "_upleft", "_upright", "_downleft", "_downright", "_45s", "_90s" };

                // 创建XML文档
                XmlDocument dom = new XmlDocument();
                XmlElement rootNode = dom.CreateElement("Anims");
                dom.AppendChild(rootNode);

                // 解析动画数据
                if (anims > 0)
                {
                    for (int i = 0; i < anims; i++)
                    {
                        XmlElement animNode = dom.CreateElement("anim");
                        rootNode.AppendChild(animNode);

                        // 读取名称
                        string name = ReadPrefixedString(reader, isLittleEndian);

                        // 读取方向类型
                        byte facingType = reader.ReadByte();
                        if (facingType == FACING_UP)
                            name += dir[0];
                        else if (facingType == FACING_DOWN)
                            name += dir[1];
                        else if (facingType == (FACING_LEFT | FACING_RIGHT))
                            name += dir[2];
                        else if (facingType == FACING_LEFT)
                            name += dir[3];
                        else if (facingType == FACING_RIGHT)
                            name += dir[4];
                        else if (facingType == (FACING_UPLEFT | FACING_UPRIGHT))
                            name += dir[5];
                        else if (facingType == (FACING_DOWNLEFT | FACING_DOWNRIGHT))
                            name += dir[6];
                        else if (facingType == FACING_UPLEFT)
                            name += dir[7];
                        else if (facingType == FACING_UPRIGHT)
                            name += dir[8];
                        else if (facingType == FACING_DOWNLEFT)
                            name += dir[9];
                        else if (facingType == FACING_DOWNRIGHT)
                            name += dir[10];
                        else if (facingType == (FACING_UPLEFT | FACING_UPRIGHT | FACING_DOWNLEFT | FACING_DOWNRIGHT))
                            name += dir[11];
                        else if (facingType == (FACING_UP | FACING_DOWN | FACING_LEFT | FACING_RIGHT))
                            name += dir[12];

                        animNode.SetAttribute("name", name);

                        // 读取其他属性
                        uint hash = ReadUInt32(reader, isLittleEndian);
                        float frameRate = ReadFloat(reader, isLittleEndian);
                        uint framesNum = ReadUInt32(reader, isLittleEndian);

                        animNode.SetAttribute("root", hash.ToString());
                        animNode.SetAttribute("framerate", ((int)frameRate).ToString());
                        animNode.SetAttribute("numframes", ((int)framesNum).ToString());

                        // 解析帧数据
                        if (framesNum > 0)
                        {
                            for (int iframe = 0; iframe < framesNum; iframe++)
                            {
                                XmlElement frameNode = dom.CreateElement("frame");
                                animNode.AppendChild(frameNode);

                                // 读取帧属性
                                float x = ReadFloat(reader, isLittleEndian);
                                float y = ReadFloat(reader, isLittleEndian);
                                float w = ReadFloat(reader, isLittleEndian);
                                float h = ReadFloat(reader, isLittleEndian);

                                frameNode.SetAttribute("w", w.ToString());
                                frameNode.SetAttribute("h", h.ToString());
                                frameNode.SetAttribute("x", x.ToString());
                                frameNode.SetAttribute("y", y.ToString());

                                // 解析事件
                                uint numEvents = ReadUInt32(reader, isLittleEndian);
                                if (numEvents > 0)
                                {
                                    for (int ievent = 0; ievent < numEvents; ievent++)
                                    {
                                        XmlElement eventNode = dom.CreateElement("event");
                                        frameNode.AppendChild(eventNode);
                                        uint nameHash = ReadUInt32(reader, isLittleEndian);
                                        eventNode.SetAttribute("name", nameHash.ToString());
                                    }
                                }

                                // 解析元素
                                uint numElements = ReadUInt32(reader, isLittleEndian);
                                if (numElements > 0)
                                {
                                    for (int ielements = 0; ielements < numElements; ielements++)
                                    {
                                        XmlElement elementsNode = dom.CreateElement("element");
                                        frameNode.AppendChild(elementsNode);

                                        uint nameHash = ReadUInt32(reader, isLittleEndian);
                                        uint frameInt = ReadUInt32(reader, isLittleEndian);
                                        uint layerNameHash = ReadUInt32(reader, isLittleEndian);

                                        float m_a = ReadFloat(reader, isLittleEndian);
                                        float m_b = ReadFloat(reader, isLittleEndian);
                                        float m_c = ReadFloat(reader, isLittleEndian);
                                        float m_d = ReadFloat(reader, isLittleEndian);
                                        float m_tx = ReadFloat(reader, isLittleEndian);
                                        float m_ty = ReadFloat(reader, isLittleEndian);
                                        float z = ReadFloat(reader, isLittleEndian);

                                        elementsNode.SetAttribute("name", nameHash.ToString());
                                        elementsNode.SetAttribute("layername", layerNameHash.ToString());
                                        elementsNode.SetAttribute("frame", frameInt.ToString());
                                        elementsNode.SetAttribute("z_index", (15 + ielements).ToString());
                                        elementsNode.SetAttribute("m_a", m_a.ToString());
                                        elementsNode.SetAttribute("m_b", m_b.ToString());
                                        elementsNode.SetAttribute("m_c", m_c.ToString());
                                        elementsNode.SetAttribute("m_d", m_d.ToString());
                                        elementsNode.SetAttribute("m_tx", m_tx.ToString());
                                        elementsNode.SetAttribute("m_ty", m_ty.ToString());
                                    }
                                }
                            }
                        }
                    }
                }

                // 读取哈希表
                uint hashs = ReadUInt32(reader, isLittleEndian);
                for (int ihash = 0; ihash < hashs; ihash++)
                {
                    uint hashId = ReadUInt32(reader, isLittleEndian);
                    int hashLen = ReadInt32(reader, isLittleEndian);
                    byte[] hashStrBytes = reader.ReadBytes(hashLen);
                    string hashStr = Encoding.ASCII.GetString(hashStrBytes);
                    hashCollection[hashId] = hashStr;
                }                // 替换XML中的哈希值
                XmlNodeList nodes = rootNode.GetElementsByTagName("anim");
                foreach (XmlNode node in nodes)
                {
                    XmlElement inode = (XmlElement)node;
                    uint hashId = uint.Parse(inode.GetAttribute("root"));
                    if (hashCollection.ContainsKey(hashId))
                        inode.SetAttribute("root", hashCollection[hashId]);

                    XmlNodeList frameNodes = inode.GetElementsByTagName("frame");
                    foreach (XmlNode frame in frameNodes)
                    {
                        XmlElement iframe = (XmlElement)frame;

                        XmlNodeList eventNodes = iframe.GetElementsByTagName("event");
                        foreach (XmlNode evnt in eventNodes)
                        {
                            XmlElement ievent = (XmlElement)evnt;
                            uint eventHashId = uint.Parse(ievent.GetAttribute("name"));
                            if (hashCollection.ContainsKey(eventHashId))
                                ievent.SetAttribute("name", hashCollection[eventHashId]);
                        }
                        XmlNodeList elementNodes = iframe.GetElementsByTagName("element");
                        foreach (XmlNode element in elementNodes)
                        {
                            XmlElement ielement = (XmlElement)element;
                            uint elementHashId = uint.Parse(ielement.GetAttribute("name"));
                            if (hashCollection.ContainsKey(elementHashId))
                                ielement.SetAttribute("name", hashCollection[elementHashId]);

                            uint layerHashId = uint.Parse(ielement.GetAttribute("layername"));
                            if (hashCollection.ContainsKey(layerHashId))
                                ielement.SetAttribute("layername", hashCollection[layerHashId]);
                        }
                    }
                }

                // 输出XML
                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "\t",
                    NewLineChars = "\n"
                };

                using (XmlWriter writer = XmlWriter.Create(outFile, settings))
                {
                    dom.Save(writer);
                }
            }
        }
        static void Main(string[] args)
        {
            bool isLittleEndian = true; // 对应Python的endianstring = "<"
            string infile = string.Empty;

            // 如果有命令行参数，直接处理指定文件
            if (args.Length >= 1)
            {
                infile = args[0] ?? string.Empty;
            }
            // 否则扫描当前目录下的所有bin和xml文件并让用户选择
            else
            {
                // 获取当前目录下的所有.bin和.xml文件
                string currentDirectory = Directory.GetCurrentDirectory();
                string[] binFiles = Directory.GetFiles(currentDirectory, "*.bin");
                string[] xmlFiles = Directory.GetFiles(currentDirectory, "*.xml");

                List<string> allFiles = new List<string>();
                allFiles.AddRange(binFiles);
                allFiles.AddRange(xmlFiles);

                if (allFiles.Count == 0)
                {
                    Console.WriteLine("当前目录下没有找到.bin或.xml文件。");
                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey();
                    return;
                }

                // 显示文件列表
                Console.WriteLine("发现以下文件，请选择要处理的文件编号：");
                for (int i = 0; i < allFiles.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {Path.GetFileName(allFiles[i])}");
                }

                // 获取用户选择
                int selection = 0;
                bool validSelection = false;
                while (!validSelection)
                {
                    Console.Write("请输入文件编号: ");
                    string input = Console.ReadLine() ?? string.Empty;

                    if (int.TryParse(input, out selection) && selection >= 1 && selection <= allFiles.Count)
                    {
                        validSelection = true;
                    }
                    else
                    {
                        Console.WriteLine("无效的选择，请重新输入。");
                    }
                }

                // 设置选中的文件
                infile = allFiles[selection - 1];
            }
            try
            {
                string path = Path.GetDirectoryName(infile) ?? string.Empty;
                string baseName = Path.GetFileName(infile);
                string extension = Path.GetExtension(infile).ToLower();

                Console.WriteLine($"正在处理文件: {baseName}");

                // 根据文件扩展名决定执行哪个操作
                if (extension == ".bin")
                {
                    // bin -> xml
                    byte[] animData = File.ReadAllBytes(infile);
                    string outfilename = infile + ".xml";

                    using (StreamWriter fout = new StreamWriter(outfilename, false))
                    {
                        ExportAnim(isLittleEndian, animData, fout, false);
                    }

                    Console.WriteLine($"成功导出XML到: {outfilename}");
                }
                else if (extension == ".xml")
                {
                    // xml -> bin
                    string xmlData = File.ReadAllText(infile);
                    string outfilename = infile + ".bin";

                    using (FileStream fout = new FileStream(outfilename, FileMode.Create))
                    {
                        ImportAnim(isLittleEndian, xmlData, fout);
                    }

                    Console.WriteLine($"成功导出BIN到: {outfilename}");
                }
                else
                {
                    Console.WriteLine($"不支持的文件类型: {extension}");
                }

                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"处理错误: {infile}\n{e.Message}");
                Console.Error.WriteLine(e.StackTrace);
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                Environment.Exit(-1);
            }
        }
    }
}
