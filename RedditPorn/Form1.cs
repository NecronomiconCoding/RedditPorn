using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RedditSharp;
using RedditSharp.Things;
using Timer = System.Windows.Forms.Timer;

namespace RedditPorn
{
    public partial class Form1 : Form
    {
        private static string _subreddit;
        private static Reddit _reddit;
        private static Subreddit _curSubreddit;
        private static readonly Timer NextUodate = new Timer();
        private static readonly Dictionary<string, string> ImageList = new Dictionary<string, string>();
        private static bool _running;
        private static bool _hidden;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

        private const uint SpiSetdeskwallpaper = 20;
        private const uint SpifUpdateinifile = 0x1;

        public Form1()
        {
            InitializeComponent();
            comboBox1.SelectedIndex = 0;
            notifyIcon1.Visible = true;
            Resize += delegate
            {
                if (FormWindowState.Minimized == WindowState)
                {
                    _hidden = true;
                    notifyIcon1.ShowBalloonTip(60);
                    Hide();
                }
            };
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            if (_running)
            {
                using (var s = new StreamWriter(Directory.GetCurrentDirectory() + "\\" + "imglist.txt"))
                {
                    foreach (var entry in ImageList)
                        s.WriteLine(entry.Key + "§" + entry.Value);
                    s.Flush();
                }
                NextUodate.Stop();
                ImageList.Clear();
                startButton.Text = "Start";
                statusLabel.Text = "Not Running";
                linkLabel1.Visible = false;
                linkLabel1.Links.Clear();
                _running = false;
                return;
            }

            _running = true;
            statusLabel.Text = "Running...";
            startButton.Text = "Stop";
            Hide();
            _hidden = true;
            notifyIcon1.ShowBalloonTip(60);
            if (File.Exists(Directory.GetCurrentDirectory() + "\\" + "imglist.txt"))
                foreach (var line in File.ReadAllLines(Directory.GetCurrentDirectory() + "\\" + "imglist.txt"))
                    ImageList.Add(line.Split('§')[0], line.Split('§')[1]);

            _reddit = new Reddit();
            _subreddit = subredditTextBox.Text;
            _curSubreddit = _reddit.GetSubreddit($"/r/{_subreddit}");
            NextUodate.Interval = 1000*60*int.Parse(updateTimeinSecTextBox.Text);
            NextUodate.Tick += CycleImage;
            CycleImage(null, null);
            NextUodate.Start();
        }

        private void CycleImage(object sender, EventArgs e)
        {
            if (!CheckOnImages())
            {
                MessageBox.Show("Was not able to get pictures. Trying again next interval.");
                return;
            }
            using (var iClient = new WebClient())
            {
                iClient.DownloadFile(new Uri(ImageList.Values.First()),
                    Directory.GetCurrentDirectory() + "\\" + ImageList.Keys.First());
            }
            SystemParametersInfo(SpiSetdeskwallpaper, 0, Directory.GetCurrentDirectory() + "\\" + ImageList.Keys.First(),
                SpifUpdateinifile);
            statusLabel.Text = @"Running...";
            linkLabel1.Visible = true;
            linkLabel1.Text = "Current Picture";
            linkLabel1.Links.Add(0, linkLabel1.Text.Length, ImageList.Values.First());
            Task.Factory.StartNew(delegate
            {
                Thread.Sleep(3000);
                Debug.Write(Directory.GetCurrentDirectory() + "\\" + ImageList.Keys.First());

                File.Delete(Directory.GetCurrentDirectory() + "\\" + ImageList.Keys.First());

                ImageList.Remove(ImageList.Keys.First());
            });
        }

        private bool CheckOnImages()
        {
            var selectedSortMethod = comboBox1.SelectedIndex;
            if (ImageList.Keys.Any()) return ImageList.Keys.Any();
            if (selectedSortMethod == 0)
            {
                foreach (var post in _curSubreddit.GetTop(FromTime.All).Take(60))
                {
                    if (ImageList.ContainsValue(post.Url.AbsolutePath) || ImageList.Count >= 10) continue;
                    var url = post.Url.AbsoluteUri;
                    if (!url.Contains(".jpg") && !url.Contains(".png")) continue;
                    ImageList.Add(Path.GetFileName(post.Url.AbsolutePath), url);
                }
            }
            else
            {
                foreach (var post in _curSubreddit.Hot.Take(60))
                {
                    if (!ImageList.ContainsValue(post.Url.AbsolutePath) || ImageList.Count <= 10) continue;
                    var url = post.Url.AbsoluteUri;
                    if (!url.Contains(".jpg") && !url.Contains(".png")) continue;
                    ImageList.Add(Path.GetFileName(post.Url.AbsolutePath), url);
                }
            }

            return ImageList.Keys.Any();
        }

        private void subRedditUpdateButton_Click(object sender, EventArgs e)
        {
            _subreddit = subredditTextBox.Text;
            if (_reddit != null)
                _curSubreddit = _reddit.GetSubreddit($"/r/{_subreddit}");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            NextUodate.Interval = 1000*60*int.Parse(updateTimeinSecTextBox.Text);
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_hidden)
            {
                Show();
                _hidden = false;
            }

            else
            {
                Hide();
                _hidden = true;
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(e.Link.LinkData.ToString());
        }
    }
}
