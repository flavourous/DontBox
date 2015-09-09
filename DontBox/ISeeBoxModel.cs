using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DontBox
{
    enum ModelChangedEventType { Added, Removed };
    delegate void ModelChangedEventHandler(ModelChangedEventArgs ea);
    class ModelChangedEventArgs
    {
        public SeeMetaData changed;
        public ModelChangedEventType changetype;
    }
    class SeeMetaData
    {
        public String Name { get { return Path.Length > 1 ? Path.Substring(Path.LastIndexOf('/') + 1) : ""; } }
        public bool Is_Dir;
        public String Path;
        public long Size;
        public SeeMetaData Parent;
        public IList<SeeMetaData> Contents;
        public Func<Task> ExpandContents;
    }
    class SeeUserLogin
    {
        public String Token, Secret;
    }
    interface IDontBoxModel
    {
        bool LoggedIn { get; }
        Task<bool> LogIn(UrlVisitor visit);
        bool HaveRemembered { get; }
        void LoginWithRemembered();
        Task<SeeMetaData> ListFiles();
        Task<bool> Download(SeeMetaData file, String to, IProgress<ProgClass> progress);
        Task<bool> Upload(String file, SeeMetaData to, IProgress<ProgClass> progress);
        Task<bool> CreateFolder(String folder, SeeMetaData inside, IProgress<ProgClass> progress);
        Task<bool> Delete(SeeMetaData file, IProgress<ProgClass> progress);
        event ModelChangedEventHandler ModelChanged;
    }
}
