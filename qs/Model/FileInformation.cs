﻿namespace qs.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    [Serializable]
    class FileInformation
    {
        public int Length { get; set; }
        public string  FileName { get; set; }
    }
}