using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Budget_Model.Models
{
    /// Implement your own FinancialInstitution class and override the hard-coded GetFinancialInstitutions method

    public class FinancialInstitution : BaseFinancialInstitution
    {
        public FinancialInstitution()
        {
        }

        public FinancialInstitution(string institution_name, string shortname)
        {
            InstitutionName = institution_name;
            ShortName = shortname;
        }

        /**
        public override FinancialInstitution[] GetFinancialInstitutions()
        {
            return new FinancialInstitution[] {  };
        }
        **/
    }
}
