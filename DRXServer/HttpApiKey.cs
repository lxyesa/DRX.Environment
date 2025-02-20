using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRXServer
{
    public struct HttpApiKey
    {
        public string[] Keys { get; set; }

        public string[] GetKeys()
        {
            return Keys;
        }
    }
}
