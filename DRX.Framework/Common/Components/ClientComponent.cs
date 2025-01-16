
using System.IO;
using System.Net;
using System.Text.Json.Serialization;
using DRX.Framework.Common.Interface;
using DRX.Framework.Common.Utility;

namespace DRX.Framework.Common.Components;

public class ClientComponent : IComponent
{
    public int Id { get; set; }
    public string? IP { get; private set; }
    public int Port { get; private set; }

    [JsonIgnore]
    public object? Owner { get; set; }

    // 下面的是一些关于账号的信息
    public bool IsBannded { get; set; }
    public DateTime BanndedDate { get; set; }   // 被封禁的时间
    public DateTime UnBandedDate { get; set; }   // 解封时间
    public int PermissionLevel { get; set; } = 1;
    public string? UID { get; set; }
    public string? Name { get; set; } = "游客";
    public string? Password { get; set; }
    public string? Email { get; set; } = "未知/未绑定";

    public DRXSocket? GetSocket()
    {
        return Owner as DRXSocket;
    }

    public void SetPermissionLevel(int level)
    {
        PermissionLevel = level;
    }

    public void Start()
    {
        var socket = Owner as DRXSocket;
        if (socket?.RemoteEndPoint is IPEndPoint endpoint)
        {
            IP = endpoint.Address.ToString();
            Port = endpoint.Port;
            Id = GetHashCode();
        }
    }

    public void Awake()
    {

    }

    public void OnDestroy()
    {

    }

    public void Dispose()
    {

    }

    public void Ban(DateTime date)
    {
        IsBannded = true;
        BanndedDate = DateTime.Now;
        UnBandedDate = date;
    }

    public void UnBan()
    {
        IsBannded = false;
        BanndedDate = DateTime.MinValue;
        UnBandedDate = DateTime.MinValue;
    }

    public async Task SaveToFileAsync(string path)
    {
        string fullPath = Path.Combine(path, $"{UID}.json");
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory == null)
        {
            throw new InvalidOperationException("无法获取目录路径");
        }

        if (!Directory.Exists(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        if (!Directory.Exists(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        await DRXFile.SaveToJsonAsync(fullPath, this);
    }

    public async Task LoadFromFileAsync(string path, string fileName)
    {
        string fullPath = Path.Combine(path, fileName);
        var loadedData = await DRXFile.LoadFromJsonAsync<ClientComponent>(fullPath);
        if (loadedData != null)
        {
            Id = loadedData.Id;
            IP = loadedData.IP;
            Port = loadedData.Port;
            Owner = loadedData.Owner;
            IsBannded = loadedData.IsBannded;
            BanndedDate = loadedData.BanndedDate;
            UnBandedDate = loadedData.UnBandedDate;
            PermissionLevel = loadedData.PermissionLevel;
            UID = loadedData.UID;
            Name = loadedData.Name;
            Password = loadedData.Password;
            Email = loadedData.Email;
        }
    }

    public void SaveToFile(string path)
    {
        SaveToFileAsync(path).GetAwaiter().GetResult();
    }

    public void LoadFromFile(string path, string fileName)
    {
        LoadFromFileAsync(path, fileName).GetAwaiter().GetResult();
    }
}
