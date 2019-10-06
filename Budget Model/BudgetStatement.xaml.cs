using Budget_Model.Models;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Budget_Model
{
    /// <summary>
    /// Interaction logic for BudgetStatement.xaml
    /// </summary>
    public partial class BudgetStatement : Page
    {
        public IEnumerable<Holder> HoldersItems { get; set; }

        public BudgetStatement()
        {
            InitializeComponent();

            HoldersItems = Helpers.Initializer.SetHolders(comboFor, this);

            date_month.SelectedDateChanged -= SelectionChanged;
            Helpers.Initializer.SetDates(date_month);
            date_month.SelectedDateChanged += SelectionChanged;

            Task.Factory.StartNew(() =>
            {
                GetAlert();
                ;
            });
            Task.Factory.StartNew(() =>
            {
                FillDataGrid(null);
            });
            Task.Factory.StartNew(() =>
            {
                GetStatementItems(true);
            });
            Task.Factory.StartNew(() =>
            {
                MakeExpenseChart();
            });
        }

        public void GetAlert()
        {
            int n_undefined = 0;
            int n_duplicates = 0;
            DateTime this_month = new DateTime();
            DateTime statement_month = new DateTime();
            Dispatcher.Invoke(() =>
            {
                this_month = new DateTime(Convert.ToDateTime(date_month.SelectedDate).Year, Convert.ToDateTime(date_month.SelectedDate).Month, 1);
                statement_month = (DateTime)date_month.SelectedDate;
            });
            Tuple<int, int> untreated_data = MonthlyStatement.CheckUndefinedOrDuplicates(this_month, statement_month);
            n_undefined = untreated_data.Item1;
            n_duplicates = untreated_data.Item2;

            Dispatcher.Invoke(() =>
            {
                if (n_undefined > 0 || n_duplicates > 0)
                {
                    alert_undefined.Foreground = Brushes.Red;
                    alert_undefined.FontWeight = FontWeights.Bold;
                    alert_undefined.Text = "Found "
                        + (n_undefined > 0 ? n_undefined.ToString() + " undefined items" : "")
                        + (n_undefined > 0 && n_duplicates > 0 ? " and " : "")
                        + (n_duplicates > 0 ? n_duplicates.ToString() + " duplicate items" : "");
                }
                else
                {
                    alert_undefined.Foreground = Brushes.Black;
                    alert_undefined.FontWeight = FontWeights.Normal;
                    alert_undefined.Text = "Data looks good";
                }

            });
        }

        public void FillDataGrid(string category)
        {
            //filling datagrid 
            string selected_person = "";
            DateTime statement_month = new DateTime();
            Dispatcher.Invoke(() =>
            {
                selected_person = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
                statement_month = (DateTime)date_month.SelectedDate;
            });
            DataTable dt = MonthlyStatement.GetTransactionsByCategory(category, statement_month, selected_person);
            Dispatcher.Invoke(() =>
            {
                DataGridLineItems.DataContext = dt;
            });
        }
        public void GetStatementItems()
        {
            GetStatementItems(false);
        }
        public void GetStatementItems(bool first)
        {
            //filling textblocks 
            MonthlyStatement statement = new MonthlyStatement();
            Dispatcher.Invoke(() =>
            {
                statement.statement_date = (DateTime)date_month.SelectedDate;
                statement.holder = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();

                Dictionary<string, double> category_amounts = statement.GetAggregateCategoryAmount();

                ///grid assets
                NetWorthGrid.Children.Clear();
                List<AccountType> acctypes = new List<AccountType>();
                FinancialInstitution financialinstitutions = new FinancialInstitution();
                int row = 1;
                foreach (FinancialInstitution fi in financialinstitutions.GetFinancialInstitutions().Where(e => e.Accounts.Any(a => a.AccountType != AccountType.CreditCard)))
                {
                    if (first)
                    {
                        NetWorthGrid.RowDefinitions.Add(new RowDefinition());
                    }
                    TextBlock txt_fi = Helpers.GridHelper.CreateTextInGrid(fi.InstitutionName, row, 0, false, HorizontalAlignment.Left, false, true, true);
                    NetWorthGrid.Children.Add(txt_fi);

                    foreach (Account acc in fi.Accounts)
                    {
                        if (acc.AccountType != AccountType.CreditCard)
                        {
                            if (!acctypes.Contains(acc.AccountType))
                            {
                                if (first)
                                {
                                    NetWorthGrid.ColumnDefinitions.Add(new ColumnDefinition());
                                }
                                TextBlock txt_acc = Helpers.GridHelper.CreateTextInGrid(acc.AccountTypeDescription, 0, acctypes.Count + 1, false, HorizontalAlignment.Center, true, false, true);
                                acctypes.Add(acc.AccountType);
                            }
                            double balance = statement.GetBalance(acc.AccountType, fi.ShortName);
                            TextBlock txt_data = Helpers.GridHelper.CreateTextInGrid(string.Format("{0:C2}", balance), row, acctypes.IndexOf(acc.AccountType) + 1, false, HorizontalAlignment.Right, false, false, true);
                            NetWorthGrid.Children.Add(txt_data);
                        }
                    }
                    row++;
                }
                if (first)
                {
                    NetWorthGrid.ColumnDefinitions.Add(new ColumnDefinition());
                }
                TextBlock txt_total_bank = Helpers.GridHelper.CreateTextInGrid("Total", 0, acctypes.Count + 1, false, HorizontalAlignment.Center, true, false, true);
                NetWorthGrid.Children.Add(txt_total_bank);
                for (int r = 1; r < row; r++ )
                {
                    double amount = 0;
                    for (int c = 1; c <= acctypes.Count; c++)
                    {
                        TextBlock selected_txt = NetWorthGrid.Children.Cast<TextBlock>().Where(e => Grid.GetColumn(e) == c && Grid.GetRow(e) == r).FirstOrDefault();
                        if (selected_txt != null)
                        {
                            amount += Convert.ToDouble(selected_txt.Text.Replace("$", "").Replace(",", ""));
                        }
                    }
                    txt_total_bank = Helpers.GridHelper.CreateTextInGrid(string.Format("{0:C2}", amount), r, acctypes.Count + 1, false, HorizontalAlignment.Right, true, false, true);
                    NetWorthGrid.Children.Add(txt_total_bank);
                }

                if (first)
                {
                    NetWorthGrid.RowDefinitions.Add(new RowDefinition());
                }
                TextBlock txt_total_accounttype = Helpers.GridHelper.CreateTextInGrid("Total", row, 0, false, HorizontalAlignment.Left, true, false, true);
                NetWorthGrid.Children.Add(txt_total_accounttype);
                for (int c = 1; c <= acctypes.Count + 1; c++)
                {
                    double amount = 0;
                    for (int r = 1; r < row; r++)
                    {
                        TextBlock selected_txt = NetWorthGrid.Children.Cast<TextBlock>().Where(e => Grid.GetColumn(e) == c && Grid.GetRow(e) == r).FirstOrDefault();
                        if (selected_txt != null)
                        {
                            amount += Convert.ToDouble(selected_txt.Text.Replace("$", "").Replace(",", ""));
                        }
                    }
                    txt_total_accounttype = Helpers.GridHelper.CreateTextInGrid(string.Format("{0:C2}", amount), row, c, false, HorizontalAlignment.Right, true, false, true);
                    NetWorthGrid.Children.Add(txt_total_accounttype);
                }
                
                ///grid statement
                amt_gross_salary.Text = string.Format("{0:C2}", category_amounts["Gross Salary"]);
                //amt_ret_contribution.Text = string.Format("{0:C2}", -1 * category_amounts["Retirement Contribution"]);
                amt_netpay.Text = string.Format("{0:C2}", category_amounts["Payroll"]);
                amt_other_withholding.Text = (-1 * (double.Parse(amt_gross_salary.Text, NumberStyles.Currency)
                    - double.Parse(amt_netpay.Text, NumberStyles.Currency)
                    + double.Parse(amt_ret_contribution.Text, NumberStyles.Currency))).ToString("C2");

                //amt_ret_income.Text = string.Format("{0:C2}", category_amounts["Retirement Income"]);
                amt_other_income.Text = string.Format("{0:C2}", category_amounts["Other Income"]);
                amt_total_income.Text = (double.Parse(amt_netpay.Text, NumberStyles.Currency)
                    + double.Parse(amt_ret_income.Text, NumberStyles.Currency)
                    + double.Parse(amt_other_income.Text, NumberStyles.Currency)).ToString("C2");

                amt_rent.Text = string.Format("{0:C2}", category_amounts["Rent"]);
                amt_utilities.Text = string.Format("{0:C2}", category_amounts["Utilities"]);
                amt_commute.Text = string.Format("{0:C2}", category_amounts["Commute"]);
                amt_groceries.Text = string.Format("{0:C2}", category_amounts["Groceries"]);
                amt_restaurants.Text = string.Format("{0:C2}", category_amounts["Restaurants"]);
                amt_cash.Text = string.Format("{0:C2}", category_amounts["Cash"]);
                amt_living_expense.Text = (double.Parse(amt_rent.Text, NumberStyles.Currency)
                    + double.Parse(amt_utilities.Text, NumberStyles.Currency)
                    + double.Parse(amt_commute.Text, NumberStyles.Currency)
                    + double.Parse(amt_groceries.Text, NumberStyles.Currency)
                    + double.Parse(amt_restaurants.Text, NumberStyles.Currency)
                    + double.Parse(amt_cash.Text, NumberStyles.Currency)).ToString("C2");

                amt_shopping.Text = string.Format("{0:C2}", category_amounts["Shopping"]);
                amt_entertainment.Text = string.Format("{0:C2}", category_amounts["Entertainment"]);
                amt_travel.Text = string.Format("{0:C2}", category_amounts["Travel"]);
                amt_medical.Text = string.Format("{0:C2}", category_amounts["Medical"]);
                amt_other_expense.Text = (double.Parse(amt_shopping.Text, NumberStyles.Currency)
                    + double.Parse(amt_entertainment.Text, NumberStyles.Currency)
                    + double.Parse(amt_travel.Text, NumberStyles.Currency)
                    + double.Parse(amt_medical.Text, NumberStyles.Currency)).ToString("C2");

                amt_total_expense.Text = (double.Parse(amt_living_expense.Text, NumberStyles.Currency) + double.Parse(amt_other_expense.Text, NumberStyles.Currency)).ToString("C2");

                amt_savings_before_misc.Text = (double.Parse(amt_total_income.Text, NumberStyles.Currency) + double.Parse(amt_total_expense.Text, NumberStyles.Currency)).ToString("C2");

                amt_misc.Text = string.Format("{0:C2}", category_amounts["Miscellaneous"]);
                amt_investment_gains.Text = string.Format("{0:C2}", category_amounts["Investment Gains"]);

                amt_net_savings.Text = (double.Parse(amt_total_income.Text, NumberStyles.Currency)
                    + double.Parse(amt_total_expense.Text, NumberStyles.Currency)
                    + double.Parse(amt_misc.Text, NumberStyles.Currency)
                    + double.Parse(amt_investment_gains.Text, NumberStyles.Currency)
                    ).ToString("C2");
            });
        }

        public SeriesCollection SeriesCollection_budget { get; set; }
        public string[] Labels { get; set; }
        public Func<double, string> Formatter { get; set; }
        public void MakeExpenseChart()
        {
            MakeExpenseChart(false);
        }
        public void MakeExpenseChart(bool change)
        {
            //bar chart
            MonthlyStatement statement = new MonthlyStatement();

            Dispatcher.Invoke(() =>
            {
                statement.holder = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
                statement.statement_date = Convert.ToDateTime(date_month.SelectedDate);
            });
            
            if (!change)
            {
                SeriesCollection_budget = new SeriesCollection();
            }
            
            Tuple<List<double>,List<double>, List<string>> result = statement.CompareMonths(statement.statement_date.AddMonths(-1));

            ChartValues<double> ChartValues_thismonth = new ChartValues<double>(result.Item1);
            ChartValues<double> ChartValues_lastmonth = new ChartValues<double>(result.Item2);
            List<string> labels = result.Item3;

            Dispatcher.Invoke(() =>
            {
                ColumnSeries colseries_thismonth = new ColumnSeries();
                colseries_thismonth.Title = string.Format("{0:MMM yyyy}", statement.statement_date);
                colseries_thismonth.Values = ChartValues_thismonth;
                colseries_thismonth.DataLabels = true;
                colseries_thismonth.ColumnPadding = 0;
                colseries_thismonth.MaxColumnWidth = 15;

                ColumnSeries colseries_lastmonth = new ColumnSeries();
                colseries_lastmonth.Title = string.Format("{0:MMM yyyy}", statement.statement_date.AddMonths(-1));
                colseries_lastmonth.Values = ChartValues_lastmonth;
                colseries_lastmonth.ColumnPadding = 0;
                colseries_lastmonth.MaxColumnWidth = 15;

                SeriesCollection_budget.Clear();
                SeriesCollection_budget.Add(colseries_thismonth);
                SeriesCollection_budget.Add(colseries_lastmonth);

                if (!change)
                {

                    Labels = labels.ToArray();
                    Formatter = value => value.ToString("C0");
                    DataContext = this;
                }
            });
        }

        private void SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (date_month.IsLoaded)
            {
                Task.Factory.StartNew(() =>
                {
                    GetAlert();
                });
                Task.Factory.StartNew(() =>
                {
                    FillDataGrid(null);
                });
                Task.Factory.StartNew(() =>
                {
                    GetStatementItems();
                });
                Task.Factory.StartNew(() =>
                {
                    MakeExpenseChart(true);
                });
            }
        }

        private void UpdateGrid(object sender, MouseButtonEventArgs e)
        {
            string category = (sender as TextBlock).Text;
            FillDataGrid(category);
        }

        private void InvestmentsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Uri("Investments.xaml", UriKind.Relative));
        }
        private void HistoricalButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Uri("HistoricalSeries.xaml", UriKind.Relative));
        }

        private void DataDefinitionsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Uri("DataDefinitions.xaml", UriKind.Relative));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            (this.Parent as Window).WindowState = WindowState.Maximized;
        }
    }
}
