using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace MuteMic
{
    internal class Program
    {
        static NotifyIcon notifyIcon = new NotifyIcon();
        static MMDevice microphone;
        static AudioSessionControl activeSession;

        static bool microphoneIsMutted;
        static bool micDisabled;
        static bool firstTime = true;
        static bool notifications;

        static bool exit;

        static void Main(string[] args)
        {
            //Checking args
            var pathConfigFile = Path.Combine(Application.ExecutablePath.Substring(0, Application.ExecutablePath.LastIndexOf("\\")), "config.cfg");
            if (!args.Any() && !File.Exists(pathConfigFile))
            {
                Console.WriteLine("No name of mic selected, put the name of the mic as first arg");
                return;
            }

            if (!args.Any())
            {
                args = new string[]
                {
                    File.ReadAllText(pathConfigFile)
                };
            }

            //Finding micro
            MMDeviceCollection deviceCollection = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            microphone = deviceCollection.First(d => d.DeviceFriendlyName.ToLowerInvariant().Contains(args[0].ToLowerInvariant()) &&
                d.AudioSessionManager.Sessions.Count >= 1);

            if (microphone == null)
            {
                Console.WriteLine("No microphone found with sessions");
                return;
            }

            //Checking if the app is running
            bool isNewInstance;
            Mutex singleMutex = new Mutex(true, "MuteMic", out isNewInstance);

            if (!isNewInstance)
            {
                //If is running only change the mic state
                SwitchMuteUnMute();
                return;
            }

            //Starting tray icon
            notifyIcon.Icon = Icon.ExtractAssociatedIcon("icons/off.ico");
            notifyIcon.DoubleClick += (obj, ev) => { SwitchMuteUnMute(); };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Notify - No", null, (s, e) =>
            {
                notifications = !notifications;
                contextMenu.Items[0].Text = "Notify - " + (notifications ? "Yes" : "No");
            });
            contextMenu.Items.Add("Enable/Disable Mic", null, (s, e) => { SwitchMuteUnMute(); });
            contextMenu.Items.Add("Exit", null, (s, e) => { exit = true; notifyIcon.Visible = false; Application.Exit(); });
            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.Visible = true;

            //Starting process to recognize the changes
            new Thread(() =>
            {
                while (!exit)
                {
                    activeSession = null;
                    microphone.AudioSessionManager.RefreshSessions();

                    var sess = microphone.AudioSessionManager.Sessions.ToList();

                    if (!sess.Any(s => s.State == AudioSessionState.AudioSessionStateActive) && !micDisabled)
                    {
                        notifyIcon.Icon = Icon.ExtractAssociatedIcon("icons/off.ico");
                        micDisabled = true;
                        firstTime = true;
                        Thread.Sleep(100);
                        continue;
                    }

                    var activeSessions = sess.Where(s => s.State == AudioSessionState.AudioSessionStateActive);

                    if (!activeSessions.Any())
                    {
                        notifyIcon.Icon = Icon.ExtractAssociatedIcon("icons/off.ico");
                        micDisabled = true;
                        Thread.Sleep(100);
                        continue;
                    }

                    micDisabled = false;

                    if (activeSessions.All(s => s.SimpleAudioVolume.Mute) && (!microphoneIsMutted || firstTime))
                    {
                        microphoneIsMutted = true;
                        notifyIcon.Icon = Icon.ExtractAssociatedIcon("icons/red.ico");

                        Toast();
                    }
                    else if (activeSessions.All(s => !s.SimpleAudioVolume.Mute) && (microphoneIsMutted || firstTime))
                    {
                        microphoneIsMutted = false;
                        notifyIcon.Icon = Icon.ExtractAssociatedIcon("icons/green.ico");

                        Toast();
                    }

                    firstTime = false;

                    Thread.Sleep(100);
                }
            }).Start();

            //Starting app
            Application.Run();
        }

        static void SwitchMuteUnMute()
        {
            activeSession = null;
            microphone.AudioSessionManager.RefreshSessions();

            var sess = microphone.AudioSessionManager.Sessions.ToList().Where(s => s.State == AudioSessionState.AudioSessionStateActive);
            var toState = !sess.FirstOrDefault()?.SimpleAudioVolume.Mute ?? false;
            foreach(var s in sess)
            {
                s.SimpleAudioVolume.Volume = 0.94f;
                s.SimpleAudioVolume.Mute = toState;
            }
        }

        static void Toast()
        {
            if (!notifications)
                return;

            var ToastNotifier = ToastNotificationManager.CreateToastNotifier("MuteMic");

            XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            XmlNodeList toastNodeList = toastXml.GetElementsByTagName("text");

            toastNodeList[0].AppendChild(toastXml.CreateTextNode("Micrófono Switcher"));


            toastNodeList[1].AppendChild(toastXml.CreateTextNode(microphoneIsMutted ? "OFF" : "ON"));

            IXmlNode toastNode = toastXml.SelectSingleNode("/toast");
            XmlElement audio = toastXml.CreateElement("audio");
            audio.SetAttribute("src", "ms-winsoundevent:Notification.SMS");
            ToastNotification toast = new ToastNotification(toastXml);
            toast.ExpirationTime = DateTime.Now.AddSeconds(4);
            ToastNotifier.Show(toast);
        }
    }

    static class Tools
    {
        public static List<AudioSessionControl> ToList(this SessionCollection collection)
        {
            var l = new List<AudioSessionControl>();

            for (var i = 0; i < collection.Count; i++)
            {
                l.Add(collection[i]);
            }

            return l;
        }
    }
}
