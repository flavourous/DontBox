using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DontBox.WPF
{
    /// <summary>
    /// Interaction logic for ProgressDialog.xaml
    /// </summary>
    public partial class ProgressDialog : Window, IProgressView
    {
        public ProgressDialog(Window parent)
        {
            Owner = parent;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            InitializeComponent();
            DataContext = this;
        }

        public void Start()
        {
            Dispatcher.BeginInvoke(new Func<bool?>(ShowDialog));
        }

        public double ProgVal
        {
            get { return (double)GetValue(ProgValProperty); }
            set { Dispatcher.Invoke(() => SetValue(ProgValProperty, value)); }
        }
        public static readonly DependencyProperty ProgValProperty =
            DependencyProperty.Register("ProgVal", typeof(double), typeof(ProgressDialog), new PropertyMetadata(0.0));

        public String ProgText
        {
            get { return (String)GetValue(ProgTextProperty); }
            set { SetValue(ProgTextProperty, value); }
        }
        public static readonly DependencyProperty ProgTextProperty =
            DependencyProperty.Register("ProgText", typeof(String), typeof(ProgressDialog), new PropertyMetadata(""));

        public void Progres(float frac) { ProgVal = frac; }
        public void Progres(string status) {
            Dispatcher.Invoke(() =>
            {
                ProgText = status;
            });
        }

        public double dummy { get { return (double)GetValue(dummyProperty); } set { SetValue(dummyProperty, value); } }
        public static readonly DependencyProperty dummyProperty = DependencyProperty.Register("dummy", typeof(double), typeof(ProgressDialog), new PropertyMetadata(0.0));
        public void Finish(bool success)
        {
            if (success)
            {
                DoubleAnimation da = new DoubleAnimation(1, TimeSpan.FromSeconds(2));
                da.Completed += (s, e) => Close();
                BeginAnimation(dummyProperty, da);
            }
            else
            {
                bcl.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Mouse.Capture(this);
            if (Mouse.LeftButton == MouseButtonState.Pressed) return;
            Top = Owner.Top + (Owner.ActualHeight - ActualHeight) / 2;
            Left = Owner.Left + (Owner.ActualWidth - ActualWidth) / 2;
        }
    }
}
