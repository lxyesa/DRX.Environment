using System;
using System.Collections.Generic;
using Drx.Sdk.Network.DataBase;

// 1. 定义一个简单的数据模型，并实现必要的接口
public class Note : IXmlSerializable, IIndexable
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public void WriteToXml(IXmlNode node)
    {
        node.PushString("data", "Id", Id);
        node.PushString("data", "Title", Title);
        node.PushString("data", "Content", Content);
    }
    public void ReadFromXml(IXmlNode node)
    {
        Id = node.GetString("data", "Id");
        Title = node.GetString("data", "Title");
        Content = node.GetString("data", "Content");
    }
}

class Program
{
    static void Main(string[] args)
    {
        // =================================================================
        // 步骤 1: 自动索引 (写入)
        // =================================================================
        
        // 只需一句话，就能初始化一个指向 "my_notes" 目录的“笔记仓库”
        var notesRepo = new IndexedRepository<Note>("my_notes");

        Console.WriteLine("正在批量保存3条笔记...");
        
        // 只需调用 .SaveAll()，仓库就会自动完成所有操作：
        // - 创建 my_notes/index.xml 索引文件
        // - 为每条笔记创建独立的 note-001.xml, note-002.xml... 数据文件
        // - 将它们全部关联起来
        notesRepo.SaveAll(new List<Note>
        {
            new Note { Id = "note-001", Title = "购物清单", Content = "牛奶, 面包" },
            new Note { Id = "note-002", Title = "会议纪要", Content = "讨论Q3季度成果" },
            new Note { Id = "note-003", Title = "个人提醒", Content = "记得打电话给朋友" }
        });

        Console.WriteLine(" -> 写入完成！一个自动化的索引系统已在 'my_notes' 文件夹中创建完毕。");
        Console.WriteLine();


        // =================================================================
        // 步骤 2: 单项读取 (按需加载)
        // =================================================================

        string idToFind = "note-002";
        Console.WriteLine($"正在通过ID '{idToFind}' 精准读取单条笔记...");
        
        // 只需调用 .Get()，就能直接从索引中找到并只加载那一个文件
        // 完全不会触及或加载 note-001 和 note-003 的数据
        Note singleNote = notesRepo.Get(idToFind);

        if (singleNote != null)
        {
            Console.WriteLine(" -> 读取成功!");
            Console.WriteLine($"    标题: {singleNote.Title}");
            Console.WriteLine($"    内容: {singleNote.Content}");
        }
    }
}