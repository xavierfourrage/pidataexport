using OSIsoft.AF.Asset;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pidataexport
{
    public class MultiSelectItem
    {
        public AFElement Element { get; set; }

        public string Name => Element?.Name;

        public AFElementTemplate Template => Element?.Template;

        public bool IsSelected { get; set; }

        public bool IsEnabled { get; set; } = true;
    }
}
