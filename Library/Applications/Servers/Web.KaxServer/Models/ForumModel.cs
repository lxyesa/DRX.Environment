using System;
using System.Xml.Serialization;

namespace Web.KaxServer.Models;

public class ForumCategoryModel
{
    public string Id { get; set; } /* 版块ID */
    public string Title { get; set; } /* 版块标题 */
    public string Description { get; set; } /* 版块描述 */
    public string IconClass { get; set; } /* 版块图标 */
    public int ThreadCount { get; set; } /* 版块帖子数量 */
    public int PostCount { get; set; } /* 版块评论数量 */
    public List<string> ThreadIds { get; set; } = new(); /* 版块帖子ID列表 */

    [XmlIgnore]
    public List<ForumThreadModel> Threads { get; set; } = new(); /* 版块帖子列表, 运行时加载 */

    public string? LastThreadId { get; set; } /* 版块最后帖子ID */
    
    [XmlIgnore]
    public ForumThreadModel? LastThread { get; set; } /* 版块最后帖子, 运行时加载 */
    public List<int> Moderators { get; set; } = new(); /* 版块管理员列表 UserSession.UserId */
}

public class ForumThreadModel
{
    public string Id { get; set; } /* 帖子ID */
    public string CategoryId { get; set; } /* 所属版块ID */
    public string Title { get; set; } /* 帖子标题 */
    public string AuthorName { get; set; } /* 主题帖作者 UserSession.Username */
    public string AuthorAvatarUrl { get; set; } /* 作者头像URL */
    public string Content { get; set; } /* 支持 Markdown */
    public DateTime PostTime { get; set; } /* 帖子发布时间 */
    public List<ForumThreadCommentModel> Comments { get; set; } = new(); /* 帖子评论列表 */
    public int Views { get; set; } /* 浏览量 */
    public LastPostInfoModel LastPostInfo { get; set; } /* 最后回复信息 */
}

public class ForumThreadCommentModel
{
    public string Id { get; set; } /* 评论ID */
    public string AuthorName { get; set; } /* 评论贴作者 UserSession.Username */
    public string Content { get; set; } /* 支持 Markdown */
    public DateTime PostTime { get; set; } /* 评论发布时间 */
}

public class LastPostInfoModel
{
    public string AuthorName { get; set; }
    public DateTime PostTime { get; set; }
}

public class ForumModel
{

}
