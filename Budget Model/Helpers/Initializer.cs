using Budget_Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Budget_Model.Helpers
{
    static class Initializer
    {
        public static IEnumerable<Holder> SetHolders(ComboBox comboFor, Page page)
        {
            Holder holder = new Holder();
            comboFor.DataContext = page;
            comboFor.SelectedIndex = 0;
            return holder.HolderCollection(true);
        }

        public static void SetDates(DatePicker date_month)
        {
            SetDates(null, date_month);
        }
        public static void SetDates(DatePicker date_month_from, DatePicker date_month_to)
        {
            Tuple<DateTime, DateTime> dates = BankTransaction.GetDates("Home");
            if (dates != null)
            {
                date_month_to.SelectedDate = new DateTime(dates.Item2.Year, dates.Item2.Month, 1).AddMonths(1).AddDays(-1);
                date_month_to.DisplayDate = new DateTime(dates.Item2.Year, dates.Item2.Month, 1).AddMonths(1).AddDays(-1);
                if (date_month_from != null)
                {
                    date_month_from.SelectedDate = new DateTime(dates.Item2.Year, dates.Item2.Month, 1).AddMonths(-11);
                    date_month_from.DisplayDate = new DateTime(dates.Item2.Year, dates.Item2.Month, 1).AddMonths(-11);
                }
            }
            else
            {
                date_month_to.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(-1);
                date_month_to.DisplayDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(-1);
                if (date_month_from != null)
                {
                    date_month_from.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11).AddDays(-1);
                    date_month_from.DisplayDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11).AddDays(-1);
                }
            }
        }
    }
}
