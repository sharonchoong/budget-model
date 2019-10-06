using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Budget_Model.Models
{
    public enum AccountType { Checking, Savings, CreditCard, Brokerage };

    public enum BrokerageInformation { Statement, Orders };

    interface IFinancialInstitution
    {
        string InstitutionName { get; }
        string ShortName { get; }
        Account[] Accounts { get; }
    }
    public abstract class BaseFinancialInstitution : IFinancialInstitution
    {
        public string InstitutionName { get; protected set; }
        public string ShortName { get; protected set; }
        public Account[] Accounts { get; protected set; }
        public bool? IsChecked { get; set; } = false;

        public void AddAccounts(Account[] accounts)
        {
            foreach (Account acc in accounts)
            {
                acc.FinancialInstitution = this;
                foreach (ReportFormat rf in acc.ExcelImportFormats)
                {
                    rf.Account = acc;
                }
            }
            Accounts = accounts;
        }

        /// <summary>
        /// Initializes a collection of all financial institutions where money and investments are managed.  
        /// Specifies all financial institutions for which a CSV import function is defined in ExcelImport.cs
        /// </summary>
        /// <returns>A collection of financial institutions represented by the FinancialInstitution class</returns>
        public virtual FinancialInstitution[] GetFinancialInstitutions()
        {
            var fi1 = new FinancialInstitution("BankSample", "B1");
            fi1.AddAccounts(new[]
            {
                new Account(AccountType.Checking),
                new Account(AccountType.Savings)
            });
            var fi2 = new FinancialInstitution("BrokerageSample", "B2");
            fi2.AddAccounts(new[]
            {
                new Account(AccountType.Brokerage, new ReportFormat[]
                {
                    ///can specify different export formats for the same bank/financial institution
                    new StatementReportFormat("Brokerage Statement Format"),
                    new TransactionReportFormat("Brokerage Orders and Activity Format")
                })
            });
            return new[] { fi1, fi2 };
        }
    }

    public class Account
    {
        public AccountType AccountType { get; protected set; }
        public string AccountTypeDescription
        {
            get
            {
                if (AccountType == AccountType.CreditCard)
                {
                    return "Credit Card";
                }
                else
                {
                    return AccountType.ToString();
                }
            }
        }
        public ReportFormat[] ExcelImportFormats { get; protected set; }
        public bool? IsChecked { get; set; } = false;
        public BaseFinancialInstitution FinancialInstitution { get; set; }

        public Account()
        {
        }

        public Account(AccountType account_type)
        {
            AccountType = account_type;
            ReportFormat rf;
            if (account_type == AccountType.Brokerage)
                rf = new StatementReportFormat("Default Format");
            else
                rf = new TransactionReportFormat("Default Format");
            ExcelImportFormats = new[] { rf };
        }

        public Account(AccountType account_type, ReportFormat[] import_formats)
        {
            AccountType = account_type;
            ExcelImportFormats = import_formats;
        }
    }

    /// <summary>
    /// Works together with CSV data import methods used in DataDefinitions.xaml.cs. 
    /// One Account may have different report CSV formats which can be defined in this class.
    /// </summary>
    public abstract class ReportFormat
    {
        public Account Account { get; set; }
        public string FormatName { get; set; }
        public bool? IsChecked { get; set; } = false;
        public string ReportDescription
        {
            get
            {
                if (this is StatementReportFormat)
                {
                    return "Statement";
                }
                else
                {
                    return "Orders and Activity";
                }
            }
        }
    }

    /// <summary>
    /// Works together with CSV data import methods used in DataDefinitions.xaml.cs. 
    /// One Account may have different transaction report CSV formats which can be defined in this class.
    /// </summary>
    public class TransactionReportFormat : ReportFormat
    {
        public TransactionReportFormat(string format_name)
        {
            FormatName = format_name;
        }
    }
    /// <summary>
    /// Works together with CSV data import methods used in DataDefinitions.xaml.cs. 
    /// One Account may have different statement report CSV formats which can be defined in this class.
    /// </summary>
    public class StatementReportFormat : ReportFormat
    {
        private bool? _needs_statement_date = null;

        /// <summary>
        /// Indicates whether the as-of date of brokerage statements needs to be specified manually (if it is not provided in the CSV export) 
        /// </summary>
        public bool? NeedsStatementDate
        {
            get
            {
                return _needs_statement_date == null ? false : true;
            }
        }
        public StatementReportFormat(string format_name)
        {
            FormatName = format_name;
            _needs_statement_date = null;
        }
        public StatementReportFormat(string format_name, bool? needs_statement_date)
        {
            FormatName = format_name;
            _needs_statement_date = needs_statement_date;
        }
    }

}
