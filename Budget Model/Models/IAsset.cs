using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Budget_Model.Models
{
    interface IAsset
    {
        DateTime AsOf { get; set; }
        string Description { get; set; }
        double Value { get; set; }
        string Holder { get; set; }
        string Bank { get; set; }

        void Save();
    }
}
