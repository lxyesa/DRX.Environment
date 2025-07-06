using System;

namespace NDV.Models;

public class MessageBox
{
    public string Id { get; set; } = "ndvMessageBox";
    public string Type { get; set; } = "Info"; // Success, Error, Warning, Info
    public string Title { get; set; } = "消息提示";
    public string Content { get; set; } = "";
    public string Details { get; set; } = "";
    public bool ShowTimestamp { get; set; } = true;
    public bool ShowCancel { get; set; } = true;
    public string ConfirmText { get; set; } = "确定";
    public string CancelText { get; set; } = "关闭";
    public string OnConfirm { get; set; } = "";
    public string OnCancel { get; set; } = "";
    public bool AutoShow { get; set; } = false;
    public int AutoClose { get; set; } = 0;
    public bool IsActive { get; set; } = false;

    public string GetIconClass()
    {
        return Type.ToLower() switch
        {
            "success" => "fa-check",
            "error" => "fa-exclamation-circle",
            "warning" => "fa-exclamation-triangle",
            _ => "fa-info-circle"
        };
    }
}
