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
    public class BrokerageAsset : IAsset
    {
        public DateTime AsOf { get; set; }
        public string Description { get; set; }
        public double Value { get; set; }
        public string Holder { get; set; }
        public string Bank { get; set; }
        public string Symbol { get; set; }

        public BrokerageAsset()
        {
            Symbol = "";
        }
        public BrokerageAsset(string symbol)
        {
            Symbol = symbol;
        }

        public void Save()
        {
            if (AsOf != null && Value != 0 && Description != null)
            {
                string _query = "INSERT INTO [FinancialAssets] ([date], asset_symbol, asset_description, ending_mkt_value, holder, bank) SELECT @date, ";
                _query += "@symbol, @description, @value, @holder, @bank ";
                _query += "WHERE NOT EXISTS (SELECT * FROM [FinancialAssets] WHERE date=@date and ending_mkt_value=@value";
                _query += " and asset_symbol=@symbol and holder=@holder and bank=@bank);";

                using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
                {
                    using (SQLiteCommand comm = new SQLiteCommand(_query, conn))
                    {
                        comm.Parameters.AddWithValue("@date", AsOf.ToString("yyyy-MM-dd"));
                        comm.Parameters.Add("@symbol", DbType.String, 250).Value = Symbol;
                        comm.Parameters.Add("@description", DbType.String, 500).Value = Description;
                        comm.Parameters.Add("@value", DbType.Double).Value = Value;
                        comm.Parameters.Add("@holder", DbType.String, 50).Value = Holder;
                        comm.Parameters.Add("@bank", DbType.String, 50).Value = Bank + "_broker";

                        conn.Open();
                        comm.ExecuteNonQuery();
                        conn.Close();
                    }
                }
            }
        }

        public static DataTable GetMonthInvestments(string selected_person, DateTime selected_date)
        {
            DataTable dt = new DataTable();
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
                {
                    string qry = "SELECT [date], asset_symbol, asset_description, category, holder, bank, ending_mkt_value, ";
                    qry += " case when category is not null then 1 else 0 end as category_sort ";
                    qry += " FROM Investments";
                    qry += " WHERE [date] = (SELECT MAX([date]) FROM Investments WHERE [date] <= date(@date, 'start of month','+1 month','-1 day')) ";
                    if (selected_person != "Home")
                    {
                        qry += " AND holder = '" + selected_person + "' ";
                    }
                    qry += " order by category_sort, category_order, ending_mkt_value DESC";
                    using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                    {
                        adapter.SelectCommand = cmd;
                        adapter.SelectCommand.Parameters.AddWithValue("@date", selected_date.ToString("yyyy-MM-dd"));
                        adapter.Fill(dt);
                    }
                }
            }
            return dt;
        }

        public static DataTable GetHistoricalInvestments(string selected_person, string specific_category, string asset_column, DateTime from_month, DateTime to_month)
        {
            DataTable dt = new DataTable();
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
                {
                    string qry = @"SELECT a.[date], " + (asset_column == null ? "" : asset_column + ", ") + @"
                        a.category, category_order, SUM(a.ending_mkt_value) as ending_mkt_value FROM Investments a 
                        WHERE a.[date] BETWEEN @start and @end ";
                    if (specific_category != null)
                    {
                        qry += " AND category = '" + specific_category + "'";
                    }
                    else
                    {
                        qry += " AND category IS NOT NULL ";
                    }

                    if (selected_person != "Home")
                    {
                        qry += " AND holder = '" + selected_person + "'";
                    }
                    qry += " GROUP by [date], " + (asset_column == null ? "" : asset_column + ", ") + "category, category_order ORDER BY [date], category_order";

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

        public static string GetFirstAsset()
        {
            string firstasset = "";
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry;
                qry = @"SELECT asset_symbol FROM InvestmentDefinitions a INNER JOIN InvestmentCategories b ON a.category = b.category ORDER BY category_order LIMIT 1";
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    conn.Open();
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        // Call Read before accessing data.
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                firstasset = reader.GetString(0);
                            }
                        }

                        // Call Close when done reading.
                        reader.Close();
                    }
                    conn.Close();
                }
            }
            return firstasset;
        }
    }
}
