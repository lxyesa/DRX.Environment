using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DRX.Framework.Common.Enums;

namespace DRX.Framework.Common.Interface
{
    public interface IEngine
    {
        public EngineType Type { get; }
    }
}
