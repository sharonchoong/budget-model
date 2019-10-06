using CsvHelper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using System.Net;
using System.Text.RegularExpressions;
using Budget_Model.Models;
using Budget_Model.Helpers;
using System.Collections.ObjectModel;
using System.Data.SQLite;

namespace Budget_Model
{
    /// <summary>
    /// Interaction logic for DataDefinitions.xaml
    /// </summary>
    public partial class DataDefinitions : Page
    {
        public Tuple<int, string> old_keyword;
        public IEnumerable<Holder> HoldersItems { get; set; }
        public IEnumerable<FinancialInstitution> FinancialInstitutions { get; set; }
        public IEnumerable<Account> Accounts { get; set; }
        public IEnumerable<ReportFormat> ReportFormats { get; set; }

        public DataDefinitions()
        {
            InitializeComponent();

            Holder holder = new Holder();
            HoldersItems = holder.HolderCollection();
            HoldersRadio.DataContext = this;

            FinancialInstitution financialinstitutions = new FinancialInstitution();
            FinancialInstitutions = financialinstitutions.GetFinancialInstitutions();
            Accounts = FinancialInstitutions.First().Accounts;
            if (!Accounts.Any(r => r.IsChecked == true))
                Accounts.First().IsChecked = true;
            ReportFormats = Accounts.First().ExcelImportFormats;
            if (!ReportFormats.Any(r => r.IsChecked == true))
                ReportFormats.First().IsChecked = true;

            comboBank.DataContext = this;
            comboBank.SelectedIndex = 0;

            AccountRadio.DataContext = this;

            comboFormat.DataContext = this;
            comboFormat.SelectedIndex = 0;

            comboDate.SelectedIndex = 0;
            Holders.ItemsSource = holder.GetHolders;
            Categories.ItemsSource = BudgetCategory.GetCategories();
            CustomCategories.ItemsSource = BudgetCategory.GetCategories().Concat(new[] { "" });

            SetSalaryDatepicker();
            DisplayDefaultSalary();

            DataGridDefinitions.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
            UpdateDataGrid();
        }

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".csv";
            dlg.Filter = "Excel Files (*.xls;*.xlsx;*.csv)|*.xls;*.xlsx;*.csv";

