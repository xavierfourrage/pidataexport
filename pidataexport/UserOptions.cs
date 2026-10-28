using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pidataexport
{
    public class UserOptions
    {
        public string DefaultExportFolder { get; set; }

        public bool AutoOpenExcelAfterExport { get; set; }

        public bool MinimizeEFByDefault { get; set; }

        public double DefaultWidth { get; set; }

        public double DefaultHeight { get; set; }
    }
}
