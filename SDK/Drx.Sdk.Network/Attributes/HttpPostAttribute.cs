using System;

namespace Drx.Sdk.Network.Attributes
{
    /// <summary>
    /// ���һ������ΪHTTP POST���������
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class HttpPostAttribute : Attribute
    {
        /// <summary>
        /// ��ȡPOST�����·��
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// ��ʼ��HttpPostAttribute����ʵ��
        /// </summary>
        /// <param name="path">POST�����·��</param>
        public HttpPostAttribute(string path)
        {
            Path = path;
        }
    }
}