            // Display OpenFileDialog by calling ShowDialog method 
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                // Open document 
                string filename = dlg.FileName;
                if (Path.GetExtension(filename) == ".csv" || Path.GetExtension(filename) == ".CSV")
                {
                    ReportFormat bank = ReportFormats.Where(r => r.IsChecked == true).First();
                    string holder = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
                    ExcelImport.ImportFromExcel(filename, bank, holder, date_statement.SelectedDate);
                    UpdateSalaries();
                    UpdateDataGrid();
                }
            }
        }

        private void GridBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            //store old keyword before edit
            DataGridRow row = (DataGridRow)DataGridDefinitions.ItemContainerGenerator.ContainerFromItem(DataGridDefinitions.CurrentItem);
            DataRowView row_items = (DataRowView)row.Item;
            string keyword = row_items.Row.ItemArray[6].ToString();
            old_keyword = new Tuple<int, string>(row.GetIndex(), keyword);
        }

        private bool isManualEditCommit;
        private void GridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (!isManualEditCommit)
            {
                isManualEditCommit = true;
                DataGridRow row = e.Row;
                DataRowView row_items = (DataRowView)row.Item;
                int row_index = row.GetIndex();
                BudgetCategory new_category = new BudgetCategory();
                if (e.Column.Header.ToString() == "Keyword" && old_keyword.Item1 == row_index && !string.IsNullOrWhiteSpace(row_items.Row.ItemArray[5].ToString()))
                {
                    TextBox t = e.EditingElement as TextBox;
                    new_category.Category = row_items.Row.ItemArray[5].ToString();
                    new_category.Keyword = t.Text.ToString();
                    new_category.UpdateDefinition(old_keyword.Item2, row_items.Row.ItemArray[5] != DBNull.Value && !string.IsNullOrWhiteSpace(t.Text.ToString()));

                    UpdateDataGrid();
                }
                else if (e.Column.Header.ToString() == "Category" && !string.IsNullOrWhiteSpace(row_items.Row.ItemArray[6].ToString()))
                {
                    string keyword = row_items.Row.ItemArray[6].ToString();
                    ComboBox cb = e.EditingElement as ComboBox;
                    string t = Convert.ToString(cb.SelectedItem);

                    new_category.Category = t;
                    new_category.Keyword = keyword;
                    new_category.UpdateDefinition(keyword, row_items.Row.ItemArray[6] != DBNull.Value && !string.IsNullOrWhiteSpace(t));

                    UpdateDataGrid();
                }
                else if (e.Column.Header.ToString() == "Category Override"
                    && !string.IsNullOrWhiteSpace(row_items.Row.ItemArray[5].ToString()) && !string.IsNullOrWhiteSpace(row_items.Row.ItemArray[6].ToString()))
                {
                    string keyword = row_items.Row.ItemArray[6].ToString();
                    ComboBox cb = e.EditingElement as ComboBox;
                    string t = Convert.ToString(cb.SelectedItem);

                    int id = Convert.ToInt32(row_items.Row.ItemArray[0]);
                    new_category.CustomCategory = t;
                    new_category.CategoryOverride(id, !string.IsNullOrWhiteSpace(t));

                    UpdateDataGrid();
                }
                else if (e.Column.Header.ToString() == "Holder"
                    && !string.IsNullOrWhiteSpace(row_items.Row.ItemArray[5].ToString()) && !string.IsNullOrWhiteSpace(row_items.Row.ItemArray[6].ToString()))
                {
                    ComboBox cb = e.EditingElement as ComboBox;
                    string t = Convert.ToString(cb.SelectedItem);

                    BankTransaction new_transaction = new BankTransaction();
                    new_transaction.Holder = t;
                    new_transaction.ChangeHolder(Convert.ToInt32(row_items.Row.ItemArray[0]));
                    UpdateDataGrid();
                }
                else if (e.Column.Header.ToString() == "Category Override")
                {
                    UpdateDataGrid();
                }
                else
                {
                    DataGridDefinitions.CommitEdit();
                    DataGridDefinitions.CommitEdit();
                }
                isManualEditCommit = false;
            }
        }

        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (DataGridDefinitions.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                for (int i = 0; i < DataGridDefinitions.Items.Count; i++)
                {
                    var row = (DataGridRow)DataGridDefinitions.ItemContainerGenerator.ContainerFromIndex(i);
                    DataRowView rowview = (DataRowView)DataGridDefinitions.Items[i];
                    if (row != null)
                    {
                        if (rowview["category_sort"].ToString() != "0" && rowview["totalCount"].ToString() == "1")
                            row.Background = new SolidColorBrush(Colors.White);
                        else if (rowview["category_sort"].ToString() == "0")
                            row.Background = new SolidColorBrush(Colors.LightPink);
                        else if (rowview["totalCount"].ToString() != "1")
                            row.Background = new SolidColorBrush(Colors.DarkSalmon);
                    }
                }
            }
        }


        private void SelectionChanged(object sender, EventArgs e)
        {
            UpdateDataGrid();
            SetSalaryDatepicker();
            DisplayDefaultSalary();
        }

        private void BankChanged(object sender, EventArgs e)
        {
            if (FinancialInstitutions.Where(r => r.IsChecked == true).Count() > 0)
            {
                Accounts = FinancialInstitutions.Where(r => r.IsChecked == true).First().Accounts;
                if (!Accounts.Any(r => r.IsChecked == true))
                    Accounts.First().IsChecked = true;
                AccountRadio.ItemsSource = Accounts;

                AccountTypeChecked();
            }
        }
        private void AccountTypeChecked(object sender = null, EventArgs e = null)
        {
            ReportFormats = Accounts.Where(r => r.IsChecked == true).First().ExcelImportFormats;
            if (!ReportFormats.Any(r => r.IsChecked == true))
                ReportFormats.First().IsChecked = true;
            comboFormat.ItemsSource = ReportFormats;
            comboFormat.SelectedIndex = 0;

            //visibility of brokerage statement date picker selection
            FormatChanged();
        }
        private void FormatChanged(object sender = null, EventArgs e = null)
        {
            //visibility of brokerage statement date picker selection
            StatementText.Visibility = Visibility.Collapsed;
            date_statement.Visibility = Visibility.Collapsed;
            if (Accounts.Where(r => r.IsChecked == true).First().AccountType == AccountType.Brokerage)
            {
                if (ReportFormats.Where(r => r.IsChecked == true).First() is StatementReportFormat && ((StatementReportFormat)ReportFormats.Where(r => r.IsChecked == true).First()).NeedsStatementDate == true)
                {
                    StatementText.Visibility = Visibility.Visible;
                    date_statement.Visibility = Visibility.Visible;
                }
            }
        }

        private void DataGridChanged(object sender, EventArgs e)
        {
            UpdateDataGrid();
        }

        public void UpdateDataGrid()
        {
            ComboBoxItem selected_date = (ComboBoxItem)(comboDate.SelectedItem);
            if (comboBank != null && selected_date.Content != null)
            {
                DataTable dt = new DataTable();
                using (SQLiteConnection conn = new SQLiteConnection(ConfigurationManager.ConnectionStrings["BudgetDataConnectionString"].ConnectionString))
                {
                    using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
                    {
                        string subquery_date = "";
                        switch (selected_date.Content.ToString())
                        {
                            case "Last 3 Months":
                                subquery_date = " WHERE date between DATE('now', 'start of month', '-3 month') AND DATE('now') OR category IS NULL OR totalCount > 1 "; break;
                            case "Last 6 Months":
                                subquery_date = " WHERE date between DATE('now', 'start of month', '-6 month') AND DATE('now') OR category IS NULL OR totalCount > 1 "; break;
                            case "Last Year":
                                subquery_date = " WHERE date between DATE('now', 'start of month', '-12 month') AND DATE('now') OR category IS NULL OR totalCount > 1 "; break;
                            case "Last 2 Years":
                                subquery_date = " WHERE date between DATE('now', 'start of month', '-24 month') AND DATE('now') OR category IS NULL OR totalCount > 1 "; break;
                            default:
                                break;
                        }

                        string qry = "select *, COALESCE(category_presort, 1) as category_sort from Statements ";
                        qry += " LEFT JOIN (SELECT Id as id2, COUNT(1) totalCount FROM Statements GROUP BY Id) a on id2 = id ";
                        qry += " LEFT JOIN (SELECT Id as id3, 0 as category_presort FROM Statements WHERE category IS NULL) b on id3 = id ";
                        qry += subquery_date;
                        qry += " order by totalCount desc, category_sort asc, date desc";
                        using (SQLiteCommand cmd = new SQLiteCommand(qry, conn))
                        {
                            adapter.SelectCommand = cmd;
                            adapter.Fill(dt);
                            DataGridDefinitions.CommitEdit();
                            DataGridDefinitions.CommitEdit();
                            DataGridDefinitions.DataContext = dt;
                        }
                    }
                }
            }
        }

        private void SalaryDate_SelectedDateChanged(object sender, EventArgs e)
        {
            Salary current_salary = new Salary();
            current_salary.Date = new DateTime(Convert.ToDateTime(date_month.SelectedDate).Year, Convert.ToDateTime(date_month.SelectedDate).Month, 1);
            current_salary.Holder = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
            txt_gross_salary.Text = current_salary.Get(false);
        }

        private void SalaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txt_gross_salary.Text) && date_month.SelectedDate != null)
            {
                Salary new_salary = new Salary();
                new_salary.Date = new DateTime(Convert.ToDateTime(date_month.SelectedDate).Year, Convert.ToDateTime(date_month.SelectedDate).Month, 1);
                new_salary.Holder = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
                new_salary.Amount = Math.Round(Convert.ToDouble(txt_gross_salary.Text), 2);
                new_salary.Delete();
                new_salary.Save();
                SetSalaryDatepicker();
                DisplayDefaultSalary();
            }
        }

        public void SetSalaryDatepicker()
        {
            if (HoldersItems != null)
            {
                Salary currentsalary = new Salary();
                currentsalary.Holder = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
                var salary_dates = currentsalary.GetDates();
                if (salary_dates != null)
                {
                    date_month.DisplayDateStart = salary_dates.Item1;
                    date_month.DisplayDateEnd = salary_dates.Item2;
                }
            }
        }
        public void DisplayDefaultSalary()
        {
            if (HoldersItems != null)
            {
                Salary current_salary = new Salary();
                current_salary.Holder = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
                default_gross_salary.Text = current_salary.Get(true);
                if (default_gross_salary.Text == "")
                    default_gross_salary.Text = "0";
            }
        }

        public void UpdateSalaries()
        {
            Salary new_salary = new Salary();
            new_salary.Holder = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
            new_salary.Amount = Convert.ToDouble(default_gross_salary.Text);
            var salary_dates = new_salary.GetDates();
            if (salary_dates != null)
            {
                DateTime start_date = salary_dates.Item1;
                int n_months = ((salary_dates.Item2.Year - start_date.Year) * 12) + salary_dates.Item2.Month - start_date.Month + 1;

                for (int i = 1; i <= n_months; i++)
                {
                    new_salary.Date = start_date;
                    new_salary.Save();
                    start_date = start_date.AddMonths(1);
                }
            }
        }


    }
}
