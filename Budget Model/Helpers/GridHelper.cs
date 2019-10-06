using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Budget_Model.Helpers
{
    static class GridHelper
    {
        public static TextBlock CreateTextInGrid(string text, int row, int column)
        {
            return CreateTextInGrid(text, row, column, false, HorizontalAlignment.Left, false, false, false);
        }
        public static TextBlock CreateTextInGrid(string text, int row, int column, bool vertical_align, HorizontalAlignment horizontal_align, bool bold, bool italic, bool margin)
        {
            TextBlock txt = new TextBlock();
            txt.Text = text;
            Grid.SetRow(txt, row);
            Grid.SetColumn(txt, column);
            txt.HorizontalAlignment = horizontal_align;
            if (vertical_align)
            {
                txt.VerticalAlignment = VerticalAlignment.Bottom;
            }
            if (italic)
            {
                txt.FontStyle = FontStyles.Italic;
            }
            if (bold)
            {
                txt.FontWeight = FontWeights.Bold;
            }
            if (margin)
            {
                txt.Margin = new Thickness(10, 2, 10, 2);
            }
            return txt;
        }
    }
}
