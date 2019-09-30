using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Budget_Model.Models
{
    interface ITransaction
    {
        DateTime Date { get; set; }
        string Category { get; set; }
        string Description { get; set; }
        double Amount { get; set; }
        string Holder { get; set; }
        string Bank { get; set; }

        void Save();
    }
}
