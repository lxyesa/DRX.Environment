using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Collections.Generic;

namespace Drx.Sdk.Network.Components
{
    [HtmlTargetElement("clickable-card")]
    public class ClickableTagHelper : TagHelper
    {
        /// <summary>图标类名（如 "fa fa-user"）</summary>
        public string Icon { get; set; } = "fa fa-user";

        /// <summary>卡片标题</summary>
        public string Title { get; set; } = "";

        /// <summary>卡片描述</summary>
        public string Description { get; set; } = "";

        /// <summary>跳转链接（可选）</summary>
        public string? Href { get; set; } = null;

        /// <summary>转发到 a/button 的属性，使用 forward- 前缀，如 forward-target="_blank"</summary>
        [HtmlAttributeName(DictionaryAttributePrefix = "forward-")]
        public Dictionary<string, string> ForwardedAttributes { get; set; } = new();

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!string.IsNullOrEmpty(Href))
            {
                output.TagName = "a";
                output.Attributes.SetAttribute("href", Href);
                output.Attributes.SetAttribute("class", "card clickable-card");
            }
            else
            {
                output.TagName = "button";
                output.Attributes.SetAttribute("class", "card clickable-card");
            }

            // 转发属性，避免覆盖已设置的属性
            foreach (var attr in ForwardedAttributes)
            {
                // 框架已经去掉了"forward-"前缀，直接使用键作为属性名
                if (!output.Attributes.ContainsName(attr.Key))
                {
                    output.Attributes.SetAttribute(attr.Key, attr.Value);
                }
            }
            

            output.Content.SetHtmlContent($@"
    <i class='card-icon {Icon}'></i>
    <div class='card-info'>
        <h4>{Title}</h4>
        <p>{Description}</p>
    </div>
    <div class='card-footer'>
        <i class='fa fa-chevron-right'></i>
    </div>
    ");
        }
    }
}