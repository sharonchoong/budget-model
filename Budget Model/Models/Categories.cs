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
    class BudgetCategory: ICategory
    {
        public string Category { get; set; }
        public string Keyword { get; set; }
        public string CustomCategory { get; set; }
        public string CategoryOrder { get; set; }

        public static IEnumerable<string> GetCategories(bool withorder = false)
        {
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry = "select category from Categories" + (withorder == true ? " ORDER BY category_order " : "");
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            yield return reader.GetString(reader.GetOrdinal("category"));
                        }
                        conn.Close();
                    }
                }
            }
        }

        public void UpdateDefinition(string old_keyword, bool is_insert)
        {
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry = "DELETE FROM Definitions WHERE keyword = @keyword";
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    cmd.Parameters.Add("@keyword", DbType.String, 100).Value = old_keyword;
                    conn.Open();
                    cmd.ExecuteNonQuery();

                    if (is_insert)
                    {
                        qry = "INSERT INTO Definitions (category, keyword, id) SELECT @category, @keyword, Entries.id FROM Entries WHERE description LIKE "
                            + (Keyword.StartsWith("~") ? "" : "'%' ||") + " @keyword " + (Keyword.EndsWith("~") ? "" : "|| '%'");
                        cmd.CommandText = qry;
                        cmd.Parameters.Add("@category", DbType.String, 100).Value = Category;
                        cmd.Parameters.Add("@keyword", DbType.String, 100).Value = Keyword.Replace("~","");
                        cmd.ExecuteNonQuery();
                    }

                    conn.Close();
                }
            }
        }

        public void CategoryOverride(int id, bool is_insert)
        {
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry = "DELETE FROM Customcategory WHERE id = @id";
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    cmd.Parameters.Add("@id", DbType.Int32).Value = id;
                    conn.Open();
                    cmd.ExecuteNonQuery();

                    if (is_insert)
                    {
                        qry = "INSERT INTO Customcategory (id, custom_category) VALUES(@id, @category)";
                        cmd.CommandText = qry;
                        cmd.Parameters.Add("@custom_category", DbType.String, 50).Value = CustomCategory;
                        cmd.ExecuteNonQuery();
                    }

                    conn.Close();
                }
            }
        }
    }

    class InvestmentCategory : ICategory
    {
        public string Category { get; set; }
        public string Keyword { get; set; }
        public string CategoryOrder { get; set; }

        public void UpdateDefinition(string old_keyword, bool is_insert)
        {
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry = "DELETE FROM InvestmentDefinitions WHERE asset_symbol = @asset_symbol";
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    cmd.Parameters.Add("@asset_symbol", DbType.String, 100).Value = old_keyword;
                    conn.Open();
                    cmd.ExecuteNonQuery();

                    if (is_insert)
                    {
                        qry = "INSERT INTO InvestmentDefinitions (category, asset_symbol) VALUES(@category, @asset_symbol)";
                        cmd.CommandText = qry;
                        cmd.Parameters.Add("@category", DbType.String, 100).Value = Category;
                        cmd.Parameters.Add("@asset_symbol", DbType.String, 100).Value = Keyword;
                        cmd.ExecuteNonQuery();
                    }

                    conn.Close();
                }
            }
        }

        public static IEnumerable<string> Getcategories()
        {
            using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
            {
                string qry;
                qry = @"SELECT category FROM InvestmentCategories ORDER BY category_order";
                using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                {
                    conn.Open();
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        // Call Read before accessing data.
                        while (reader.Read())
                        {
                            yield return reader.GetString(reader.GetOrdinal("category"));
                        }

                        // Call Close when done reading.
                        reader.Close();
                        conn.Close();
                    }
                }
            }
        }
    }
}
