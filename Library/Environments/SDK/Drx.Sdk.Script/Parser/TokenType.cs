﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.Parser
{
    public enum TokenType
    {
        Identifier,
        Number,
        String,
        Operator,
        Keyword,
        Punctuation,
        EndOfFile
    }
}
