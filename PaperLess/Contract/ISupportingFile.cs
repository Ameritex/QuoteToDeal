﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Quote_To_Deal.PaperLess.Contract
{
    public interface ISupportingFile
    {
        public string Filename { get; set; }
        public string Url { get; set; }
    }
}
