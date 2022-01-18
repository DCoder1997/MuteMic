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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MuteMic
{
    internal class Program
    {
        public static bool microphoneIsMutted;
        static bool micDisabled;
        static bool firstTime = true;
        static bool notifications;

        public static List<uint> sessionsRegistered = new List<uint>();

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
            var microphone = deviceCollection.First(d => d.DeviceFriendlyName.ToLowerInvariant().Contains(args[0].ToLowerInvariant()) &&
                d.AudioSessionManager.Sessions.Count >= 1);

            if (microphone == null)
            {
                Console.WriteLine("No microphone found with sessions");
                return;
            }

            //Checking if the app is running
            var processes = Process.GetProcessesByName("MuteMic");

            if (processes.Length > 1 || args.Length == 2)
            {
                //If is running only change the mic state
                SwitchMuteUnMute(args: args[0]);
                return;
            }

            //Tray icon
            NotifyIcon notifyIcon = new NotifyIcon();

            //Events
            microphone.AudioSessionManager.OnSessionCreated += (ev, arg) =>
            {
                RefreshAll(microphone, notifyIcon);
            };

            //Starting tray icon
            notifyIcon.Icon = Icon.ExtractAssociatedIcon("icons/off.ico");
            notifyIcon.DoubleClick += (obj, ev) => { SwitchMuteUnMute(args: args[0]); };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Notify - No", null, (s, e) =>
            {
                notifications = !notifications;
                contextMenu.Items[0].Text = "Notify - " + (notifications ? "Yes" : "No");
            });
            contextMenu.Items.Add("Enable/Disable Mic", null, (s, e) =>
            {
                SwitchMuteUnMute();
            });
            contextMenu.Items.Add("Exit", null, (s, e) =>
            {
                notifyIcon.Visible = false;
                Application.Exit();
                Process.GetProcessesByName("MuteMic").ToList().ForEach(p => p.Kill());
            });
            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.Visible = true;

            //First start
            RefreshAll(microphone, notifyIcon);

            //Starting app
            Application.ApplicationExit += (ev, arg) =>
            {
                notifyIcon.Visible = false;
                Application.Exit();
                Process.GetProcessesByName("MuteMic").ToList().ForEach(p => p.Kill());
            };
            Application.Run();
        }

        static void RefreshAll(MMDevice microphone, NotifyIcon notifyIcon)
        {
            microphone.AudioSessionManager.RefreshSessions();
            var sess = microphone.AudioSessionManager.Sessions.ToList();
            foreach (var s in sess)
            {
                if (!sessionsRegistered.Contains(s.GetProcessID))
                {
                    s.RegisterEventClient(new MicroEvents() { refreshAll = () => RefreshAll(microphone, notifyIcon), session = s, unregister = () => sessionsRegistered.Remove(s.GetProcessID) });
                    sessionsRegistered.Add(s.GetProcessID);
                }
            }

            if (!sess.Any(s => s.State == AudioSessionState.AudioSessionStateActive) && !micDisabled)
            {
                notifyIcon.Icon = Icon.ExtractAssociatedIcon("icons/off.ico");
                micDisabled = true;
                firstTime = true;
                return;
            }

            var activeSessions = sess.Where(s => s.State == AudioSessionState.AudioSessionStateActive);

            if (!activeSessions.Any())
            {
                notifyIcon.Icon = Icon.ExtractAssociatedIcon("icons/off.ico");
                micDisabled = true;
                return;
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
        }

        static void SwitchMuteUnMute(MMDevice microphone = null, string args = null)
        {
            if (microphone == null)
                microphone = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .First(d => d.DeviceFriendlyName.ToLowerInvariant().Contains(args.ToLowerInvariant()) &&
                    d.AudioSessionManager.Sessions.Count >= 1);

            microphone.AudioSessionManager.RefreshSessions();

            var sess = microphone.AudioSessionManager.Sessions.ToList().Where(s => s.State == AudioSessionState.AudioSessionStateActive);
            var toState = !sess.FirstOrDefault()?.SimpleAudioVolume.Mute ?? false;
            foreach (var s in sess)
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

    class MicroEvents : IAudioSessionEventsHandler
    {
        public Action refreshAll;
        public Action unregister;
        public AudioSessionControl session;

        public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex) { }

        public void OnDisplayNameChanged(string displayName) { }

        public void OnGroupingParamChanged(ref Guid groupingId) { }

        public void OnIconPathChanged(string iconPath) { }

        public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
        {
            unregister();

            try
            {
                session.UnRegisterEventClient(this);
                session.Dispose();
            }
            catch (COMException) { }

            GC.Collect();
            refreshAll();
        }

        public void OnStateChanged(AudioSessionState state)
        {
            refreshAll();
        }

        public void OnVolumeChanged(float volume, bool isMuted)
        {
            if (Program.microphoneIsMutted != isMuted)
            {
                refreshAll();
                Program.microphoneIsMutted = isMuted;
            }
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
