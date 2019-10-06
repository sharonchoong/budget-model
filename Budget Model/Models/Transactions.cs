using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Budget_Model.Models
{
    public class BankTransaction : ITransaction
    {
        public DateTime Date { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public double Amount { get; set; }
        public string Holder { get; set; }
        public string Bank { get; set; }
        public AccountType AccountType { get; set; } = AccountType.Checking;

        public BankTransaction()
        {
            AccountType = AccountType.Checking;
        }
        public BankTransaction(AccountType account_type)
        {
            AccountType = account_type;
        }

        public void Save()
        {
            if (Date != null && Amount != 0 && Description != null)
            {
                string _query = "INSERT INTO Entries (date,amount,description,holder,bank) SELECT @date, @amount, @description, @holder, @bank";
                _query += " WHERE NOT EXISTS (SELECT * FROM [Entries] WHERE date=@date and amount=@amount and description=@description and bank=@bank";
                if (AccountType != AccountType.CreditCard)
                {
                    _query += " and holder=@holder";
                }
                _query += " )";

                string bank_name = Bank;
                switch (AccountType)
                {
                    case AccountType.Savings:
                        bank_name += "_savings"; break;
                    case AccountType.CreditCard:
                        bank_name += "_credit"; break;
                    case AccountType.Brokerage:
                        bank_name += "_broker"; break;
                    default:
                        bank_name += "_checking"; break;
                }

                using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
                {
                    using (SQLiteCommand comm = new SQLiteCommand(_query, conn))
                    {
                        comm.Parameters.AddWithValue("@date", Date.ToString("yyyy-MM-dd"));
                        comm.Parameters.Add("@amount", DbType.Double).Value = Amount;
                        comm.Parameters.Add("@description", DbType.String, 500).Value = Description;
                        comm.Parameters.Add("@holder", DbType.String, 50).Value = Holder;
                        comm.Parameters.Add("@bank", DbType.String, 50).Value = bank_name;

                        conn.Open();
                        comm.ExecuteNonQuery();
                    }
                }
            }
        }

        public static Tuple<DateTime, DateTime> GetDates(string holder)
        {
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry = "select date(min(date)) as start_date, date(max(date)) as end_date from Entries ";
                if (holder != "Home")
                {
                    qry += " where holder = @holder ";
                } 
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    if (holder != "Home")
                    {
                        cmd.Parameters.Add("@holder", DbType.String, 50).Value = holder;
                    }
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.HasRows && reader.GetValue(reader.GetOrdinal("start_date")).ToString() != "")
                            {
                                return new Tuple<DateTime, DateTime>(reader.GetDateTime(reader.GetOrdinal("start_date")), reader.GetDateTime(reader.GetOrdinal("end_date")));
                            }
                        }
                    }
                    conn.Close();
                }
            }
            return null;
        }

        public void ChangeHolder(int id)
        {
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry = "UPDATE Entries SET holder = @holder WHERE id = @id";
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    cmd.Parameters.Add("@id", DbType.Int32).Value = id;
                    cmd.Parameters.Add("@holder", DbType.String, 50).Value = Holder;
                    conn.Open();
                    cmd.ExecuteNonQuery();
                    conn.Close();
                }
            }
        }
    }

    public class BrokerageTransaction : ITransaction
    {
        public DateTime Date { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Holder { get; set; }
        public string Bank { get; set; }
        public double Price { get; set; }
        public double Quantity { get; set; }
        public double Amount
        {
            get
            {
                return Price * Quantity;
            }
            set
            {
                Price = value / Quantity;
            }
        }
        public string Symbol { get; set; }
        public DateTime? MaturityDate { get; set; } = null;
        public double? CouponRate { get; set; } = null;
        public double? YieldToMaturity { get; set; } = null;

        public BrokerageTransaction(double price, double quantity, string symbol)
        {
            Price = price;
            Quantity = quantity;
            Symbol = symbol;
        }

        public BrokerageTransaction(double price, double quantity, string symbol, DateTime maturity, double coupon, double ytm)
        {
            Price = price;
            Quantity = quantity;
            Symbol = symbol;
            MaturityDate = maturity;
            CouponRate = coupon;
            YieldToMaturity = ytm;
        }

        public void Save()
        {
            if (Date != null && Price != 0 && Description != null && Symbol != null)
            {
                string _query = "INSERT INTO [InvestmentTransactions] ";
                _query += "(asset_symbol,date,transaction_description,holder,bank,price,quantity, maturity, coupon_rate, yield_to_maturity) ";
                _query += "SELECT @symbol, @date, @description, @holder, @bank, @price, @quantity, @maturity, @coupon, @yield ";
                _query += " WHERE NOT EXISTS (SELECT * FROM [InvestmentTransactions] WHERE date=@date and asset_symbol=@symbol ";
                _query += " and transaction_description=@description and holder=@holder and bank=@bank); ";

                using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
                {
                    using (SQLiteCommand comm = new SQLiteCommand(_query, conn))
                    {
                        comm.Parameters.Add("@symbol", DbType.String, 250).Value = Symbol;
                        comm.Parameters.AddWithValue("@date", Date.ToString("yyyy-MM-dd"));
                        comm.Parameters.Add("@description", DbType.String, 500).Value = Description;
                        comm.Parameters.Add("@holder", DbType.String, 50).Value = Holder;
                        comm.Parameters.Add("@bank", DbType.String, 50).Value = Bank + "_broker";
                        comm.Parameters.Add("@price", DbType.Double).Value = Price;
                        comm.Parameters.Add("@quantity", DbType.Double).Value = Quantity;
                        if (MaturityDate != null)
                        {
                            comm.Parameters.AddWithValue("@maturity", ((DateTime)MaturityDate).ToString("yyyy-MM-dd"));
                        }

                        else
                        {
                            comm.Parameters.AddWithValue("@maturity", DBNull.Value);
                            comm.Parameters.Add("@coupon", DbType.Double).Value = (object)CouponRate ?? DBNull.Value;
                            comm.Parameters.Add("@yield", DbType.Double).Value = (object)YieldToMaturity ?? DBNull.Value;
                        }

                        conn.Open();
                        comm.ExecuteNonQuery();
                        conn.Close();
                    }
                }
            }
        }

        public static DataTable GetHistoricalTransactions(string asset, string selected_holder, DateTime from_month, DateTime to_month)
        {
            DataTable dt = new DataTable();
            using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
            {
                string qry = @"SELECT * FROM InvestmentTransactions a 
                        WHERE a.[date] BETWEEN @start AND @end ";
                if (asset == "Treasuries")
                {
                    qry += "AND asset_symbol LIKE '912%' ";
                }
                else
                {
                    qry += "AND asset_symbol = @asset ";
                }
                if (selected_holder != "Home")
                {
                    qry += " AND holder = '" + selected_holder + "'";
                }
                qry += " ORDER BY [date]";
                using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                    {
                        adapter.SelectCommand = cmd;
                        adapter.SelectCommand.Parameters.AddWithValue("@asset", asset);
                        adapter.SelectCommand.Parameters.AddWithValue("@start", from_month.ToString("yyyy-MM-dd"));
                        adapter.SelectCommand.Parameters.AddWithValue("@end", to_month.ToString("yyyy-MM-dd"));
                        adapter.Fill(dt);
                    }
                }
            }
            return dt;

        }
    }

    public class InvestmentChange : ITransaction
    {
        public DateTime Date { get; set; }
        public string Category { get; set; }
        public string Description {
            get
            {
                return "Change in Investment Value (price and income)";
            }
            set
            {
                Description = "Change in Investment Value (price and income)";
            }
        }
        public double Amount { get; set; }
        public string Holder { get; set; }
        public string Bank { get; set; }
        public AccountType AccountType { get; set; } = AccountType.Brokerage;

        public void Save()
        {
            if (Date != null && Amount != 0 && Description != null)
            {
                string _query = "INSERT INTO [Entries] (date,amount,description,holder,bank) SELECT @date, @amount, @description, @holder, @bank ";
                _query += " WHERE NOT EXISTS (SELECT * FROM [Entries] WHERE date=@date and amount=@amount and description=@description and holder=@holder and bank=@bank); ";

                string bank_name = Bank;
                switch (AccountType)
                {
                    case AccountType.Savings:
                        bank_name += "_savings"; break;
                    case AccountType.Checking:
                        bank_name += "_checking"; break;
                    default:
                        bank_name += "_broker"; break;
                }

                using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
                {
                    using (SQLiteCommand comm = new SQLiteCommand(_query, conn))
                    {
                        comm.Parameters.AddWithValue("@date", Date.ToString("yyyy-MM-dd"));
                        comm.Parameters.Add("@amount", DbType.Double).Value = Amount;
                        comm.Parameters.Add("@description", DbType.String, 500).Value = Description;
                        comm.Parameters.Add("@holder", DbType.String, 50).Value = Holder;
                        comm.Parameters.Add("@bank", DbType.String, 50).Value = bank_name;

                        conn.Open();
                        comm.ExecuteNonQuery();
                        conn.Close();
                    }
                }
            }
        }

        public static DataTable GetHistoricalGains(string datatype, DateTime from_month, DateTime to_month)
        {
            DataTable dt = new DataTable();
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
                {
                    string qry = "";
                    if (datatype == "Cumulative")
                    {
                        qry = @"SELECT DISTINCT [date], holder, SUM(a.amount) OVER (PARTITION BY holder ORDER BY a.[date]) AS amount
                        FROM Statements a WHERE a.category = 'Investment Gains' AND a.[date] BETWEEN date(@start) and date(@end)
                        UNION SELECT DISTINCT [date], 'Home', SUM(a.amount) OVER (ORDER BY a.[date]) AS amount
                        FROM Statements a WHERE a.category = 'Investment Gains' AND a.[date] BETWEEN date(@start) and date(@end)
                        ORDER BY [date]";
                    }
                    else if (datatype == "Percentage")
                    {
                        qry = @";WITH t ([date], holder, gain, ending_mkt_value) as
                        (SELECT a.[date] as [date], b.holder, SUM(a.amount) as gain, ending_mkt_value
                        FROM EndMarketValues b LEFT JOIN Statements a ON date(a.[date]) = date(b.[date],'start of month','+2 month','-1 day') and a.holder = b.holder
                        WHERE a.category = 'Investment Gains' AND a.[date] BETWEEN @start and @end 
                        GROUP BY a.[date], b.holder, ending_mkt_value )
                        SELECT [date], holder, gain/ending_mkt_value as amount FROM t
                        UNION SELECT [date], 'Home', SUM(gain)/sum(ending_mkt_value) FROM t GROUP BY [date] ORDER BY [date]";
                    }
                    else
                    {
                        qry = @"SELECT [date], holder, SUM(amount) AS amount
                        FROM Statements WHERE category = 'Investment Gains' AND [date] BETWEEN date(@start) and date(@end)
                        GROUP BY [date], holder
                        UNION SELECT [date], 'Home', SUM(amount) AS amount
                        FROM Statements WHERE category = 'Investment Gains' AND [date] BETWEEN date(@start) and date(@end)
                        GROUP BY [date] ORDER BY [date]";
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
    }

    public class Salary : ITransaction
    {
        public DateTime Date { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public double Amount { get; set; }
        public string Holder { get; set; }
        public string Bank { get; set; }

        public string Get(bool default_salary)
        {
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry = "SELECT gross_salary FROM GrossSalary ";
                if (default_salary)
                {
                    qry += @" INNER JOIN (SELECT holder, max(date) as maxdate from GrossSalary GROUP BY holder) a 
                        ON GrossSalary.holder = a.holder and GrossSalary.date = a.maxdate ";
                    qry += " WHERE GrossSalary.holder = @holder" + (default_salary ? "" : " and date = @date");
                }
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    if (!default_salary)
                    {
                        cmd.Parameters.AddWithValue("@date", Date.ToString("yyyy-MM-dd"));
                    }
                    cmd.Parameters.Add("@holder", DbType.String, 50).Value = Holder;
                    conn.Open();
                    return Convert.ToString(cmd.ExecuteScalar());
                }
            }
        }

        public Tuple<DateTime, DateTime> GetDates()
        {
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry = "select date(min(date), 'start of month') as start_date, date(max(date), 'start of month') as end_date";
                qry += " from (select date from GrossSalary where holder = @holder UNION select date from entries where holder = @holder) ";
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    cmd.Parameters.Add("@holder", DbType.String, 50).Value = Holder;
                    conn.Open();
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.HasRows && reader.GetValue(reader.GetOrdinal("start_date")).ToString() != "")
                            {
                                return new Tuple<DateTime, DateTime>(reader.GetDateTime(reader.GetOrdinal("start_date")), reader.GetDateTime(reader.GetOrdinal("end_date")));
                            }
                        }
                    }
                    conn.Close();
                }
            }
            return null;
        }

        public void Delete()
        {
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry_del = "DELETE FROM GrossSalary ";
                qry_del += " WHERE date = @date and holder = @holder";
                using (SQLiteCommand cmd_del = new SQLiteCommand(qry_del, conn))
                {
                    cmd_del.Parameters.AddWithValue("@date", Date.ToString("yyyy-MM-dd"));
                    cmd_del.Parameters.Add("@holder", DbType.String, 50).Value = Holder;
                    conn.Open();
                    cmd_del.ExecuteNonQuery();
                }
            }
        }
        public void Save()
        {
            if (Date != null && Amount != 0 && Holder != null)
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
                {
                    string qry = "INSERT INTO GrossSalary SELECT @date, @holder, @gross_salary ";
                    qry += " WHERE NOT EXISTS (SELECT 1 FROM GrossSalary WHERE date = @date and holder = @holder) ";
                    using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                    {
                        cmd.Parameters.AddWithValue("@date", Date.ToString("yyyy-MM-dd"));
                        cmd.Parameters.Add("@holder", DbType.String, 50).Value = Holder;
                        cmd.Parameters.Add("@gross_salary", DbType.Double).Value = Amount;
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
