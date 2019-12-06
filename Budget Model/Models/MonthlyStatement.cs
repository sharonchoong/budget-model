using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Budget_Model.Models
{
    class MonthlyStatement
    {
        public DateTime statement_date { get; set; }
        public string holder { get; set; }

        public static Tuple<int, int> CheckUndefinedOrDuplicates(DateTime start_date, DateTime end_date)
        {
            string qry = "SELECT COALESCE((SELECT COUNT(1) FROM Statements where category is null and date(date) BETWEEN date(@start) AND date(@end)), 0 ) as undefined, ";
            qry += " coalesce((SELECT COUNT(1) FROM statements WHERE date(date) BETWEEN date(@start) AND date(@end) GROUP BY id HAVING COUNT(1) > 1 LIMIT 1),0) as duplicates";
            Tuple<int, int> tuple;
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    cmd.Parameters.AddWithValue("@start", start_date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@end", end_date.ToString("yyyy-MM-dd"));
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        reader.Read();
                        tuple = new Tuple<int, int>(reader.GetInt32(0), reader.GetInt32(1));
                        conn.Close();
                    }
                }
            }
            return tuple;
        }

        public Dictionary<string, double> GetAggregateCategoryAmount()
        {
            Dictionary<string, double> result = new Dictionary<string, double>();
            DateTime start_month = new DateTime(Convert.ToDateTime(statement_date).Year, Convert.ToDateTime(statement_date).Month, 1);
            string qry = "select [period], Categories.category, COALESCE(amount,0) as amount FROM Categories LEFT JOIN " + (holder == "Home" ? "Home" : "Individual");
            qry += "_Monthly a ON Categories.category = a.category AND date(a.[period]) = date(@startdate) ";
            qry += ((holder != "Home") ? " AND holder = @holder " : "");
            qry += " UNION SELECT @startdate, 'Gross Salary', COALESCE((SELECT sum(gross_salary) FROM GrossSalary WHERE date([date]) = date(@startdate)";
            qry += ((holder != "Home") ? " AND holder = @holder" : "") + " GROUP BY [date]), 0)";

            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    if (holder != "Home")
                    {
                        cmd.Parameters.Add("@holder", DbType.String, 50).Value = holder;
                    }
                    cmd.Parameters.AddWithValue("@startdate", start_month.ToString("yyyy-MM-dd"));
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            double amount = 0;
                            if (!reader.IsDBNull(reader.GetOrdinal("amount")))
                            {
                                amount = reader.GetDouble(reader.GetOrdinal("amount"));
                            }
                            string category = reader.GetString(reader.GetOrdinal("category"));
                            result.Add(category, amount);
                        }
                    }
                    conn.Close();
                }
            }
            return result;
        }

        public Tuple<List<double>, List<double>, List<string>> CompareMonths(DateTime month_to_compare)
        {
            List<double> this_month_amounts = new List<double>();
            List<double> last_month_amounts = new List<double>();
            List<string> labels = new List<string>();

            string qry = "select Categories.category, a.amount as amount_thismonth, b.amount as amount_lastmonth, category_order ";
            qry += " FROM Categories LEFT JOIN " + (holder == "Home" ? "Home" : "Individual") + "_Monthly a ON Categories.category = a.category AND date(a.[period]) = date(@thisquarter) ";
            qry += ((holder != "Home") ? " AND a.holder = @holder " : "");
            qry += " LEFT JOIN " + (holder == "Home" ? "Home" : "Individual") + "_Monthly b ON Categories.category = b.category AND date(b.[period]) =date(@lastquarter) ";
            qry += ((holder != "Home") ? " AND b.holder = @holder " : "");
            qry += " WHERE (Categories.category != 'Miscellaneous') ";
            string holder_qry = (holder != "Home" ? (" and holder = @holder ") : "");
            qry += " UNION SELECT 'Savings ex Misc.' as category, (SELECT SUM(amount) from NetSavings_bef_misc_Monthly WHERE date(period) = date(@thisquarter)" + holder_qry + ") as amount_thismonth, "
                    + "(SELECT SUM(amount) from NetSavings_bef_misc_Monthly WHERE date(period) = date(@lastquarter)" + holder_qry + ") as amount_lastmonth, 1000 as category_order "
                    + " ORDER BY Categories.category_order";

            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    cmd.Parameters.AddWithValue("@lastquarter", new DateTime(Convert.ToDateTime(month_to_compare).Year, Convert.ToDateTime(month_to_compare).Month, 1).ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@thisquarter", new DateTime(Convert.ToDateTime(statement_date).Year, Convert.ToDateTime(statement_date).Month, 1).ToString("yyyy-MM-dd"));
                    if (holder != "Home")
                    {
                        cmd.Parameters.Add("@holder", DbType.String, 50).Value = holder;
                    }
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            this_month_amounts.Add(reader.IsDBNull(reader.GetOrdinal("amount_thismonth")) ? 0 : reader.GetDouble(reader.GetOrdinal("amount_thismonth")));
                            last_month_amounts.Add(reader.IsDBNull(reader.GetOrdinal("amount_lastmonth")) ? 0 : reader.GetDouble(reader.GetOrdinal("amount_lastmonth")));
                            labels.Add(reader.GetString(reader.GetOrdinal("category")));
                        }
                    }
                    conn.Close();
                }
            }
            return new Tuple<List<double>, List<double>, List<string>>(this_month_amounts, last_month_amounts, labels);
        }

        public double GetBalance(AccountType acctype, string bank)
        {
            double result = 0;
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                switch (acctype)
                {
                    case AccountType.Savings:
                        bank += "_savings"; break;
                    case AccountType.Checking:
                        bank += "_checking"; break;
                    case AccountType.CreditCard:
                        bank += "_credit"; break;
                    default:
                        bank += "_broker"; break;
                }
                string qry = "";
                if (acctype == AccountType.Checking || acctype == AccountType.Savings)
                {
                    qry = "SELECT SUM(amount) FROM Entries WHERE date([date]) <= date(@enddate) AND bank = @bank";
                }
                else if (acctype == AccountType.Brokerage)
                {
                    qry = "SELECT SUM(ending_mkt_value) FROM FinancialAssets WHERE date([date]) = date(@enddate) AND bank = @bank";
                }
                if (holder != "Home")
                {
                    qry += " AND holder = @holder";
                }
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    cmd.Parameters.AddWithValue("@enddate", statement_date.ToString("yyyy-MM-dd HH:mm:ss"));
                    if (holder != "Home")
                    {
                        cmd.Parameters.AddWithValue("@holder", holder);
                    }
                    if (bank != "")
                    {
                        cmd.Parameters.AddWithValue("@bank", bank);
                    }
                    conn.Open();
                    if (cmd.ExecuteScalar() != null)
                    {
                        double.TryParse(cmd.ExecuteScalar().ToString(), out result);
                    }
                    conn.Close();
                }
            }
            return result;
        }

        public static DataTable GetTransactionsByCategory(string _category, DateTime statement_date, string selected_person)
        {
            string category = _category == null ? "": _category;
            switch (_category)
            {
                case "Net Pay Earnings":
                    category = "Payroll"; break;
                case "Total Income":
                    category = "Income"; break;
                case "Total Expenses":
                    category = "Expenses"; break;
                case "Miscellaneous Items":
                    category = "Miscellaneous"; break;
                case "Net Savings":
                    category = ""; break;
                case "Investment Gains or Losses":
                    category = "Investment Gains"; break;
            }

            DataTable dt = new DataTable();
            DateTime start_month = new DateTime(Convert.ToDateTime(statement_date).Year, Convert.ToDateTime(statement_date).Month, 1);
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
                {
                    string qry = "";
                    if (category == "Trading Account")
                    {
                        qry = "select date, asset_symbol as category, asset_description as description, ending_mkt_value as amount from FinancialAssets WHERE date([date]) = date(@end)";
                        qry += ((selected_person != "Home") ? " AND holder = @holder" : "");
                    }
                    else
                    {
                        qry = "select date, category, description, amount from Statements WHERE";
                        if (selected_person != "Home")
                        {
                            qry += " holder = @holder AND";
                        }
                        qry += " date([date]) BETWEEN date(@start) AND date(@end)";
                        if (category != null && !string.IsNullOrWhiteSpace(category))
                        {
                            if (category == "Income")
                            {
                                qry += " AND amount > 0 AND category != 'Miscellaneous' ";
                            }
                            else if (category == "Expenses")
                            {
                                qry += " AND amount < 0 AND category != 'Miscellaneous' ";
                            }
                            else if (category == "Gross Salary")
                            {
                                qry += " AND description like '%employer match%' AND category = 'Retirement Contribution' ";
                                qry += " UNION SELECT [date], 'Gross Salary', 'Gross Salary', sum(gross_salary) FROM GrossSalary WHERE date([date]) = date(@start) ";
                                qry += ((selected_person != "Home") ? " AND holder = @holder" : "") + " GROUP BY date([date])";
                            }
                            else
                            {
                                qry += " AND category = @category";
                            }
                        }

                    }
                    qry += " order by date(date)";
                    using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                    {
                        adapter.SelectCommand = cmd;
                        adapter.SelectCommand.Parameters.AddWithValue("@start", start_month.ToString("yyyy-MM-dd"));
                        adapter.SelectCommand.Parameters.AddWithValue("@end", statement_date.ToString("yyyy-MM-dd"));
                        if (category != null && !string.IsNullOrWhiteSpace(category))
                        {
                            adapter.SelectCommand.Parameters.Add("@category", DbType.String, 500).Value = category;
                        }
                        if (selected_person != "Home")
                        {
                            adapter.SelectCommand.Parameters.Add("@holder", DbType.String, 500).Value = selected_person;
                        }
                        adapter.Fill(dt);
                    }
                }
            }
            return dt;
        }

        public static DataTable GetHistoricalNetWorth(string datatable, DateTime from_month, DateTime to_month)
        {
            DataTable dt = new DataTable();
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
                {
                    string qry = @"SELECT date(period) as period, holder, COALESCE(RunningBalance, 0) + COALESCE(ending_mkt_value,0) AS NetWorth FROM 
                        (SELECT a.holder, a.period, ending_mkt_value, runningbalance FROM " + datatable + @" a LEFT JOIN EndMarketValues d ON DATE(a.period, 'start of month', '+1 month', '-1 day') = DATE(d.date) and a.holder = d.holder
                        WHERE a.period BETWEEN date(@start) and date(@end)
                        UNION SELECT d.holder, d.date, ending_mkt_value, runningbalance FROM EndMarketValues d LEFT JOIN " + datatable + @" a ON DATE(a.period, 'start of month', '+1 month', '-1 day') = DATE(d.date) and a.holder = d.holder
                        WHERE d.date BETWEEN date(@start) and date(@end)
                        )
                        UNION SELECT date(period), 'Home', COALESCE(RunningBalance, 0) + COALESCE(ending_mkt_value,0) as NetWorth FROM 
                        (SELECT b.period, ending_mkt_value, runningbalance FROM " + datatable + @"_All b LEFT JOIN (SELECT [date], SUM(ending_mkt_value) as ending_mkt_value FROM EndMarketValues GROUP BY date([date])) c ON DATE(b.period, 'start of month', '+1 month', '-1 day') = DATE(c.date)
                        WHERE b.period BETWEEN date(@start) and date(@end)
                        UNION SELECT c.date, ending_mkt_value, runningbalance FROM (SELECT [date], SUM(ending_mkt_value) as ending_mkt_value FROM EndMarketValues GROUP BY date([date])) c LEFT JOIN " + datatable + @"_All b ON DATE(b.period, 'start of month', '+1 month', '-1 day') = DATE(c.date)
                        WHERE date(c.date) BETWEEN date(@start) and date(@end)
                        )
                        ORDER BY [period]";
                    using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                    {
                        adapter.SelectCommand = cmd;
                        adapter.SelectCommand.Parameters.AddWithValue("@start", from_month.ToString("yyyy-MM-dd"));
                        adapter.SelectCommand.Parameters.AddWithValue("@end", to_month.ToString("yyyy-MM-dd"));
                        adapter.Fill(dt);
                    }
                }
            }
            return dt;
        }

        public static DataTable GetHistoricalSavings(DateTime from_month, DateTime to_month)
        {
            DataTable dt = new DataTable();
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
                {
                    string qry = "";
                    if ((to_month - from_month).TotalDays < 93)
                    {
                        qry = @"SELECT a.period, a.holder, SUM(a.amount) OVER (PARTITION BY holder ORDER BY a.period) AS amount FROM 
                        NetSavings_bef_misc_Weekly a 
                        WHERE a.period BETWEEN DATE(@start, 'weekday 0', '+7 days') and DATE(@end, 'weekday 0', '+7 days')
                        UNION SELECT DISTINCT b.period, 'Home', SUM(b.amount) OVER (ORDER BY b.period) AS amount FROM 
                        NetSavings_bef_misc_Weekly b 
                        WHERE b.period BETWEEN DATE(@start, 'weekday 0', '+7 days') and DATE(@end, 'weekday 0', '+7 days')
                        ORDER BY [period]";
                    }
                    else
                    {
                        qry = @"SELECT a.period, a.holder, a.amount FROM 
                        NetSavings_bef_misc_Monthly a 
                        WHERE a.period BETWEEN date(@start) and date(@end)
                        UNION SELECT b.period, 'Home', SUM(b.amount) FROM 
                        NetSavings_bef_misc_Monthly b 
                        WHERE b.period BETWEEN date(@start) and date(@end)
                        GROUP BY b.period ORDER BY [period]";
                    }
                    using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                    {
                        adapter.SelectCommand = cmd;
                        adapter.SelectCommand.Parameters.AddWithValue("@start", from_month.ToString("yyyy-MM-dd"));
                        adapter.SelectCommand.Parameters.AddWithValue("@end", to_month.ToString("yyyy-MM-dd"));
                        adapter.Fill(dt);
                    }
                }
            }
            return dt;
        }

        public static DataTable GetHistoricalTransactionsByCategory(string selected_category, string frequency, DateTime from_month, DateTime to_month)
        {
            DataTable dt = new DataTable();
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
                {
                    string qry = "";
                    if (selected_category == "All Expenses ex. Miscellaneous" || selected_category == "All Income ex. Miscellaneous")
                    {
                        string operator_expenses_income = (selected_category == "All Expenses ex. Miscellaneous" ? "<" : ">");
                        qry = @"SELECT DATETIME(period) as period, holder, SUM(amount) as amount FROM (SELECT a.period, 'Home' as holder, a.amount FROM Home_" + frequency + @" a 
                        WHERE DATE(a.period) BETWEEN date(@start) and date(@end)
                        AND a.category != 'Miscellaneous' AND a.category != 'Investment Gains'
                        AND a.amount " + operator_expenses_income + @" 0
                        UNION SELECT DATETIME(b.period) as period, holder, b.amount FROM Individual_" + frequency + @" b 
                        WHERE DATE(b.period) BETWEEN date(@start) and date(@end)
                        AND b.category != 'Miscellaneous' AND b.category != 'Investment Gains'
                        AND b.amount " + operator_expenses_income + @" 0) all_tbl
                        GROUP BY date(period), holder ORDER BY [period]";
                    }
                    else if (selected_category == "Gross Salary")
                    {
                        qry = @"SELECT DATE([date]) as period, holder, gross_salary as amount FROM GrossSalary a 
                        WHERE DATE(a.date) BETWEEN date(@start) and date(@end)
                        UNION SELECT DATE([date]) as period, 'Home' as holder, SUM(gross_salary) as amount FROM GrossSalary b
                        WHERE DATE(b.date) BETWEEN date(@start) and date(@end)
                        GROUP BY date(b.date) ORDER BY [period]";
                    }
                    else
                    {
                        qry = @"SELECT DATETIME(a.period) as period, 'Home' as holder, a.category, a.amount FROM Home_" + frequency + @" a 
                        WHERE DATE(a.period) BETWEEN date(@start) and date(@end) " + (selected_category != "" ? "AND a.category = '" + selected_category + "'" : "");
                        if (selected_category == "")
                        {
                            qry += " AND a.category != 'Miscellaneous' AND a.category != 'Investment Gains' and amount < 0";
                        }
                        if (selected_category != "")
                        {
                            qry += @" UNION SELECT DATETIME(b.period) as period, holder, b.category,  b.amount FROM Individual_" + frequency + @" b 
                            WHERE DATE(b.period) BETWEEN date(@start) and date(@end) AND b.category = '" + selected_category + "'";
                        }
                        qry += " ORDER BY [period]";

                    }
                    using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                    {
                        adapter.SelectCommand = cmd;
                        adapter.SelectCommand.Parameters.AddWithValue("@start", from_month.ToString("yyyy-MM-dd"));
                        adapter.SelectCommand.Parameters.AddWithValue("@end", to_month.ToString("yyyy-MM-dd"));
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        public static Tuple<string, string, string> GetHistoricalAggregateCategoryAmount(string type, string selected_person, DateTime from_month, DateTime to_month)
        {
            Tuple<string, string, string> mean_median_std = new Tuple<string, string, string>("", "", "");
            string qry_table = "";
            if (type == "expenses")
            {
                qry_table = "(SELECT period, SUM(amount) as amount FROM " + (selected_person == "Home" ? "Home" : "Individual") + @"_Monthly
                          WHERE date(period) BETWEEN date(@start) and date(@end) " + (selected_person == "Home" ? "" : "AND holder = @holder ") + @"
                          AND category != 'Miscellaneous' AND amount < 0 GROUP BY period)a";
            }
            else if (type == "savings")
            {
                if (selected_person != "Home")
                {
                    qry_table = @"(SELECT * FROM NetSavings_bef_misc_Monthly a 
                              WHERE a.period BETWEEN date(@start) and date(@end) AND holder = @holder)a";
                }
                else
                {
                    qry_table = @"(SELECT SUM(amount) as amount FROM NetSavings_bef_misc_Monthly a 
                              WHERE a.period BETWEEN date(@start) and date(@end) GROUP BY date([period]))a";
                }
            }
            else
            {
                qry_table = "(SELECT * FROM " + (selected_person == "Home" ? "Home" : "Individual") + @"_Monthly a 
                          WHERE date(a.period) BETWEEN date(@start) and date(@end) " + (selected_person == "Home" ? "" : "AND a.holder = @holder ") + @"
                         AND a.category = '" + type + "')a";
            }


            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry = @"SELECT (SELECT AVG(amount) as median FROM (SELECT amount
                                FROM " + qry_table + @" ORDER BY amount LIMIT 2 - (SELECT COUNT(*) FROM " + qry_table + @") % 2 
                                OFFSET (SELECT (COUNT(*) - 1) / 2 FROM " + qry_table + @"))) as median,
                                AVG(a.amount) as mean, STDEV(a.amount) as std FROM " + qry_table;
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    if (selected_person != "Home")
                    {
                        cmd.Parameters.Add("@holder", DbType.String, 50).Value = selected_person;
                    }
                    cmd.Parameters.AddWithValue("@start", from_month.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@end", to_month.ToString("yyyy-MM-dd"));
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string mean = (!reader.IsDBNull(reader.GetOrdinal("mean")) ? reader.GetDouble(reader.GetOrdinal("mean")).ToString("C2") : "");
                            string median = (!reader.IsDBNull(reader.GetOrdinal("median")) ? reader.GetDouble(reader.GetOrdinal("median")).ToString("C2") : "");
                            string std = (!reader.IsDBNull(reader.GetOrdinal("std")) ? reader.GetDouble(reader.GetOrdinal("std")).ToString("C2") : "");
                            mean_median_std = new Tuple<string, string, string>(mean, median, std);
                        }
                    }
                    conn.Close();
                }
            }
            return mean_median_std;
        }
    }
}
