using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pidataexport
{
    public class AppConfiguration
    {
        public string AFServer { get; set; }
        public string PIDAServer { get; set; }
        public string AFDatabase { get; set; }
        public List<string> SelectedElements { get; set; }
        public string PhaseFilter { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string SelectedTemplate { get; set; }
    }
}
