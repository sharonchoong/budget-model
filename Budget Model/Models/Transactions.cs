﻿using System;
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
                _query += " WHERE NOT EXISTS (SELECT * FROM [Entries] WHERE date(date)=date(@date) and amount=@amount and description=@description and bank=@bank";
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

        public void Delete(int id)
        {
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry = "DELETE FROM Entries WHERE id = @id";
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    cmd.Parameters.Add("@id", DbType.Int32).Value = id;
                    conn.Open();
                    cmd.ExecuteNonQuery();
                    conn.Close();
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
        public double Fees { get; set; }
        public double Amount
        {
            get
            {
                return Price * Quantity + Fees;
            }
            set
            {
                Price = value / Quantity;
            }
        }
        public string LocalCurrencySymbol { get; set; }
        public double LocalCurrencyPrice { get; set; }
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

        public BrokerageTransaction(string local_currency_symbol, double local_currency_price, double quantity, double fees, string symbol)
        {
            LocalCurrencyPrice = local_currency_price;
            LocalCurrencySymbol = local_currency_symbol;
            Quantity = quantity;
            Fees = fees;
            Symbol = symbol;
        }

        public BrokerageTransaction(double price, double quantity, double fees, string symbol)
        {
            Price = price;
            Quantity = quantity;
            Fees = fees;
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
            if (Date != null && (Price != 0 || (LocalCurrencyPrice != 0 && LocalCurrencySymbol != null)) && Description != null && Symbol != null)
            {
                string _query = "INSERT INTO [InvestmentTransactions] ";
                _query += "(asset_symbol,date,transaction_description,holder,bank,price,quantity, fees, local_currency_price, local_currency_symbol, maturity, coupon_rate, yield_to_maturity) ";
                _query += "SELECT @symbol, @date, @description, @holder, @bank, @price, @quantity, @fees, @local_currency_price, @local_currency_symbol, @maturity, @coupon, @yield ";
                _query += " WHERE NOT EXISTS (SELECT * FROM [InvestmentTransactions] WHERE date(date)=date(@date) and asset_symbol=@symbol ";
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
                        if (LocalCurrencySymbol != null)
                        {
                            double exchange_rate = CurrencyTransaction.GetMostRecentExchangeRate(LocalCurrencySymbol, Bank + "_broker", Date);
                            comm.Parameters.Add("@price", DbType.Double).Value = LocalCurrencyPrice / exchange_rate;
                        }
                        else
                            comm.Parameters.Add("@price", DbType.Double).Value = Price;
                        comm.Parameters.Add("@fees", DbType.Double).Value = Fees;
                        comm.Parameters.Add("@local_currency_price", DbType.Double).Value = LocalCurrencyPrice;
                        comm.Parameters.Add("@local_currency_symbol", DbType.String).Value = LocalCurrencySymbol;
                        comm.Parameters.Add("@quantity", DbType.Double).Value = Quantity;
                        if (MaturityDate != null)
                        {
                            comm.Parameters.AddWithValue("@maturity", ((DateTime)MaturityDate).ToString("yyyy-MM-dd"));
                        }

                        else
                        {
                            comm.Parameters.AddWithValue("@maturity", DBNull.Value);
                        }
                        comm.Parameters.Add("@coupon", DbType.Double).Value = (object)CouponRate ?? DBNull.Value;
                        comm.Parameters.Add("@yield", DbType.Double).Value = (object)YieldToMaturity ?? DBNull.Value;

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
                string qry = @"SELECT *, (price * quantity) + COALESCE(fees, 0) as mkt_value FROM InvestmentTransactions a  
                        WHERE date(a.[date]) BETWEEN date(@start) AND date(@end) ";
                if (asset == "Treasuries")
                {
                    qry += "AND asset_symbol LIKE '912%' ";
                }
                else if (asset != null)
                {
                    qry += "AND asset_symbol = @asset ";
                }
                if (selected_holder != "Home")
                {
                    qry += " AND holder = '" + selected_holder + "'";
                }
                qry += " ORDER BY date([date])";
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

    public class CurrencyTransaction : ITransaction
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
        public string LocalCurrencySymbol { get; set; }
        public CurrencyTransaction(double price, double quantity, string currency_symbol)
        {
            Price = price;
            Quantity = quantity;
            LocalCurrencySymbol = currency_symbol;
        }

        public void Save()
        {
            if (Date != null && Price != 0 && Description != null && LocalCurrencySymbol != null)
            {
                string _query = "INSERT INTO [InvestmentTransactions] ";
                _query += "(date,transaction_description,holder,bank,price,quantity, local_currency_price, local_currency_symbol, asset_symbol) ";
                _query += "SELECT @date, @description, @holder, @bank, @price, @quantity, 1, @local_currency_symbol, @local_currency_symbol ";
                _query += " WHERE NOT EXISTS (SELECT * FROM [InvestmentTransactions] WHERE date(date)=date(@date) and local_currency_symbol=@local_currency_symbol ";
                _query += " and transaction_description=@description and holder=@holder and bank=@bank); ";

                using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
                {
                    using (SQLiteCommand comm = new SQLiteCommand(_query, conn))
                    {
                        comm.Parameters.Add("@local_currency_symbol", DbType.String, 250).Value = LocalCurrencySymbol;
                        comm.Parameters.AddWithValue("@date", Date.ToString("yyyy-MM-dd"));
                        comm.Parameters.Add("@description", DbType.String, 500).Value = Description;
                        comm.Parameters.Add("@holder", DbType.String, 50).Value = Holder;
                        comm.Parameters.Add("@bank", DbType.String, 50).Value = Bank + "_broker";
                        comm.Parameters.Add("@price", DbType.Double).Value = Price;
                        comm.Parameters.Add("@quantity", DbType.Double).Value = Quantity;

                        conn.Open();
                        comm.ExecuteNonQuery();
                        conn.Close();
                    }
                }
            }
        }

        public static double GetMostRecentExchangeRate(string currency_symbol, string bank, DateTime? date = null)
        {
            double exchange_rate = 0;
            using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
            {
                string qry = @"SELECT price FROM InvestmentTransactions WHERE local_currency_symbol = @currency_symbol AND asset_symbol = @currency_symbol AND bank = @bank ";
                if (date.HasValue)
                    qry += " ORDER BY abs(strftime('%s',date(date)) - strftime('%s', date(@date))) ";
                else
                    qry += " ORDER BY date([date]) DESC";
                qry += " LIMIT 1";
                using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                    {
                        cmd.Parameters.Add("@bank", DbType.String, 50).Value = bank;
                        cmd.Parameters.Add("@currency_symbol", DbType.String, 250).Value = currency_symbol;
                        if (date.HasValue)
                            cmd.Parameters.AddWithValue("@date", date.Value.ToString("yyyy-MM-dd"));
                        conn.Open();
                        exchange_rate = Convert.ToDouble(cmd.ExecuteScalar());
                        conn.Close();
                    }
                }
            }
            return exchange_rate;
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
                _query += " WHERE NOT EXISTS (SELECT * FROM [Entries] WHERE date(date)=date(@date) and amount=@amount and description=@description and holder=@holder and bank=@bank); ";

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
                        qry = @"SELECT DISTINCT [date], holder, SUM(a.amount) OVER (PARTITION BY holder ORDER BY date(a.[date])) AS amount
                        FROM (SELECT [date], holder, sum(amount) as amount FROM Statements WHERE category = 'Investment Gains' GROUP BY date(date), holder) a 
                        WHERE date(a.[date]) BETWEEN date(@start) and date(@end)
                        UNION SELECT DISTINCT date([date]) as [date], 'Home', SUM(a.amount) OVER (ORDER BY a.[date]) AS amount
                        FROM (SELECT [date], sum(amount) as amount FROM Statements a WHERE category = 'Investment Gains' GROUP BY date(date)) a 
                        WHERE date(a.[date]) BETWEEN date(@start) and date(@end)
                        ORDER BY date([date])";
                    }
                    else if (datatype == "Percentage" || datatype == "Cumulative Percentage")
                    {
                        qry = @";WITH t as
                        (SELECT a.[date], b.holder, SUM(a.amount) as gain, ending_mkt_value + RunningBalance as ending_mkt_value
                        FROM EndMarketValues b LEFT JOIN MonthlyBalance c ON DATE(c.[period]) = DATE(b.[date]) and c.holder = b.holder
                        LEFT JOIN Statements a ON DATE(a.[date]) = DATE(b.[date],'start of month','+2 month','-1 day') and a.holder = b.holder
                        WHERE a.category = 'Investment Gains' AND DATE(a.[date]) BETWEEN DATE(@start) and DATE(@end) 
                        GROUP BY DATE(a.[date]), b.holder, ending_mkt_value )
                        SELECT [date], holder, gain/ending_mkt_value as amount FROM t
                        UNION SELECT date([date]), 'Home', SUM(gain)/sum(ending_mkt_value) FROM t GROUP BY DATE([date]) ORDER BY DATE([date])";
                    }
                    else
                    {
                        qry = @"SELECT [date], holder, SUM(amount) AS amount
                        FROM Statements WHERE category = 'Investment Gains' AND date([date]) BETWEEN date(@start) and date(@end)
                        GROUP BY date([date]), holder
                        UNION SELECT date([date]) as [date], 'Home', SUM(amount) AS amount
                        FROM Statements WHERE category = 'Investment Gains' AND date([date]) BETWEEN date(@start) and date(@end)
                        GROUP BY date([date]) ORDER BY date([date])";
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
            if (datatype == "Cumulative Percentage")
            {
                dt = dt.AsEnumerable().OrderBy(r => r["holder"]).ThenBy(r => r["date"]).CopyToDataTable();
                List<DataRow> rows = new List<DataRow>();
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    double return_pct = Convert.ToDouble(dt.Rows[i]["amount"]);
                    bool is_beginning = i == 0 || dt.Rows[i]["holder"].ToString() != dt.Rows[i - 1]["holder"].ToString();
                    if (is_beginning)
                    {
                        DataRow row = dt.NewRow();
                        row["date"] = Convert.ToDateTime(dt.Rows[i]["date"]).AddMonths(-1);
                        row["holder"] = dt.Rows[i]["holder"].ToString();
                        row["amount"] = 1;
                        rows.Add(row);
                    }
                    dt.Rows[i]["amount"] = (1+return_pct) * (is_beginning ? 1: dt.Rows[i - 1].Field<double>("amount"));
                }
                foreach (DataRow row in rows)
                {
                    dt.Rows.Add(row);
                }
                dt = dt.AsEnumerable().OrderBy(r => r["holder"]).ThenBy(r => r["date"]).CopyToDataTable();
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
                    qry += @" INNER JOIN (SELECT holder, max(date(date)) as maxdate from GrossSalary GROUP BY holder) a 
                        ON GrossSalary.holder = a.holder and date(GrossSalary.date) = date(a.maxdate) ";
                }
                qry += " WHERE GrossSalary.holder = @holder";
                if (!default_salary)
                {
                    qry += " and date(date) = date(@date)";
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
                string qry = "select date(min(date(date)), 'start of month') as start_date, date(max(date(date)), 'start of month') as end_date";
                qry += " from (select date(date) as date from GrossSalary where holder = @holder UNION select date(date) from entries where holder = @holder) ";
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
                qry_del += " WHERE date(date) = date(@date) and holder = @holder";
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
                    qry += " WHERE NOT EXISTS (SELECT 1 FROM GrossSalary WHERE date(date)=date(@date) and holder = @holder) ";
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
