using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading;
using System.ComponentModel;
using System.Reflection;

namespace DontBox
{
    // erm
    [Serializable]
    public class Account
    {
        public String name;
        public String token;
    }
    [Serializable]
    public class RememberedData { public List<Account> accounts = new List<Account>(); }
    class MyDirectDropBoxAPI : IDontBoxModel
    {
        RememberedData remembered;
        const String cfn = "rememberedAccounts.xml";
        XmlSerializer xs = XmlSerializer.FromTypes(new Type[] { typeof(RememberedData) })[0];
        //XmlSerializer xs = new XmlSerializer(typeof(RememberedData));
        public MyDirectDropBoxAPI()
        {
            FileStream fs = null;
            try { fs = new FileStream(cfn, FileMode.Open); }
            catch (FileNotFoundException fne) { fs = null; }
            remembered = fs != null ? (RememberedData)xs.Deserialize(fs) : new RememberedData();
            if (fs != null) fs.Close();
        }

        // memberies
        public event ModelChangedEventHandler ModelChanged = delegate { };
        public bool LoggedIn { get { return wc != null; } }
        public bool HaveRemembered { get { return remembered.accounts.Count > 0; } }

        const String APPNAME = "DontBox";
        const String KEY = "fknc0trjadwi92i";
        const String SECRET = "kzj5a59rrxhqco3";

        // stuffies
        class DWebClient : WebClient
        {
            public DWebClient(String token)
            {
                Headers["Authorization"] = "Bearer " + token;
            }
            object clock = new object();
            public String GetString(String url)
            {
                lock (clock) return DownloadString(url);
            }
            // FIXME Use a webclient factory. Reason for semaphore and async methods is because sync method doesnt report progress...
            public void GetData(String url, String file, Action<long> progress)
            {
                AutoResetEvent are = new AutoResetEvent(false);
                DownloadProgressChangedEventHandler h = (s, e) => progress(e.BytesReceived);
                AsyncCompletedEventHandler c = null;
                c = (s, e) => { are.Set(); DownloadFileCompleted -= c; };
                lock (clock)
                {
                    DownloadProgressChanged += h;
                    DownloadFileCompleted += c; // fucks sake...
                    DownloadFileAsync(new Uri(url), file);
                    are.WaitOne();
                    DownloadProgressChanged -= h;
                }
            }
            
            public bool PutData(String url, String file, Action<long> progress, out String response)
            {
                AutoResetEvent are = new AutoResetEvent(false);
                UploadProgressChangedEventHandler h = (s, e) => progress(e.BytesSent);
                UploadFileCompletedEventHandler c = null;
                UploadFileCompletedEventArgs ret = null;
                c = (s, e) =>
                {
                    UploadFileCompleted -= c;
                    ret = e;
                    are.Set();
                };
                lock (clock)
                {
                    UploadProgressChanged += h;
                    UploadFileCompleted += c; // fucks sake...
                    UploadFileAsync(new Uri(url), "PUT", file);
                    are.WaitOne();
                    UploadProgressChanged -= h;
                }
                bool suc = true;
                try
                {
                    response = Encoding.ASCII.GetString(ret.Result ?? new byte[0]);
                }
                catch(Exception e)
                {
                    suc = false;
                    // look for a WebExceptoin
                    Exception pe = e;
                    while (!(pe is WebException) && pe.InnerException != null)
                        pe = pe.InnerException;
                    if(pe is WebException)
                    {
                        WebException pex = pe as WebException;
                        Stream rs = pex.Response.GetResponseStream();
                        byte[] sd = new byte[rs.Length];
                        rs.Read(sd, 0, (int)rs.Length);
                        response = Encoding.ASCII.GetString(sd);
                    }
                    else throw;
                }
                return suc;
            }
        }
        DWebClient wc;
        enum RT {Token, Code};
        String DB_OAUTH2_AUTHORISE(RT response_type, String client_id, String redirect_uri) // nah options 
        {
            var root = "https://www.dropbox.com/1/oauth2/authorize?response_type={0}&client_id={1}&redirect_uri={2}";
            return string.Format(root, response_type == RT.Code ? "code" : "token", client_id, redirect_uri);
        }
        const String DB_ACCOUNT_INFO = "https://api.dropboxapi.com/1/account/info";
        String DB_METADATA(String path)
        {
            return "https://api.dropboxapi.com/1/metadata/auto/" + path;
        }
        const String resultUri = "dontbox://loginresult";
        string DB_DOWNLOAD(String path) { return "https://content.dropboxapi.com/1/files/auto/" + path; }
        string DB_UPLOAD(String filepath) { return "https://content.dropboxapi.com/1/files_put/auto/" + filepath; }
        string DB_CREATEFOLDER(String path) { return "https://api.dropboxapi.com/1/fileops/create_folder?root=auto&path=" + path; }
        string DB_DELETE(String path) { return "https://api.dropboxapi.com/1/fileops/delete/?root=auto&path=" + path; }

