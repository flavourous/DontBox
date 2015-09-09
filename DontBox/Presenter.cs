using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DontBox
{
    public class Presenter 
    {

        // Singleton Pattern
        private Presenter() { }
        private static Presenter _Singleton;
        public static Presenter Singleton { get { return _Singleton ?? (_Singleton = new Presenter()); } }
        public static DBChildState q { get { return DBChildState.Querying; } }

        // Class
        IDontBoxView view;
        IDontBoxModel model;
        public void Present(IDontBoxView to)
        {
            //model = new Mocks.MockModel();
            model = new MyDirectDropBoxAPI();
            model.ModelChanged += model_ModelChanged;
            view = to;
            view.login += view_login;
            view.logout += view_logout;
            view.download += view_download;
            view.upload += view_upload;
            view.createFolder += view_createFolder;
            view.delete += view_delete;
            view.queryChildren += view_queryChildren;

            if (model.HaveRemembered) DoLogin(true); // no, async please.
        }

        async void view_queryChildren(DBNodeVM obj)
        {
            await QueryChildren(obj);
        }
        async Task QueryChildren(DBNodeVM obj)
        {
            obj.stateOfChildren = DBChildState.Querying;
            var omod = obj.GetModel();
            await omod.ExpandContents();
            obj.stateOfChildren = DBChildState.Loaded;
            obj.children = ConvertNode(omod, true).children; // has notifiy
        }
        //not async cos meh
        void view_logout()
        {
            view.loggedIn = false;
            view.data = null;
        }
        // it's ok to have an async void on an event handler, so I hear.
        async void view_delete(DBNodeVM obj)
        {
            Progger pr = new Progger(view.CreateProgress());
            bool suc = await pr.Run(model.Delete(obj.GetModel(), pr));
        }
        async void view_createFolder(DBNodeVM obj)
        {
            if (obj.stateOfChildren == DBChildState.Queryable)
                await QueryChildren(obj);

            var res = await view.GetName();
            if (!res.success) return;

            Progger pr = new Progger(view.CreateProgress());
            bool suc = await pr.Run(model.CreateFolder(res.path, obj.GetModel(), pr));
        }
        async void view_upload(DBNodeVM obj)
        {
            if (obj.stateOfChildren == DBChildState.Queryable)
                await QueryChildren(obj);

            var res = await view.GetFile();
            if (!res.success) return;

            Progger pr = new Progger(view.CreateProgress());
            bool dlSuccess = await pr.Run(model.Upload(res.path, obj.GetModel(), pr));
        }
        async void view_download(DBNodeVM obj)
        {
            var res = await view.GetPath();
            if(!res.success) return;

            Progger pr = new Progger(view.CreateProgress());
            bool dlSuccess = await pr.Run(model.Download(obj.GetModel(), res.path, pr));
        }
        async void view_login() 
        {
            await DoLogin(false);
        }
        async Task DoLogin(bool remembered)
        {
            await view.ShowLoading(true, "Loading", "Please wait");
            SeeMetaData got = null;
            DBNodeVM converted = null;
            Exception ee = null;
            try
            {
                bool suc = remembered;
                if (remembered) model.LoginWithRemembered();
                else suc = await model.LogIn(view.VisitUrl);
                if(suc) got = await model.ListFiles();
            }
            catch (Exception e)
            {
                ee=e;
                got = null;
            }
            view.loggedIn = got != null;
            if (got != null)
            {
                CancellationToken ct = new CancellationToken();
                converted = await Task.Run<DBNodeVM>(() => GenModels(got), ct);
            }
            if(ee != null)
            {
                await view.ShowLoading(true, "Error: " + ee.GetType().ToString(), ee.ToString(), true);
            }
            await view.ShowLoading(false);
            Console.WriteLine("Before view data set");
            view.data = converted;
            Console.WriteLine("After view data set");
        }

        void model_ModelChanged(ModelChangedEventArgs ea)
        {
            // we should have this listed already, perhaps if not, we fire a re-list event
            var parent = ea.changed.Parent.GetViewModel();
            var item = ea.changed.GetViewModel();
            if(parent == null || (item == null && ea.changetype == ModelChangedEventType.Removed))
            {
                // report a broken-tree event somehwere
                // shit...re-request it.  might be running operations, need to use that cancellation token.
            }
            else
            {
                switch (ea.changetype)
                {
                    case ModelChangedEventType.Added:
                        item = GetNode(ea.changed);
                        item.parent = parent;
                        parent.children.Add(item);
                        break;
                    case ModelChangedEventType.Removed:
                        parent.children.Remove(item);
                        break;
                }
            }
        }

        DBNodeVM GenModels(SeeMetaData root)
        {
            return ConvertNode(root);
        }
        DBNodeVM ConvertNode(SeeMetaData md, bool dontrefroot = false)
        {
            var thisnode = GetNode(md, dontrefroot);
            if (md.Contents != null)
            {
                foreach (var mdc in md.Contents)
                {
                    var c = ConvertNode(mdc, false);
                    c.parent = thisnode;
                    thisnode.children.Add(c);
                }
            }
            return thisnode;
        }
        DBNodeVM GetNode(SeeMetaData md, bool dontref = false)
        {
            DBNodeVM_L thisnode = new DBNodeVM_L();
            thisnode.name = md.Name;
            thisnode.fileSize = md.Size;
            thisnode.children = view.CreatePreferredCollection<DBNodeVM>();
            thisnode.SeeMetaData = md;
            thisnode.itemtype = md.Is_Dir ? DBNodeType.Dir : DBNodeType.File;
            thisnode.stateOfChildren = md.Is_Dir && md.ExpandContents != null ? DBChildState.Queryable : DBChildState.Loaded;
            if(!dontref) md.Ref(thisnode);
            return thisnode;
        }
    }
    
   
    

}
