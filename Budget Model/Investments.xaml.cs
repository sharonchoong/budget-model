using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Xml;
using Budget_Model.Models;
using System.Windows.Media;

namespace Budget_Model
{
    /// <summary>
    /// Interaction logic for Investments.xaml
    /// </summary>
    public partial class Investments : Page
    {
        public Func<double, string> MonthFormatter { get; set; }
        public Func<double, string> DayFormatter { get; set; }
        public Func<double, string> GainFormatter { get; set; }
        public Func<double, string> CurrencyFormatter { get; set; }
        public Func<double, string> CurrencyFormatter2 { get; set; }
        public string order_history_formatter { get; set; } = "C2";
        public class DateModel
        {
            public DateTime DateTime { get; set; }
            public double Value { get; set; }
        }
        public string gainformatter { get; set; } = "C0";
        public IEnumerable<Holder> HoldersItems { get; set; }

        public Investments()
        {
            InitializeComponent();
            
            HoldersItems = Helpers.Initializer.SetHolders(comboFor, this);
            
            date_month_to.SelectedDateChanged -= SelectionChanged;
            date_month_from.SelectedDateChanged -= SelectionChanged;
            Helpers.Initializer.SetDates(date_month_from, date_month_to);
            date_month_to.SelectedDateChanged += SelectionChanged;
            date_month_from.SelectedDateChanged += SelectionChanged;

            Categories.ItemsSource = InvestmentCategory.Getcategories();

            var dateConfig = Mappers.Xy<DateModel>()
                        .X(dateModel => dateModel.DateTime.AddDays(-13).Ticks / (TimeSpan.FromDays(1).Ticks * 30.44))
                        .Y(dateModel => dateModel.Value);

            //save the mapper globally.
            Charting.For<DateModel>(dateConfig);
            GainFormatter = value => value.ToString(gainformatter);
            CurrencyFormatter = value => value.ToString("C0");
            CurrencyFormatter2 = value => value.ToString(order_history_formatter);
            MonthFormatter = value => new DateTime((long)((value < 0 ? 0 : value) * TimeSpan.FromDays(1).Ticks * 30.44)).AddDays(13).ToString("MMM yy");
            DayFormatter = value => new DateTime((long)((value < 0 ? 0 : value) * TimeSpan.FromDays(1).Ticks * 30.44)).AddDays(13).ToString("M/d/yy");
            
            Task.Factory.StartNew(() => {
                DataGridInvestments.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
                FillDataGrid(null);
            });
            Task.Factory.StartNew(() => {
                Gains_Chart();
            });
            Task.Factory.StartNew(() => {
                Stacked_Chart();
            });
            Task.Factory.StartNew(() => {
                StackedAsset_Chart();
            });
            Task.Factory.StartNew(() => {
                PriceAsset_Chart();
            });
        }

        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (DataGridInvestments.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                for (int i = 0; i < DataGridInvestments.Items.Count; i++)
                {
                    var row = (DataGridRow)DataGridInvestments.ItemContainerGenerator.ContainerFromIndex(i);
                    DataRowView rowview = (DataRowView)DataGridInvestments.Items[i];
                    if (row != null)
                    {
                        if (rowview["category_sort"].ToString() != "0")
                        {
                            row.Background = new SolidColorBrush(Colors.White);
                        }
                        else
                        {
                            row.Background = new SolidColorBrush(Colors.DarkSalmon);
                        }
                    }
                }
            }
        }