        // methodies
        public async Task<bool> LogIn(UrlVisitor visit)
        {
            await visit(DB_OAUTH2_AUTHORISE(RT.Token, KEY, resultUri),
                            (uri, data) =>
                            {
                                if (uri.StartsWith(resultUri))
                                {
                                    var parms = uri.Substring(uri.IndexOf('#') + 1);
                                    var args = parms.Split('&');
                                    Dictionary<String, String> vals = new Dictionary<string, string>();
                                    foreach (var arg in args)
                                    {
                                        int ei = arg.IndexOf('=');
                                        var key = arg.Substring(0, ei);
                                        var val = arg.Substring(ei + 1);
                                        vals[key] = val;
                                    }
                                    wc = new DWebClient(vals["access_token"]);
                                    JObject info = JObject.Parse(wc.GetString(DB_ACCOUNT_INFO));
                                    String em = info["email"].Value<String>();
                                    if(remembered.accounts.FindAll(a => a.name == em).Count == 0)
                                    {
                                        remembered.accounts.Add(new Account() { name = em, token = vals["access_token"] });
                                        FileStream fs = new FileStream(cfn, FileMode.OpenOrCreate);
                                        xs.Serialize(fs, remembered);
                                        fs.Close();
                                    }
                                    return true;
                                }
                                return false;
                            });
            return false;
        }
        public void LoginWithRemembered()
        {
            wc = new DWebClient(remembered.accounts[0].token);
        }
        public Task<SeeMetaData> ListFiles()
        {
            return Task.Run<SeeMetaData>(() => {
                var mdurl = DB_METADATA("");
                var resp = wc.GetString(mdurl);
                var root = JsonToModel(JObject.Parse(resp), null);
                ContentExpanders(root);
                return root;
            });
        }
        void ContentExpanders(SeeMetaData root)
        {
            bool folded = false;
            var rc = root.Contents;
            for (int i = 0; i < rc.Count; i++)
                if (rc[i].Is_Dir)
                {
                    if (rc[i].Contents == null) // needy scan!
                    {
                        folded = true;
                        var capture = rc[i];
                        capture.ExpandContents = () =>
                        {
                            return Task.Run(() =>
                            {
                                // this is a dir, we needa add it childrens
                                var nroot = JsonToModel(JObject.Parse(wc.GetString(DB_METADATA(capture.Path))), capture.Parent);
                                capture.Contents = nroot.Contents; // copycopy
                                ContentExpanders(capture);
                            });
                        };
                    }
                }
        }
        SeeMetaData JsonToModel(JObject md, SeeMetaData parent)
        {
            SeeMetaData ret = new SeeMetaData();
            ret.Is_Dir = md["is_dir"].Value<bool>();
            ret.Path = md["path"].Value<String>();
            ret.Size = ParseSize(md["size"].Value<String>());
            ret.Parent = parent;
            if (md["contents"] != null)
            {
                ret.Contents = new List<SeeMetaData>();
                foreach (JObject j in md["contents"])
                    ret.Contents.Add(JsonToModel(j, ret));
            }
            return ret;
        }
        // Dropbox, for giving me a formatted string instead of an int, for file size, I hate you.
        int ParseSize(String sizeString)
        {
            var ts = sizeString.Trim();
            char[] ns = new char[ts.Length];
            int i = 0;
            foreach (var ic in ts.TakeWhile<char>(c => c < 58))
                ns[i++] = ic;
            String istr = new string(ns, 0, i);
            String sstr = ts.Substring(i);
            var b = double.Parse(istr);
            double ret = 1.0;
            switch (sstr.Trim().ToLower())
            {
                default:
                case "b": ret = b; break;
                case "kb": ret = b * 1024; break;
                case "mb": ret = b * 1024 * 1024; break;
                case "gb": ret = b * 1024 * 1024 * 1024; break;
                case "tb": ret = b * 1024 * 1024 * 1024 * 1024; break;
            }
            return (int)ret;
        }
        // commandies
        public Task<bool> Download(SeeMetaData file, string to, IProgress<ProgClass> progress)
        {
            ProgClass pc = new ProgClass() { msg = "Downloading " + file.Path, val = 0f };
            progress.Report(pc);
            return Task.Run<bool>(() =>
            {
                wc.GetData(DB_DOWNLOAD(file.Path), Path.Combine(to, file.Name), f => { pc.val = f/(float)file.Size; progress.Report(pc); });
                return true;
            });
        }
        public Task<bool> Upload(string file, SeeMetaData to, IProgress<ProgClass> progress)
        {
            ProgClass pc = new ProgClass() { msg = "Uploading " + file, val = 0f };
            progress.Report(pc);
            return Task.Run<bool>(() =>
            {
                var fi = new FileInfo(file);
                long tot = fi.Length;
                String name = fi.Name, resp = "Unknown Error";
                if (wc.PutData(DB_UPLOAD(to.Path.TrimStart('/') + name), file, f => { pc.val = f / (float)tot; progress.Report(pc); }, out resp))
                {
                    var nmd = JsonToModel(JObject.Parse(resp), to);
                    to.Contents.Add(nmd);
                    ModelChanged(new ModelChangedEventArgs() { changed = nmd, changetype = ModelChangedEventType.Added });
                    return true;
                }
                else
                {
                    pc.msg = resp;
                    progress.Report(pc);
                    return false;
                }
            });
        }
        public Task<bool> CreateFolder(string folder, SeeMetaData inside, IProgress<ProgClass> progress)
        {
            String fp = inside.Path + "/" + folder;
            ProgClass pc = new ProgClass() { msg = "Creating " + fp, val = 0f };
            progress.Report(pc);
            return Task.Run<bool>(() =>
            {
                String newmetadata = wc.GetString(DB_CREATEFOLDER(fp));
                var nmd = JsonToModel(JObject.Parse(newmetadata), inside);
                nmd.Contents = new List<SeeMetaData>(); // is empty...
                inside.Contents.Add(nmd);
                ModelChanged(new ModelChangedEventArgs() { changed = nmd, changetype = ModelChangedEventType.Added });
                pc.val = 1f;
                progress.Report(pc);
                return true;
            });
        }
        public Task<bool> Delete(SeeMetaData file, IProgress<ProgClass> progress)
        {
            ProgClass pc = new ProgClass() { msg = "Deleting " +file.Name, val = 0f };
            progress.Report(pc);
            return Task.Run<bool>(() =>
            {
                wc.GetString(DB_DELETE(file.Path));
                file.Parent.Contents.Remove(file);
                ModelChanged(new ModelChangedEventArgs() { changed = file, changetype = ModelChangedEventType.Removed });
                pc.val = 1f;
                progress.Report(pc);
                return true;
            });
        }
    }
}
