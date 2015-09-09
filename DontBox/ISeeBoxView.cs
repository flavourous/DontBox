using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DontBox
{
    class ProgClass
    {
        public float val;
        public String msg;
    }
    class Progger : IProgress<ProgClass>
    {
        readonly IProgressView pv;
        public Progger(IProgressView pv)
        {
            this.pv = pv;
        }
        public void Report(ProgClass value)
        {
            pv.Progres(value.val);
            pv.Progres(value.msg);
        }
        public async Task<bool> Run(Task<bool> task)
        {
            pv.Start();
            bool suc = await task;
            pv.Finish(suc);
            return suc;
        }
    }
    public enum DBChildState { Loaded, Queryable, Querying };
    public enum DBNodeType { File, Dir };
    public class DBNodeVM : INotifyPropertyChanged
    {
        protected DBNodeVM() { }
        public String name { get; set; }
        public long fileSize { get; set; }
        public DBNodeVM parent { get; set; }
        private ICollection<DBNodeVM> _children;
        public ICollection<DBNodeVM> children
        {
            get { return _children; }
            set { _children = value; OnPropertyChanged("children"); }
        }
        private DBChildState _stateOfChildren;
        public DBChildState stateOfChildren
        {
            get { return _stateOfChildren; }
            set { _stateOfChildren = value; OnPropertyChanged("stateOfChildren"); }
        }
        public DBNodeType itemtype { get; set; }

        // opc
        protected void OnPropertyChanged(String property) { PropertyChanged(this, new PropertyChangedEventArgs(property)); }
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
    }
    class DBNodeVM_L : DBNodeVM
    {
        public DBNodeVM_L() : base() { }
        public SeeMetaData SeeMetaData;
    }
    static class MVVMMappingExtensions
    {
        public static SeeMetaData GetModel(this DBNodeVM vanilla)
        {
            return ((DBNodeVM_L)vanilla).SeeMetaData;
        }
        static Dictionary<SeeMetaData, DBNodeVM> backreference = new Dictionary<SeeMetaData, DBNodeVM>(); // sigh...
        public static void Ref(this SeeMetaData m, DBNodeVM v)
        {
            backreference.Add(m, v);
        }
        public static void ClearRefs()
        {
            backreference.Clear();
        }
        public static DBNodeVM GetViewModel(this SeeMetaData model)
        {
            return backreference.ContainsKey(model) ? backreference[model] : null;
        }
    }
    public delegate bool VisitSuccess(String url, String pageContent);
    delegate Task<bool> UrlVisitor(String url, VisitSuccess htmlIsSuccess);
    public struct GPRes { public String path; public bool success; }
    public interface IDontBoxView
    {
        ICollection<T> CreatePreferredCollection<T>();
        Task<GPRes> GetName();
        Task<GPRes> GetPath();
        Task<GPRes> GetFile();
        Task<bool> VisitUrl(String url, VisitSuccess htmlIsSuccess); // this kinda breaks the mvp pattern, but eh.
        IProgressView CreateProgress();
        Task ShowLoading(bool value, String title = null, String message = null, bool manualClose = false);
        bool loggedIn { set; }
        DBNodeVM data { set; }
        event Action login, logout;
        event Action<DBNodeVM> download, upload, delete, createFolder, queryChildren;
    }
    public interface IProgressView
    {
        void Start();
        void Progres(float frac);
        void Progres(String status);
        void Finish(bool success);
    }
}