        private void SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DateTime tryparsedate;
            if (date_month_from.IsLoaded && date_month_to.IsLoaded
                && !(DateTime.TryParse(e.RemovedItems[0].ToString(), out tryparsedate) && ((DateTime)e.RemovedItems[0]).Year == ((DateTime)e.AddedItems[0]).Year && ((DateTime)e.RemovedItems[0]).Month == ((DateTime)e.AddedItems[0]).Month))
            {
                Task.Factory.StartNew(() => {
                    FillDataGrid(null);
                });
                if (DateTime.TryParse(e.RemovedItems[0].ToString(), out tryparsedate))
                {
                    Task.Factory.StartNew(() => {
                        Gains_Chart(true);
                    });
                }
                Task.Factory.StartNew(() => {
                    Stacked_Chart(true);
                });
                Task.Factory.StartNew(() => {
                    StackedAsset_Chart(true);
                });
                Task.Factory.StartNew(() => {
                    PriceAsset_Chart(true);
                });
            }
        }

        private void GainChanged(object sender, SelectionChangedEventArgs e)
        {
            if (date_month_from.IsLoaded && date_month_to.IsLoaded)
            {
                Task.Factory.StartNew(() => {
                    FillDataGrid(null);
                });
                Task.Factory.StartNew(() => {
                    Gains_Chart(true);
                });
            }
        }

        public void FillDataGrid(string date)
        {
            //filling datagrid 
            string selected_person = "";
            DateTime date_selected = new DateTime();
            Dispatcher.Invoke(() =>
            {
                selected_person = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
                if (date == null)
                {
                    date_selected = Convert.ToDateTime(date_month_to.SelectedDate);
                }
                else
                {
                    DateTime.TryParseExact(date, "MMM yy", new System.Globalization.CultureInfo("en-US"), System.Globalization.DateTimeStyles.None, out date_selected);
                }
            });
            DataTable dt = BrokerageAsset.GetMonthInvestments(selected_person, date_selected);
            Dispatcher.Invoke(() =>
            {
                if (dt.Rows.Count > 0)
                {
                    date_grid.Text = "Date: " + dt.Rows[0].Field<DateTime>("date").ToShortDateString();
                }
                else
                {
                    date_grid.Text = "";
                }
                DataGridInvestments.CommitEdit();
                DataGridInvestments.CommitEdit();
                DataGridInvestments.DataContext = dt;
            });
        }

        private bool isManualEditCommit;
        private void GridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (!isManualEditCommit)
            {
                isManualEditCommit = true;
                DataGridRow row = e.Row;
                DataRowView row_items = (DataRowView)row.Item;
                if (e.Column.Header.ToString() == "Category")
                {
                    ComboBox cb = e.EditingElement as ComboBox;
                    string t = Convert.ToString(cb.SelectedItem);
                    InvestmentCategory new_category = new InvestmentCategory();
                    new_category.Category = t;
                    new_category.Keyword = row_items.Row.ItemArray[1].ToString().Replace("'", "''");
                    new_category.UpdateDefinition(new_category.Keyword, row_items.Row.ItemArray[1] != DBNull.Value && !string.IsNullOrWhiteSpace(t));
                    FillDataGrid(null);
                }
                else
                {
                    DataGridInvestments.CommitEdit();
                    DataGridInvestments.CommitEdit();
                }
                isManualEditCommit = false;
            }
        }
        
        public SeriesCollection Series_Gains { get; set; } = new SeriesCollection();

        public void Gains_Chart()
        {
            Gains_Chart(false);
        }

        public void Gains_Chart(bool change)
        {
            DateTime from_month = new DateTime();
            DateTime to_month = new DateTime();
            string datatype = "";
            Dispatcher.Invoke(() =>
            {
                from_month = Convert.ToDateTime(date_month_from.SelectedDate);
                to_month = Convert.ToDateTime(date_month_to.SelectedDate);
                datatype = ((ComboBoxItem)(comboGain.SelectedItem)).Content.ToString();
            });
            gainformatter = "C0";

            if (datatype.Contains("Percentage"))
            {
                gainformatter = "P2";
            }
            DataTable dt = InvestmentChange.GetHistoricalGains(datatype, from_month, to_month);

            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, (Action)(() =>
            {
                Series_Gains.Clear();
                foreach (Holder holder in HoldersItems)
                {
                    if (dt.AsEnumerable().Any(r => r["holder"].ToString() == holder.HolderName))
                    {
                        Series_Gains.Add(new LineSeries
                        {
                            Values = new ChartValues<DateModel>(dt.AsEnumerable().Where(r => r["holder"].ToString() == holder.HolderName)
                            .Select(r => new DateModel { DateTime = r.Field<DateTime>("date"), Value = r.Field<double?>("amount") ?? 0 })),
                            Title = holder.HolderName,
                            Fill = Brushes.Transparent,
                            DataLabels = (holder.HolderName == "Home" ? true : false)
                        });
                    }
                }
                if (!change)
                {
                    InvestmentGainsChart.DataContext = this;
                }
            }));

        }

        public SeriesCollection StackedSeriesCollection { get; set; } = new SeriesCollection();
        public void Stacked_Chart()
        {
            Stacked_Chart(false);
        }
        public void Stacked_Chart(bool change)
        {
            DateTime from_month = new DateTime();
            DateTime to_month = new DateTime();
            string selected_person = "";
            Dispatcher.Invoke(() =>
            {
                from_month = new DateTime(Convert.ToDateTime(date_month_from.SelectedDate).Year, Convert.ToDateTime(date_month_from.SelectedDate).Month, 1);
                to_month = Convert.ToDateTime(date_month_to.SelectedDate);
                selected_person = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
                StackedSeriesCollection.Clear();
            });

            DataTable dt = BrokerageAsset.GetHistoricalInvestments(selected_person, null, null, from_month, to_month);

            IEnumerable<string> selectcategories = InvestmentCategory.Getcategories();
            Brush[] colors = { Brushes.DarkGreen, Brushes.ForestGreen, Brushes.Gainsboro, Brushes.Gold, Brushes.DarkBlue };
            Dispatcher.Invoke(() =>
            {
                int count = 0;
                foreach (string category in selectcategories)
                {
                    ChartValues<DateModel> chartvalues = new ChartValues<DateModel>(dt.AsEnumerable().Where(r => r["category"].ToString() == category)
                            .Select(r => new DateModel { DateTime = r.Field<DateTime>("date"), Value = r.Field<double?>("ending_mkt_value") ?? 0 }));
                    if (chartvalues.Count != 0)
                    {
                        StackedSeriesCollection.Add(new StackedColumnSeries
                        {
                            Values = chartvalues,
                            StackMode = StackMode.Values,
                            Title = category,
                            Fill = colors[count],
                            DataLabels = true,
                            LabelPoint = point => (point.Y / 1000).ToString("C1") + "k",
                            LabelsPosition = BarLabelPosition.Perpendicular,
                            Foreground = System.Windows.Media.Brushes.Black
                        });
                        count++;
                    }
                }
            });

            if (!change)
            {
                Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.DataBind, (Action)(() =>
                {
                    DataContext = this;
                }));
            }
        }

        public SeriesCollection StackedSeriesCollection2 { get; set; } = new SeriesCollection();
        public void StackedAsset_Chart()
        {
            StackedAsset_Chart(false, "US Equity");
        }
        public void StackedAsset_Chart(bool change)
        {
            StackedAsset_Chart(change, "US Equity");
        }
        public void StackedAsset_Chart(string category)
        {
            StackedAsset_Chart(false, category);
        }
        public void StackedAsset_Chart(bool change, string category)
        {
            DateTime from_month = new DateTime();
            DateTime to_month = new DateTime();
            string selected_person = "";
            Regex regex = new Regex(@"\d{1,2}\/\d{1,2}\/\d{4}");

            Dispatcher.Invoke(() =>
            {
                label_assetclass.Text = category + " Allocation";

                from_month = new DateTime(Convert.ToDateTime(date_month_from.SelectedDate).Year, Convert.ToDateTime(date_month_from.SelectedDate).Month, 1);
                to_month = Convert.ToDateTime(date_month_to.SelectedDate);
                selected_person = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
                StackedSeriesCollection2.Clear();
            });

            string asset_column = (new [] { "Treasuries", "Bonds", "CDs" }.Contains(category) ? "asset_description" : "asset_symbol");
            DataTable dt = BrokerageAsset.GetHistoricalInvestments(selected_person, category, asset_column, from_month, to_month);

            string[] assets = dt.AsEnumerable().Select(r => r[asset_column].ToString()).Distinct()
                .OrderBy(s => (regex.IsMatch(s) ? Convert.ToDateTime(regex.Match(s).Value).ToString("yyyyMMdd") : s))
                .ToArray();
            Dispatcher.Invoke(() =>
            {
                if (assets.Length > 8)
                {
                    chartStackedAsset.LegendLocation = LegendLocation.None;
                }
                else
                {
                    chartStackedAsset.LegendLocation = LegendLocation.Top;
                }
                    
                foreach (string asset in assets)
                {
                    ChartValues<DateModel> chartvalues = new ChartValues<DateModel>(dt.AsEnumerable().Where(r => r[asset_column].ToString() == asset)
                            .Select(r => new DateModel { DateTime = r.Field<DateTime>("date"), Value = r.Field<double?>("ending_mkt_value") ?? 0 }));
                    if (chartvalues.Count != 0)
                    {
                        StackedSeriesCollection2.Add(new StackedColumnSeries
                        {
                            Values = chartvalues,
                            StackMode = StackMode.Values,
                            Title = (regex.IsMatch(asset) ? regex.Match(asset).Value : asset),
                            DataLabels = (assets.Length > 8 ? false : true),
                            LabelPoint = point => (point.Y / 1000).ToString("C1") + "k",
                            LabelsPosition = BarLabelPosition.Perpendicular,
                            Foreground = System.Windows.Media.Brushes.Black
                        });
                    }
                }
            });

            if (!change)
            {
                Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.DataBind, (Action)(() =>
                {
                    DataContext = this;
                }));
            }
        }

        public SeriesCollection PriceSeriesCollection { get; set; } = new SeriesCollection();
        public void PriceAsset_Chart()
        {
            PriceAsset_Chart(false, null);
        }
        public void PriceAsset_Chart(bool change)
        {
            PriceAsset_Chart(change, null);
        }
        public void PriceAsset_Chart(string asset)
        {
            PriceAsset_Chart(false, asset);
        }
        public void PriceAsset_Chart(bool change, string _asset)
        {
            string asset = _asset;
            if (_asset == null)
            {
                asset = BrokerageAsset.GetFirstAsset();
            }
            DateTime from_month = new DateTime();
            DateTime to_month = new DateTime();
            string selected_person = "";

            Dispatcher.Invoke(() =>
            {
                from_month = new DateTime(Convert.ToDateTime(date_month_from.SelectedDate).Year, Convert.ToDateTime(date_month_from.SelectedDate).Month, 1);
                to_month = Convert.ToDateTime(date_month_to.SelectedDate);
                selected_person = HoldersItems.Where(r => r.IsChecked == true).Select(x => x.HolderName).First();
                PriceSeriesCollection.Clear();
            });

            //stock price API
            ChartValues<DateModel> chartprices = new ChartValues<DateModel>();
            ChartValues<DateModel> chartprices1 = new ChartValues<DateModel>();
            ChartValues<DateModel> chartprices2 = new ChartValues<DateModel>();
            ChartValues<DateModel> chartprices10 = new ChartValues<DateModel>();
            if (!new [] { "Treasuries", "Bonds", "CDs" }.Contains(asset))
            {
                order_history_formatter = "C2";
                string alphav_key = System.Web.Configuration.WebConfigurationManager.AppSettings["alphav_key"];
                if (alphav_key != null)
                {
                    string url_alphav_priceadj = "https://www.alphavantage.co/query?function=TIME_SERIES_DAILY_ADJUSTED&symbol=" + asset + "&apikey=" + alphav_key;
                    string results_timeseries = "Time Series (Daily)";
                    if ((to_month - from_month).TotalDays > 95)
                    {
                        url_alphav_priceadj = "https://www.alphavantage.co/query?function=TIME_SERIES_WEEKLY_ADJUSTED&symbol=" + asset + "&apikey=" + alphav_key;
                        results_timeseries = "Weekly Adjusted Time Series";
                    }
                    JObject json_results = (JObject)JsonConvert.DeserializeObject(Helpers.APIGet.GetAPIdata(url_alphav_priceadj));
                    if (json_results.First.ToString().Contains("Error Message"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            alert_APIerror.Visibility = Visibility.Visible;
                            alert_APIerror.Text = "Security not found";
                        });
                    }
                    else if (json_results.First.ToString().Contains("Thank you for"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            alert_APIerror.Visibility = Visibility.Visible;
                            alert_APIerror.Text = "API call limit reached (5 per min)";
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            alert_APIerror.Visibility = Visibility.Hidden;
                        });
                        foreach (JProperty date in json_results[results_timeseries])
                        {
                            if (Convert.ToDateTime(date.Name) <= to_month && Convert.ToDateTime(date.Name) >= from_month)
                            {
                                chartprices.Add(new DateModel { DateTime = Convert.ToDateTime(date.Name), Value = Convert.ToDouble(date.Value["5. adjusted close"]) });
                            }
                        }
                    }
                }
            }
            else
            {
                order_history_formatter = "P2";
                string url_yields = "https://data.treasury.gov/feed.svc/DailyTreasuryYieldCurveRateData?$filter=year(NEW_DATE)%20gt%20" + (from_month.Year - 1);
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(Helpers.APIGet.GetAPIdata(url_yields));
                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("a", "http://www.w3.org/2005/Atom");
                nsmgr.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
                nsmgr.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices");
                foreach (XmlNode node in doc.DocumentElement.SelectNodes("a:entry/a:content", nsmgr))
                {
                    DateTime entry_date = Convert.ToDateTime(node.SelectSingleNode("m:properties/d:NEW_DATE", nsmgr).InnerText);
                    if ((to_month - from_month).TotalDays > 95 && entry_date.DayOfWeek != DayOfWeek.Friday)
                    {
                        continue;
                    }
                    
                    if (entry_date >= from_month && entry_date <= to_month)
                    {
                        chartprices.Add(new DateModel { DateTime = entry_date, Value = Convert.ToDouble(node.SelectSingleNode("m:properties/d:BC_3MONTH", nsmgr).InnerText) / 100 });
                        chartprices1.Add(new DateModel { DateTime = entry_date, Value = Convert.ToDouble(node.SelectSingleNode("m:properties/d:BC_1YEAR", nsmgr).InnerText) / 100 });
                        chartprices2.Add(new DateModel { DateTime = entry_date, Value = Convert.ToDouble(node.SelectSingleNode("m:properties/d:BC_2YEAR", nsmgr).InnerText) / 100 });
                        chartprices10.Add(new DateModel { DateTime = entry_date, Value = Convert.ToDouble(node.SelectSingleNode("m:properties/d:BC_10YEAR", nsmgr).InnerText) / 100 });
                    }
                }
            }

            //database of past investment transactions
            string variable_sql = (new [] { "Treasuries", "Bonds", "CDs" }.Contains(asset) ? "yield_to_maturity" : "price");
            string variable_label = (new [] { "Treasuries", "Bonds", "CDs" }.Contains(asset) ? "Yield" : "Price");
            Dispatcher.Invoke(() =>
            {
                gridstats.Children.Clear();

                if (chartprices.Count != 0)
                {
                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid("Latest " + variable_label + " (" + chartprices.AsEnumerable().OrderBy(r => r.DateTime).Last().DateTime.ToShortDateString()
                        + "):", 2, 0));
                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid("Average " + variable_label + ":", 3, 0));
                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid("Minimum " + variable_label + ":", 4, 0));
                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid("Maximum " + variable_label + ":", 5, 0));

                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices.AsEnumerable().OrderBy(r => r.DateTime).Last().Value.ToString(order_history_formatter), 2, 1));
                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices.AsEnumerable().Average(r => r.Value).ToString(order_history_formatter), 3, 1));
                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices.AsEnumerable().Min(r => r.Value).ToString(order_history_formatter), 4, 1));
                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices.AsEnumerable().Max(r => r.Value).ToString(order_history_formatter), 5, 1));

                    PriceSeriesCollection.Add(new LineSeries
                    {
                        Values = chartprices,
                        Title = (new string[] { "Treasuries", "Bonds", "CDs" }.Contains(asset) ? "3MO T-Bill" : asset),
                        Foreground = System.Windows.Media.Brushes.Black,
                        PointGeometrySize = 0
                    });
                    
                    
                    if (new string[] { "Treasuries", "Bonds", "CDs" }.Contains(asset))
                    {
                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid("3-Month:", 1, 1));

                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid("1-Year:", 1, 2));
                        
                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices1.AsEnumerable().OrderBy(r => r.DateTime).Last().Value.ToString(order_history_formatter), 2, 2));
                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices1.AsEnumerable().Average(r => r.Value).ToString(order_history_formatter), 3, 2));
                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices1.AsEnumerable().Min(r => r.Value).ToString(order_history_formatter), 4, 2));
                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices1.AsEnumerable().Max(r => r.Value).ToString(order_history_formatter), 5, 2));

                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid("2-Year:", 1, 3));

                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices2.AsEnumerable().OrderBy(r => r.DateTime).Last().Value.ToString(order_history_formatter), 2, 3));
                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices2.AsEnumerable().Average(r => r.Value).ToString(order_history_formatter), 3, 3));
                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices2.AsEnumerable().Min(r => r.Value).ToString(order_history_formatter), 4, 3));
                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices2.AsEnumerable().Max(r => r.Value).ToString(order_history_formatter), 5, 3));

                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid("10-Year:", 1, 4));

                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices10.AsEnumerable().OrderBy(r => r.DateTime).Last().Value.ToString(order_history_formatter), 2, 4));
                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices10.AsEnumerable().Average(r => r.Value).ToString(order_history_formatter), 3, 4));
                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices10.AsEnumerable().Min(r => r.Value).ToString(order_history_formatter), 4, 4));
                        gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(chartprices10.AsEnumerable().Max(r => r.Value).ToString(order_history_formatter), 5, 4));

                        PriceSeriesCollection.Add(new LineSeries
                        {
                            Values = chartprices1,
                            Title = "1YR T-Bill",
                            Foreground = System.Windows.Media.Brushes.Black,
                            PointGeometrySize = 0
                        });
                        PriceSeriesCollection.Add(new LineSeries
                        {
                            Values = chartprices2,
                            Title = "2YR T-Bill",
                            Foreground = System.Windows.Media.Brushes.Black,
                            PointGeometrySize = 0
                        });
                        PriceSeriesCollection.Add(new LineSeries
                        {
                            Values = chartprices10,
                            Title = "10YR T-Bill",
                            Foreground = System.Windows.Media.Brushes.Black,
                            PointGeometrySize = 0
                        });
                    }
                }
            });

            Dispatcher.Invoke(() => 
            {
                DataTable dt = BrokerageTransaction.GetHistoricalTransactions(asset, selected_person, from_month, to_month);
                if (dt.AsEnumerable().Where(r => r["transaction_description"].ToString().IndexOf("bought", StringComparison.OrdinalIgnoreCase) >= 0).Count() != 0 ||
                    dt.AsEnumerable().Where(r => r["transaction_description"].ToString().IndexOf("buy", StringComparison.OrdinalIgnoreCase) >= 0).Count() != 0)
                {
                    PriceSeriesCollection.Add(new ScatterSeries
                    {
                        Title = "Bought",
                        Values = new ChartValues<DateModel>(dt.AsEnumerable().Where(r => r["transaction_description"].ToString().IndexOf("bought", StringComparison.OrdinalIgnoreCase) >= 0 
                            || r["transaction_description"].ToString().IndexOf("buy", StringComparison.OrdinalIgnoreCase) >= 0)
                            .Select(r => new DateModel { DateTime = r.Field<DateTime>("date"), Value = r.Field<double?>(variable_sql) ?? 0 }))
                    });
                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid("Average " + variable_label + " Bought:", 0, 0));
                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(dt.AsEnumerable().Where(r => r["transaction_description"].ToString().IndexOf("bought", StringComparison.OrdinalIgnoreCase) >= 0 
                        || r["transaction_description"].ToString().IndexOf("buy", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Average(r => r.Field<double>(variable_sql)).ToString(order_history_formatter), 0, 1));
                }
                if (dt.AsEnumerable().Where(r => r["transaction_description"].ToString().IndexOf("sold", StringComparison.OrdinalIgnoreCase) >= 0).Count() != 0 ||
                    dt.AsEnumerable().Where(r => r["transaction_description"].ToString().IndexOf("sell", StringComparison.OrdinalIgnoreCase) >= 0).Count() != 0)
                { 
                    PriceSeriesCollection.Add(new ScatterSeries
                    {
                        Title = "Sold",
                        Values = new ChartValues<DateModel>(dt.AsEnumerable().Where(r => r["transaction_description"].ToString().IndexOf("sold", StringComparison.OrdinalIgnoreCase) >= 0 
                            || r["transaction_description"].ToString().IndexOf("sell", StringComparison.OrdinalIgnoreCase) >= 0)
                            .Select(r => new DateModel { DateTime = r.Field<DateTime>("date"), Value = r.Field<double?>(variable_sql) ?? 0 }))
                    });
                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid("Average " + variable_label + " Sold:", 0, 2));
                    gridstats.Children.Add(Helpers.GridHelper.CreateTextInGrid(dt.AsEnumerable().Where(r => r["transaction_description"].ToString().IndexOf("sold", StringComparison.OrdinalIgnoreCase) >= 0 
                        || r["transaction_description"].ToString().IndexOf("sell", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Average(r => r.Field<double>(variable_sql)).ToString(order_history_formatter), 0, 3));
                }
                label_assetclassprice.Text = asset + ": Order History and " + (new [] { "Treasuries", "Bonds", "CDs" }.Contains(asset) ? "Yields" : "Price (Adjusted For Splits and Dividends)");
                label_secstats.Text = "Statistics for " + asset;
                PriceChart.DataContext = this;
            });
        }

        public void ClickFillData(object sender, ChartPoint p)
        {
            FillDataGrid(MonthFormatter(p.X));
        }
        private string category_clicked = "";
        public void ClickCategoryAssets(object sender, ChartPoint p)
        {
            FillDataGrid(MonthFormatter(p.X));
            category_clicked = p.SeriesView.Title;
            Task.Factory.StartNew(() => {
                StackedAsset_Chart(true, category_clicked);
            });
            if (new [] { "Treasuries", "Bonds", "CDs" }.Contains(category_clicked))
            {
                Task.Factory.StartNew(() => {
                    PriceAsset_Chart(true, category_clicked);
                });
            }
        }

        public void ClickAsset(object sender, ChartPoint p)
        {
            FillDataGrid(MonthFormatter(p.X));
            string asset = p.SeriesView.Title;
            if (category_clicked != "Cash" && !new [] { "Treasuries", "Bonds", "CDs" }.Contains(category_clicked))
            {
                Task.Factory.StartNew(() => {
                    PriceAsset_Chart(true, asset);
                });
            }
        }
    }
}
