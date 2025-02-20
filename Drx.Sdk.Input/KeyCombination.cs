using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Input
{
    public class KeyCombination
    {
        public HashSet<uint> Keys { get; }

        public KeyCombination(params uint[] keys)
        {
            Keys = new HashSet<uint>(keys);
        }

        public override bool Equals(object obj)
        {
            if (obj is KeyCombination other)
            {
                return Keys.SetEquals(other.Keys);
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var key in Keys.OrderBy(k => k))
                {
                    hash = hash * 31 + key.GetHashCode();
                }
                return hash;
            }
        }
    }
}
