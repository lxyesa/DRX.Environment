using System;

namespace Drx.Sdk.Network.Attributes
{
    /// <summary>
    /// ���һ������ΪHTTP GET���������
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class HttpGetAttribute : Attribute
    {
        /// <summary>
        /// ��ȡGET�����·��
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// ��ʼ��HttpGetAttribute����ʵ��
        /// </summary>
        /// <param name="path">GET�����·��</param>
        public HttpGetAttribute(string path)
        {
            Path = path;
        }
    }
}
