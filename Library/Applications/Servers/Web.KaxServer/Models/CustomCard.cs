using System.Collections.Generic;
using System.Xml.Linq;

namespace Web.KaxServer.Models
{
    public class CustomCard
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<CustomElement> Elements { get; set; } = new List<CustomElement>();
        
        public XElement ToXml()
        {
            var element = new XElement("CustomCard",
                new XAttribute("Id", Id),
                new XElement("Title", Title),
                new XElement("Elements",
                    Elements.Select(e => e.ToXml())
                )
            );
            
            return element;
        }
        
        public static CustomCard FromXml(XElement element)
        {
            if (element == null) return null;
            
            var card = new CustomCard
            {
                Id = element.Attribute("Id")?.Value ?? string.Empty,
                Title = element.Element("Title")?.Value ?? string.Empty,
                Elements = new List<CustomElement>()
            };
            
            var elementsElement = element.Element("Elements");
            if (elementsElement != null)
            {
                foreach (var elementXml in elementsElement.Elements("CustomElement"))
                {
                    var customElement = CustomElement.FromXml(elementXml);
                    if (customElement != null)
                    {
                        card.Elements.Add(customElement);
                    }
                }
            }
            
            return card;
        }
    }
    
    public class CustomElement
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // 例如：text, image, link等
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty; // 用于滑块和链接类型的标签
        
        public XElement ToXml()
        {
            var element = new XElement("CustomElement",
                new XAttribute("Id", Id),
                new XAttribute("Type", Type),
                new XElement("Value", Value)
            );
            
            // 只有在Label不为空时才添加Label元素
            if (!string.IsNullOrEmpty(Label))
            {
                element.Add(new XElement("Label", Label));
            }
            
            return element;
        }
        
        public static CustomElement FromXml(XElement element)
        {
            if (element == null) return null;
            
            var customElement = new CustomElement
            {
                Id = element.Attribute("Id")?.Value ?? string.Empty,
                Type = element.Attribute("Type")?.Value ?? string.Empty,
                Value = element.Element("Value")?.Value ?? string.Empty,
                Label = element.Element("Label")?.Value ?? string.Empty
            };
            
            return customElement;
        }
    }
} 