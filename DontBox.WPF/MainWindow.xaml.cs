using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DontBox.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDontBoxView, System.Windows.Forms.IWin32Window
    {
        public ObservableCollection<DBNodeVM> rootcollection
        {
            get { return (ObservableCollection<DBNodeVM>)GetValue(rootcollectionProperty); }
            set { SetValue(rootcollectionProperty, value); }
        }
        public static readonly DependencyProperty rootcollectionProperty =
            DependencyProperty.Register("rootcollection", typeof(ObservableCollection<DBNodeVM>), typeof(MainWindow), new PropertyMetadata(new ObservableCollection<DBNodeVM>()));

        public bool loggedIn
        {
            get { return (bool)GetValue(loggedInProperty); }
            set
            {
                Dispatcher.Invoke(() =>
                {
                    SetValue(loggedInProperty, value);
                    loggedInOrLoggingIn = value || loading;
                });
            }
        }
        public static readonly DependencyProperty loggedInProperty =
            DependencyProperty.Register("loggedIn", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        WindowInteropHelper wih;
        public DBNodeVM data { set { Dispatcher.Invoke(() => { rootcollection.Clear(); rootcollection.Add(value); }); } }
        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            wih = new WindowInteropHelper(this);
            var ha = Resources["hoverActions"] as HoverActionTypeConverter;
            ha.ActionCreator = GetActions;
            Presenter.Singleton.Present(this);
        }

        public Task ShowLogin(bool show)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            DropBoxLoginMask.Visibility = System.Windows.Visibility.Visible;
            DoubleAnimation Fade = new DoubleAnimation(show ? 1.0 : 0.0, TimeSpan.FromSeconds(0.3));
            Fade.Completed += (s, e) =>
            {
                if (!show) DropBoxLoginMask.Visibility = System.Windows.Visibility.Collapsed;
                else Browsey.Visibility = System.Windows.Visibility.Visible;
                tcs.SetResult(true);
            };
            if(show) DropBoxLoginMask.Visibility = System.Windows.Visibility.Visible;
            else Browsey.Visibility = System.Windows.Visibility.Collapsed;
            DropBoxLoginMask.BeginAnimation(Control.OpacityProperty, Fade);
            return tcs.Task;
        }



        public bool loggedInOrLoggingIn
        {
            get { return (bool)GetValue(loggedInOrLoggingInProperty); }
            set { SetValue(loggedInOrLoggingInProperty, value); }
        }

        // Using a DependencyProperty as the backing store for loggedInOrLoggingIn.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty loggedInOrLoggingInProperty =
            DependencyProperty.Register("loggedInOrLoggingIn", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));



        public String LoadingTitle
        {
            get { return (String)GetValue(LoadingTitleProperty); }
            set { SetValue(LoadingTitleProperty, value); }
        }
        public static readonly DependencyProperty LoadingTitleProperty =
            DependencyProperty.Register("LoadingTitle", typeof(String), typeof(MainWindow), new PropertyMetadata("Loading"));

        public String LoadingMessage
        {
            get { return (String)GetValue(LoadingMessageProperty); }
            set { SetValue(LoadingMessageProperty, value); }
        }
        public static readonly DependencyProperty LoadingMessageProperty =
            DependencyProperty.Register("LoadingMessage", typeof(String), typeof(MainWindow), new PropertyMetadata("please wait"));

        private void loadclose_Click(object sender, RoutedEventArgs e)
        {
            ((sender as Button).Tag as TaskCompletionSource<bool>).SetResult(true);
        }

        bool loading = false;
        public Task ShowLoading(bool value, String title = null, String message = null, bool showCancel = false)
        {
            bool run = loading != value;
            loading = value;
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            Dispatcher.Invoke(() =>
            {
                loggedInOrLoggingIn = loggedIn || value;

                LoadingMessage = message;
                LoadingTitle = title;
                loadclose.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
                if (showCancel) loadclose.Tag = tcs;
                if (run)
                {
                    DoubleAnimation Fade = new DoubleAnimation(value ? 1.0 : 0.0, TimeSpan.FromSeconds(0.3));
                    Fade.Completed += (s, e) =>
                    {
                        if (!value) LoadingMask.Visibility = System.Windows.Visibility.Collapsed;
                        if(!showCancel) tcs.SetResult(true);
                    };
                    if (value) LoadingMask.Visibility = System.Windows.Visibility.Visible;
                    LoadingMask.BeginAnimation(Control.OpacityProperty, Fade);
                }
                else if(!showCancel) tcs.SetResult(true);
            });
            return tcs.Task;
        }

        public event Action login = delegate { }, logout = delegate { };
        public async Task<bool> VisitUrl(string url, VisitSuccess htmlIsSuccess)
        {
            await Task<bool>.Yield();
            TaskCompletionSource<bool> urlVisitCompleted = new TaskCompletionSource<bool>();

            // shows the login view
            await ShowLogin(true);

            // handlers
            LoadCompletedEventHandler loadedHandler = (s, e) =>
            {
                dynamic doc = Browsey.Document;
                String ihtml = "", loc = "";
                try { ihtml = doc.documentElement.InnerHtml; }
                catch { ihtml = ""; }
                try { loc = Browsey.Source.AbsoluteUri; }
                catch { loc = ""; }
                if (htmlIsSuccess(loc, ihtml))
                    urlVisitCompleted.SetResult(true);
            };
            NavigatingCancelEventHandler neh = (s, e) =>
            {
                String loc = "";
                try { loc = e.Uri.AbsoluteUri; }
                catch { loc = ""; }
                if (htmlIsSuccess(loc, ""))
                    urlVisitCompleted.SetResult(true);
            };
            RoutedEventHandler cancelHandler = (s,e) =>
            {
                urlVisitCompleted.SetResult(false);
            };
            
            // hook
            CancelButton.Click += cancelHandler;
            Browsey.LoadCompleted += loadedHandler;
            Browsey.Navigating += neh;

            // wait for result
            Browsey.Navigate(url);
            bool success = await urlVisitCompleted.Task;

            // unhook and go back to main
            CancelButton.Click -= cancelHandler;
            Browsey.LoadCompleted -= loadedHandler;
            await ShowLogin(false);
            Browsey.NavigateToString("<html></html>");
            return success;
        }

        private void Login_Click(object sender, RoutedEventArgs e) { login(); }
        public event Action<DBNodeVM> download = delegate { }, upload = delegate { }, delete = delegate { }, createFolder = delegate { }, queryChildren = delegate { };
        public IEnumerable<HoverActionVM> GetActions(DBNodeVM nd)
        {
            switch (nd.itemtype)
            {
                case DBNodeType.File:
                    yield return new HoverActionVM("Download", () => download(nd));
                    yield return new HoverActionVM("Delete", () => delete(nd));
                    break;
                case DBNodeType.Dir:
                    yield return new HoverActionVM("Create Folder", () => createFolder(nd));
                    yield return new HoverActionVM("Upload", () => upload(nd));
                    if (nd.parent == null)
                        yield return new HoverActionVM("Logout", () => logout());
                    break;
            }
        }
        private void Logout_Click(object sender, RoutedEventArgs e) { logout(); }
        private void Upload_Button_Click(object sender, RoutedEventArgs e)
        {
            var vm = (sender as FrameworkElement).DataContext as DBNodeVM;
            upload(vm);
        }
        private void Download_Button_Click(object sender, RoutedEventArgs e)
        {
            var vm = (sender as FrameworkElement).DataContext as DBNodeVM;
            download(vm);
        }
        private void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            var vm = (sender as FrameworkElement).DataContext as DBNodeVM;
            createFolder(vm);
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var vm = (sender as FrameworkElement).DataContext as DBNodeVM;
            delete(vm);
        }

        class MarshalingObservableCollection<T> : ICollection<T>, INotifyCollectionChanged, INotifyPropertyChanged
        {
            readonly ObservableCollection<T> oc;
            readonly Dispatcher invokeon;
            public MarshalingObservableCollection(Dispatcher invokeon)
            {
                oc = new ObservableCollection<T>();
                this.invokeon = invokeon;
                oc.CollectionChanged += oc_CollectionChanged;
                (oc as INotifyPropertyChanged).PropertyChanged += oc_PropertyChanged;
            }
            void oc_PropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                invokeon.Invoke(() => PropertyChanged(sender, e));
            }
            void oc_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                invokeon.Invoke(() => CollectionChanged(sender, e));
            }
            public event NotifyCollectionChangedEventHandler CollectionChanged = delegate { };
            public event PropertyChangedEventHandler PropertyChanged = delegate { };
            //DENSEPROXY ;)
            public void Add(T item) { oc.Add(item); }
            public void Clear() { oc.Clear(); }
            public bool Contains(T item) { return oc.Contains(item); }
            public void CopyTo(T[] array, int arrayIndex) { oc.CopyTo(array, arrayIndex); }
            public int Count { get { return oc.Count; } }
            public bool IsReadOnly { get { return (oc as ICollection<T>).IsReadOnly; } }
            public bool Remove(T item) { return oc.Remove(item); }
            public IEnumerator<T> GetEnumerator() { return oc.GetEnumerator(); }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return oc.GetEnumerator(); }
        }

        public ICollection<T> CreatePreferredCollection<T>()
        {
            return new MarshalingObservableCollection<T>(Dispatcher);
        }

        public Task<GPRes> GetName()
        {
            TaskCompletionSource<GPRes> tcs = new TaskCompletionSource<GPRes>();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var ofd = new GetName();
                GPRes g = new GPRes();
                var dr = ofd.ShowDialog();
                g.success = dr ?? false;
                g.path = ofd.Name;
                tcs.SetResult(g);
            }));
            return tcs.Task;
        }

        public Task<GPRes> GetFile()
        {
            TaskCompletionSource<GPRes> tcs = new TaskCompletionSource<GPRes>();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var ofd = new OpenFileDialog();
                GPRes g = new GPRes();
                var dr = ofd.ShowDialog(this);
                g.success = dr ?? false;
                g.path = ofd.FileName;
                tcs.SetResult(g);
            }));
            return tcs.Task;
        }

        public Task<GPRes> GetPath()
        {
            TaskCompletionSource<GPRes> tcs = new TaskCompletionSource<GPRes>();
            Dispatcher.BeginInvoke(new Action(() =>
                {
                    var ofd = new System.Windows.Forms.FolderBrowserDialog();
                    GPRes g = new GPRes();
                    var dr = ofd.ShowDialog(this);
                    g.success = dr == System.Windows.Forms.DialogResult.OK;
                    g.path = ofd.SelectedPath;
                    tcs.SetResult(g);
                }));
            return tcs.Task;
        }

        public IProgressView CreateProgress()
        {
            return new ProgressDialog(this);
        }

        public IntPtr Handle { get { return wih.Handle; } }

        private void RowExpander_Checked(object sender, RoutedEventArgs e)
        {
            var nd = (sender as FrameworkElement).DataContext as DBNodeVM;
            Task.Factory.StartNew(() =>
                {
                    if (nd.stateOfChildren == DBChildState.Queryable)
                        queryChildren(nd);
                    else foreach (var c in nd.children)
                            if (c.stateOfChildren == DBChildState.Queryable)
                                queryChildren(c);
                });
        }
    }
    public class BoolToVisHidden : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class CheckedToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? "-" : "+";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class HoverActionVM
    {
        public HoverActionVM(String name, Action action)
        {
            this.name = name;
            this.action = new Command(action);
        }
        public String name { get; set; }
        public ICommand action { get; set; }
    }
    public class Command : ICommand
    {
        Action action;
        public Command(Action action) { this.action = action; }
        public bool CanExecute(object parameter) { return true; }
        public event EventHandler CanExecuteChanged;
        public void Execute(object parameter) { action(); }
    }
    public class HoverActionTypeConverter : IValueConverter
    {
        public Func<DBNodeVM, IEnumerable<HoverActionVM>> ActionCreator = n => new HoverActionVM[0];
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return null;
            var nd = ((DBNodeVM)value);
            return new List<HoverActionVM>(ActionCreator(nd));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class LayoutHackConverter :IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return new Thickness((double)values[0] - (double)values[1] - (double)parameter, 0, 0, 0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class MockData : DependencyObject
    {
        public ObservableCollection<DBNodeVM> rootcollection
        {
            get { return (ObservableCollection<DBNodeVM>)GetValue(rootcollectionProperty); }
            set { SetValue(rootcollectionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for rootcollection.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty rootcollectionProperty =
            DependencyProperty.Register("rootcollection", typeof(ObservableCollection<DBNodeVM>), typeof(MockData), new PropertyMetadata(new ObservableCollection<DBNodeVM>()));
    }
    public class ColorToSolidBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return new SolidColorBrush((Color)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class ExpandRowAlsoIfQueryableConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {

            bool hasChildren = (bool)values[0];
            DBChildState cs = values[1] is DBChildState ? (DBChildState)values[1] : DBChildState.Loaded;
            return hasChildren || cs == DBChildState.Queryable ? Visibility.Visible : Visibility.Hidden;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
