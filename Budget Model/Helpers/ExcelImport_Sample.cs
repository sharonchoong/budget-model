using Budget_Model.Models;
using CsvHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace Budget_Model.Helpers
{
    static class ExcelImport
    {
        public static void ImportFromExcel(string filename, ReportFormat bank, string holder, DateTime? date_statement)
        {
            int count_entries = 0;
            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    var csv = new CsvReader(sr);

                    while (csv.Read())
                    {
                        DateTime dateField;
                        double amount = 0;
                        if (csv.GetField(0) != "" && csv.TryGetField(0, out dateField))
                        {
                            //asset
                            if (bank is StatementReportFormat)
                            {
                                if (((StatementReportFormat)bank).NeedsStatementDate == true)
                                {
                                    if (date_statement == null)
                                    {
                                        MessageBox.Show("Please enter the date of the statement.", "Error: Date Missing on Upload");
                                        return;
                                    }
                                }
                                IAsset new_asset = new BrokerageAsset();
                                InvestmentChange investment_change = new InvestmentChange();
                                switch (bank.Account.FinancialInstitution.InstitutionName)
                                {
                                    case "BrokerageSample":
                                        investment_change.Date = Convert.ToDateTime(csv.GetField(0));
                                        investment_change.Amount = Convert.ToDouble(csv.GetField(7));

                                        new_asset = new BrokerageAsset(csv.GetField(2));
                                        new_asset.AsOf = Convert.ToDateTime(csv.GetField(0));
                                        new_asset.Description = csv.GetField(1);
                                        new_asset.Value = Convert.ToDouble(csv.GetField(3));
                                        break;
                                    default:
                                        break;
                                }
                                new_asset.Holder = holder;
                                new_asset.Bank = bank.Account.FinancialInstitution.ShortName;
                                new_asset.Save();
                                investment_change.Holder = holder;
                                investment_change.Bank = bank.Account.FinancialInstitution.ShortName;
                                investment_change.Save();
                            }
                            //transaction
                            else
                            {
                                ITransaction new_transaction = new BankTransaction();
                                string description = "";
                                if (bank.Account.AccountType == AccountType.Brokerage)
                                {
                                    switch (bank.Account.FinancialInstitution.InstitutionName)
                                    {
                                        case "BrokerageSample":
                                            string[] quantity_price = csv.GetField(4).ToString().Split(new string[] { " shares at�$", " share at�$" }, StringSplitOptions.None);
                                            new_transaction = new BrokerageTransaction(Convert.ToDouble(quantity_price[1].Replace(",", "")), Convert.ToDouble(quantity_price[0]), csv.GetField(2));
                                            description = csv.GetField(1);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                else
                                {
                                    new_transaction = new BankTransaction(bank.Account.AccountType);
                                    switch (bank.Account.FinancialInstitution.ShortName + " " + bank.FormatName)
                                    {
                                        case "B1 Default Format":
                                            csv.TryGetField(1, out amount);
                                            description = csv.GetField(2);
                                            break;
                                        default:
                                            break;
                                    }
                                    new_transaction.Amount = amount;
                                }
                                new_transaction.Bank = bank.Account.FinancialInstitution.ShortName;
                                new_transaction.Description = description;
                                new_transaction.Holder = holder;
                                new_transaction.Date = dateField;
                                new_transaction.Save();
                            }
                            count_entries++;
                        }
                    }
                }
            }

            MessageBox.Show("Detected " + count_entries + " entries and uploaded all non-duplicated entries to database.", "Upload Complete");
        }
    }
}
