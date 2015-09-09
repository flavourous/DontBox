using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DontBox;

namespace DontBox.Mocks
{
    static class SeeMetaDataMockExtensions
    {
        public static SeeMetaData AddFileLocally(this SeeMetaData md, String name, long size)
        {
            var ret = new SeeMetaData() { Path = md.Path + "/" + name, Size = size, Parent = md };
            (md.Contents ?? delay[md]).Add(ret);
            return ret;
        }
        static Dictionary<SeeMetaData, List<SeeMetaData>> delay = new Dictionary<SeeMetaData, List<SeeMetaData>>();
        public static SeeMetaData AddDirectoryLocally(this SeeMetaData md, String name)
        {
            var ret = new SeeMetaData()
            {
                Path = md.Path + "/" + name,
                Is_Dir = true,
                Contents = null,
                Parent = md 
            };
            delay[ret] = new List<SeeMetaData>();
            ret.ExpandContents = () => Task.Run(async () => 
            { 
                await Task.Delay(5000); 
                ret.Contents = delay[ret];
            });
            (md.Contents ?? delay[md]).Add(ret);
            return ret;
        }
        public static SeeMetaData CreateRootLocally(String name)
        {
            return new SeeMetaData() { Path = "/" + name, Is_Dir = true, Contents = new List<SeeMetaData>() };
        }
        static Dictionary<int, String> postFixes = new Dictionary<int, string> {
                {0, "b"},
                {1, "kb"},
                {2, "Mb"},
                {3, "Gb"},
                {4, "Tb"},
            };
        static String SizeString(long bytes)
        {
            float bfloat = bytes;
            int fx = 0;
            while (bfloat > 1024f)
            {
                bfloat /= 1024;
                fx++;
            }

            return bfloat.ToString("F2") + postFixes[fx];
        }
    }
    class MockModel : IDontBoxModel
    {
        public bool LoggedIn { get; private set; }
        public async Task<bool> LogIn(UrlVisitor visit)
        {
            // Build some dummy pages
            String dLog = "<html><a href='file://{0}'>Test Login</a></html>";
            String dSuc = "<html>Success!</html>";
            String fLog = Path.GetTempFileName() + ".html";
            String fSuc = Path.GetTempFileName() + ".html";
            dLog = String.Format(dLog, fSuc);
            File.WriteAllText(fLog, dLog);
            File.WriteAllText(fSuc, dSuc);
            if (!await visit("file://" + fLog, HtmlIsSuccess))
                return false;
            remembered = new SeeUserLogin() { Token = "TOKEY", Secret = "SECREY" };
            await Task.Delay(2000);
            return true;
        }
        bool HtmlIsSuccess(String url, String html)
        {
            return html.Contains("Success!"); // lol hacks bloddy stupid api
        }

        SeeUserLogin remembered;
        public bool HaveRemembered { get { return remembered != null; } }

        public void LoginWithRemembered()
        {
            // lol ok.
        }

        Random d = new Random();
        public Task<SeeMetaData> ListFiles()
        {
            return Task.Run<SeeMetaData>(() =>
            {
                // build some fake SeeMetaData
                SeeMetaData rootdir = SeeMetaDataMockExtensions.CreateRootLocally("rooty");
                // some files on root
                for (int i = 0; i < 6; i++)
                    rootdir.AddFileLocally("Filey" + i, d.Next(2, 1024 * 1024 * 1024));
                // some folders on root
                for (int i = 0; i < 3; i++)
                {
                    SeeMetaData fld = rootdir.AddDirectoryLocally("Foldey" + i);
                    // files in this folder
                    for (int j = 0; j < 4; j++)
                        fld.AddFileLocally("Fileydeep" + j, d.Next(2, 1024 * 1024 * 1024));
                    // folders in this folder
                    for (int j = 0; j < 4; j++)
                        fld.AddDirectoryLocally("DirrrDeep-" + j);
                }
                return rootdir;
            });
        }

        public event ModelChangedEventHandler ModelChanged = delegate { };
        public Task<bool> Download(SeeMetaData file, String to, IProgress<ProgClass> progress)
        {
            return FakeTask(() =>
            {
                var dlf = Path.Combine(to, file.Name);
                File.WriteAllText(dlf, "lol no fie!l");
            },
            "Downloading " + file.Name + " to " + to,
            "Finished downloading " + file.Name + " to " + to,
            "Error downloading: ", progress, 5000);
        }

        public Task<bool> Upload(String file, SeeMetaData to, IProgress<ProgClass> progress)
        {
            return FakeTask(() =>
            {
                FileInfo fi = new FileInfo(file);
                var nf = to.AddFileLocally(fi.Name, fi.Length);
                ModelChanged(new ModelChangedEventArgs() { changed = nf, changetype = ModelChangedEventType.Added });
            },

            "Uploading " + file + " to " + to.Name,
            "Finished uploading " + file + " to " + to.Name,
            "Error uploading: ", progress, 4000);
        }

        public Task<bool> CreateFolder(String folder, SeeMetaData inside, IProgress<ProgClass> progress)
        {
            return FakeTask(() =>
            {
                var nf = inside.AddDirectoryLocally(folder);
                ModelChanged(new ModelChangedEventArgs() { changed = nf, changetype = ModelChangedEventType.Added });
            },
            "Creating folder " + folder + " in " + inside.Name,
            "Created folder " + folder + " in " + inside.Name,
            "Error creating folder: ", progress, 500);
        }
        public Task<bool> Delete(SeeMetaData file, IProgress<ProgClass> progress)
        {
            // error now.
            if (file.Is_Dir)
            {
                progress.Report(new ProgClass() { val = 0, msg = "Error: Deleting directories not supported." });
                return Task.FromResult<bool>(false);
            }

            String m1 = "Deleting " + file.Name, m2 = "Deleted " + file.Name, m3 = "Error deleting: ";
            return FakeTask(() =>
            {
                var pf = file.Parent;
                pf.Contents.Remove(file);
                ModelChanged(new ModelChangedEventArgs() { changed = file, changetype = ModelChangedEventType.Removed });
            }, m1, m2, m3, progress, 300);
        }

        Task<bool> FakeTask(Action worky, String init, String win, String fail, IProgress<ProgClass> prog, int delay)
        {
            ProgClass pc = new ProgClass();
            pc.val = 0;
            pc.msg = init;
            prog.Report(pc);
            return Task.Run<bool>(async () =>
            {
                bool suc = false;
                try
                {
                    worky();
                    await FakeProcess(prog, pc, delay);
                    pc.msg = win;
                    suc = true;
                }
                catch (IOException e)
                {
                    pc.msg = fail + e.ToString();
                }
                prog.Report(pc);
                return suc;
            });
        }

        async Task FakeProcess(IProgress<ProgClass> progress, ProgClass pc, int delay)
        {
            int rep = 1000 / 60; // 60fps
            int ud = delay / rep;
            for (int i = 0; i < ud; i++)
            {
                pc.val = i / (float)ud;
                progress.Report(pc);
                await Task.Delay(rep);
            }
            pc.val = 1f;
        }
    }
}
