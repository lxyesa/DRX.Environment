using System;

namespace Drx.Sdk.Network.Attributes
{
    /// <summary>
    /// ���һ������ΪAPI�Ļ��࣬�����������·��
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class APIAttribute : Attribute
    {
        /// <summary>
        /// ��ȡAPI�Ļ���·��
        /// </summary>
        public string BasePath { get; }

        /// <summary>
        /// ��ʼ��APIAttribute����ʵ��
        /// </summary>
        /// <param name="basePath">API�Ļ���·��</param>
        public APIAttribute(string basePath)
        {
            BasePath = basePath;
        }
    }
}
