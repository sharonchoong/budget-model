using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace Budget_Model
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
    }

    public class DatePickerCalendar
    {
        public static readonly DependencyProperty IsMonthYearProperty =
            DependencyProperty.RegisterAttached("IsMonthYear", typeof(bool), typeof(DatePickerCalendar),
                                                new PropertyMetadata(OnIsMonthYearChanged));

        public static bool GetIsMonthYear(DependencyObject dobj)
        {
            return (bool)dobj.GetValue(IsMonthYearProperty);
        }

        public static void SetIsMonthYear(DependencyObject dobj, bool value)
        {
            dobj.SetValue(IsMonthYearProperty, value);
        }

        private static void OnIsMonthYearChanged(DependencyObject dobj, DependencyPropertyChangedEventArgs e)
        {
            var datePicker = (DatePicker)dobj;

            Application.Current.Dispatcher
                .BeginInvoke(DispatcherPriority.Loaded,
                             new Action<DatePicker, DependencyPropertyChangedEventArgs>(SetCalendarEventHandlers),
                             datePicker, e);
        }

        private static void SetCalendarEventHandlers(DatePicker datePicker, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue == e.OldValue)
                return;

            if ((bool)e.NewValue)
            {
                datePicker.CalendarOpened += DatePickerOnCalendarOpened;
                datePicker.CalendarClosed += DatePickerOnCalendarClosed;
            }
            else
            {
                datePicker.CalendarOpened -= DatePickerOnCalendarOpened;
                datePicker.CalendarClosed -= DatePickerOnCalendarClosed;
            }
        }

        private static void DatePickerOnCalendarOpened(object sender, RoutedEventArgs routedEventArgs)
        {
            var calendar = GetDatePickerCalendar(sender);
            calendar.DisplayMode = CalendarMode.Year;

            calendar.DisplayModeChanged += CalendarOnDisplayModeChanged;
        }

        private static void DatePickerOnCalendarClosed(object sender, RoutedEventArgs routedEventArgs)
        {
            var datePicker = (DatePicker)sender;
            var calendar = GetDatePickerCalendar(sender);
            datePicker.SelectedDate = calendar.SelectedDate;

            calendar.DisplayModeChanged -= CalendarOnDisplayModeChanged;
        }

        private static void CalendarOnDisplayModeChanged(object sender, CalendarModeChangedEventArgs e)
        {
            var calendar = (Calendar)sender;
            if (calendar.DisplayMode != CalendarMode.Month)
                return;

            calendar.SelectedDate = GetSelectedCalendarDate(calendar.DisplayDate);

            var datePicker = GetCalendarsDatePicker(calendar);
            datePicker.IsDropDownOpen = false;
        }

        private static Calendar GetDatePickerCalendar(object sender)
        {
            var datePicker = (DatePicker)sender;
            var popup = (Popup)datePicker.Template.FindName("PART_Popup", datePicker);
            return ((Calendar)popup.Child);
        }

        private static DatePicker GetCalendarsDatePicker(FrameworkElement child)
        {
            var parent = (FrameworkElement)child.Parent;
            if (parent.Name == "PART_Root")
                return (DatePicker)parent.TemplatedParent;
            return GetCalendarsDatePicker(parent);
        }

        private static DateTime? GetSelectedCalendarDate(DateTime? selectedDate)
        {
            if (!selectedDate.HasValue)
                return null;
            return new DateTime(selectedDate.Value.Year, selectedDate.Value.Month, DateTime.DaysInMonth(selectedDate.Value.Year, selectedDate.Value.Month));
        }


    }

    public class DatePickerDateFormat
    {
        public static readonly DependencyProperty DateFormatProperty =
            DependencyProperty.RegisterAttached("DateFormat", typeof(string), typeof(DatePickerDateFormat),
                                                new PropertyMetadata(OnDateFormatChanged));

        public static string GetDateFormat(DependencyObject dobj)
        {
            return (string)dobj.GetValue(DateFormatProperty);
        }

        public static void SetDateFormat(DependencyObject dobj, string value)
        {
            dobj.SetValue(DateFormatProperty, value);
        }

        private static void OnDateFormatChanged(DependencyObject dobj, DependencyPropertyChangedEventArgs e)
        {
            var datePicker = (DatePicker)dobj;

            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded, new Action<DatePicker>(ApplyDateFormat), datePicker);
        }

        private static void ApplyDateFormat(DatePicker datePicker)
        {
            var binding = new Binding("SelectedDate")
            {
                RelativeSource = new RelativeSource { AncestorType = typeof(DatePicker) },
                Converter = new DatePickerDateTimeConverter(),
                ConverterParameter = new Tuple<DatePicker, string>(datePicker, GetDateFormat(datePicker))
            };
            var textBox = GetTemplateTextBox(datePicker);
            textBox.SetBinding(TextBox.TextProperty, binding);

            textBox.PreviewKeyDown -= TextBoxOnPreviewKeyDown;
            textBox.PreviewKeyDown += TextBoxOnPreviewKeyDown;

            datePicker.CalendarOpened -= DatePickerOnCalendarOpened;
            datePicker.CalendarOpened += DatePickerOnCalendarOpened;
        }

        private static TextBox GetTemplateTextBox(Control control)
        {
            control.ApplyTemplate();
            return (TextBox)control.Template.FindName("PART_TextBox", control);
        }

        private static void TextBoxOnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return)
                return;

            /* DatePicker subscribes to its TextBox's KeyDown event to set its SelectedDate if Key.Return was
             * pressed. When this happens its text will be the result of its internal date parsing until it
             * loses focus or another date is selected. A workaround is to stop the KeyDown event bubbling up
             * and handling setting the DatePicker.SelectedDate. */

            e.Handled = true;

            var textBox = (TextBox)sender;
            var datePicker = (DatePicker)textBox.TemplatedParent;
            var dateStr = textBox.Text;
            var formatStr = GetDateFormat(datePicker);
            datePicker.SelectedDate = DatePickerDateTimeConverter.StringToDateTime(datePicker, formatStr, dateStr);
        }

        private static void DatePickerOnCalendarOpened(object sender, RoutedEventArgs e)
        {
            /* When DatePicker's TextBox is not focused and its Calendar is opened by clicking its calendar button
             * its text will be the result of its internal date parsing until its TextBox is focused and another
             * date is selected. A workaround is to set this string when it is opened. */

            var datePicker = (DatePicker)sender;
            var textBox = GetTemplateTextBox(datePicker);
            var formatStr = GetDateFormat(datePicker);
            textBox.Text = DatePickerDateTimeConverter.DateTimeToString(formatStr, datePicker.SelectedDate);
        }

        private class DatePickerDateTimeConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                var formatStr = ((Tuple<DatePicker, string>)parameter).Item2;
                var selectedDate = (DateTime?)value;
                return DateTimeToString(formatStr, selectedDate);
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                var tupleParam = ((Tuple<DatePicker, string>)parameter);
                var dateStr = (string)value;
                return StringToDateTime(tupleParam.Item1, tupleParam.Item2, dateStr);
            }

            public static string DateTimeToString(string formatStr, DateTime? selectedDate)
            {
                return selectedDate.HasValue ? selectedDate.Value.ToString(formatStr) : null;
            }

            public static DateTime? StringToDateTime(DatePicker datePicker, string formatStr, string dateStr)
            {
                DateTime date;
                var canParse = DateTime.TryParseExact(dateStr, formatStr, System.Globalization.CultureInfo.CurrentCulture,
                                                      System.Globalization.DateTimeStyles.None, out date);

                if (!canParse)
                    canParse = DateTime.TryParse(dateStr, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out date);

                return canParse ? date : datePicker.SelectedDate;
            }
        }
    }

    public class xirr
    {
        public const double tol = 0.001;
        public delegate double fx(double x);

        public static fx composeFunctions(fx f1, fx f2)
        {
            return (double x) => f1(x) + f2(x);
        }

        public static fx f_xirr(double p, double dt, double dt0)
        {
            return (double x) => p * Math.Pow((1.0 + x), ((dt0 - dt) / 365.0));
        }

        public static fx df_xirr(double p, double dt, double dt0)
        {
            return (double x) => (1.0 / 365.0) * (dt0 - dt) * p * Math.Pow((x + 1.0), (((dt0 - dt) / 365.0) - 1.0));
        }

        public static fx total_f_xirr(double[] payments, double[] days)
        {
            fx resf = (double x) => 0.0;

            for (int i = 0; i < payments.Length; i++)
            {
                resf = composeFunctions(resf, f_xirr(payments[i], days[i], days[0]));
            }

            return resf;
        }

        public static fx total_df_xirr(double[] payments, double[] days)
        {
            fx resf = (double x) => 0.0;

            for (int i = 0; i < payments.Length; i++)
            {
                resf = composeFunctions(resf, df_xirr(payments[i], days[i], days[0]));
            }

            return resf;
        }

        public static double Newtons_method(double guess, fx f, fx df)
        {
            double x0 = guess;
            double x1 = 0.0;
            double err = 1e+100;

            while (err > tol)
            {
                x1 = x0 - f(x0) / df(x0);
                err = Math.Abs(x1 - x0);
                x0 = x1;
            }

            return x0;
        }

        public static double compute_yield_to_maturity(DateTime maturity, DateTime settlement, double price_paid, double coupon_rate, double par, double guess = 0.1)
        {
            int nequalsemianns = (int)Math.Ceiling((maturity - settlement).TotalDays / (365.0 / 2.0));
            List<double> payments = new List<double>();
            List<double> datesAsDoubles = new List<double>();
            payments.Add(price_paid);
            datesAsDoubles.Add((settlement - DateTime.MinValue).TotalDays);
            for (int i = 1; i < nequalsemianns; i++)
            {
                var totalDays = (maturity.AddMonths(-6 * i) - DateTime.MinValue).TotalDays;
                datesAsDoubles.Add(totalDays);
                payments.Add(coupon_rate / 2 * par);
            }
            payments.Add(par + (coupon_rate / 2 * par));
            datesAsDoubles.Add((maturity - DateTime.MinValue).TotalDays);

            double yield = Newtons_method(guess,
                    total_f_xirr(payments.ToArray(), datesAsDoubles.ToArray()),
                    total_df_xirr(payments.ToArray(), datesAsDoubles.ToArray()));
            return yield;
        }
    }
}
