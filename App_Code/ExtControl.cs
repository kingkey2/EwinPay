using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using System.Web;

public class ExtControl
{
    public const string MainFrameNameSpace = "Pay.MainFrame";

    public enum enumPanelType
    {
        StatusPanel,
        MenuPanel,
        MainTabPanel
    }

    public static string GetMyPageId()
    {
        string URL = HttpContext.Current.Request.RawUrl;
        int tmpIndex = URL.LastIndexOf('/');
        string pageId;

        if (tmpIndex != -1)
            URL = URL.Substring(tmpIndex + 1);

        tmpIndex = URL.IndexOf('?');
        if (tmpIndex != -1)
        {
            pageId = URL.Substring(0, tmpIndex);
        }
        else
        {
            pageId = URL;
        }

        return pageId.Trim().ToUpper();
    }

    public static void CallRootScript(string Fn)
    {
        EventMessage EM = new EventMessage();

        EM.Cmd = EventMessage.enumCmd.CallRootScript;
        EM.Set("Fn", Fn);

        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void CallPanelScript(enumPanelType Panel, string FnName)
    {
        EventMessage EM = new EventMessage();

        EM.Cmd = EventMessage.enumCmd.CallPanelScript;
        EM.Set("Panel", Panel.ToString());
        EM.Set("Fn", FnName);

        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void NewTabToURL(string Title, string URL)
    {
        EventMessage EM = new EventMessage();
        string PageId;
        int TmpIndex;

        TmpIndex = URL.IndexOf("?");
        if (TmpIndex != -1)
            PageId = URL.Substring(0, TmpIndex);
        else
            PageId = URL;

        EM.Cmd = EventMessage.enumCmd.NewTabToURL;
        EM.Set("Id", PageId.Trim().ToUpper());
        EM.Set("URL", URL);
        EM.Set("Title", Title);

        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void NewTabToURL2(string Title, string URL)
    {
        EventMessage EM = new EventMessage();
        string PageId;
        int TmpIndex;

        TmpIndex = URL.IndexOf("?");
        if (TmpIndex != -1)
            PageId = URL.Substring(0, TmpIndex);
        else
            PageId = URL;

        EM.Cmd = EventMessage.enumCmd.NewTabToURL;
        EM.Set("Id", System.Guid.NewGuid().ToString());
        EM.Set("URL", URL);
        EM.Set("Title", Title);

        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void CloseActiveTab(string ReloadId = null, string ShowMessageTitle = null, string ShowMessage = null)
    {
        EventMessage EM = new EventMessage();

        EM.Cmd = EventMessage.enumCmd.CloseActiveTab;
        // EM.Set("CloseId", GetMyPageId)

        if (string.IsNullOrEmpty(ReloadId) == false)
        {
            string PageId;
            int TmpIndex;

            TmpIndex = ReloadId.IndexOf("?");
            if (TmpIndex != -1)
                PageId = ReloadId.Substring(0, TmpIndex);
            else
                PageId = ReloadId;

            EM.Set("ReloadTab", PageId.Trim().ToUpper());
            // EM.Set("ReloadURL", ReloadId.Trim.ToUpper)
            EM.Set("ReloadURL", string.Empty);
        }

        EM.Set("Title", ShowMessageTitle);
        EM.Set("Message", ShowMessage);

        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void CloseTab(string TabId, string ReloadId = null, string ShowMessageTitle = null, string ShowMessage = null)
    {
        EventMessage EM = new EventMessage();

        EM.Cmd = EventMessage.enumCmd.CloseTab;
        EM.Set("CloseId", TabId.Trim().ToUpper());

        if (string.IsNullOrEmpty(ReloadId) == false)
        {
            string PageId;
            int TmpIndex;

            TmpIndex = ReloadId.IndexOf("?");
            if (TmpIndex != -1)
                PageId = ReloadId.Substring(0, TmpIndex);
            else
                PageId = ReloadId;

            EM.Set("ReloadTab", PageId.Trim().ToUpper());
            // EM.Set("ReloadURL", ReloadId.Trim.ToUpper)
            EM.Set("ReloadURL", string.Empty);
        }

        EM.Set("Title", ShowMessageTitle);
        EM.Set("Message", ShowMessage);

        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void ReloadTab(string TabId)
    {
        EventMessage EM = new EventMessage();

        EM.Cmd = EventMessage.enumCmd.ReloadTab;

        if (string.IsNullOrEmpty(TabId) == false)
        {
            string PageId;
            int TmpIndex;

            TmpIndex = TabId.IndexOf("?");
            if (TmpIndex != -1)
                PageId = TabId.Substring(0, TmpIndex);
            else
                PageId = TabId;

            EM.Set("ReloadTab", PageId.Trim().ToUpper());
            // EM.Set("ReloadURL", TabId.Trim.ToUpper)
            EM.Set("ReloadURL", string.Empty);
        }

        EM.Set("ReloadTab", TabId.Trim().ToUpper());
        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void ReloadTab(string TabId, string URL)
    {
        EventMessage EM = new EventMessage();

        EM.Cmd = EventMessage.enumCmd.ReloadTab;

        if (string.IsNullOrEmpty(TabId) == false)
        {
            string PageId;
            int TmpIndex;

            TmpIndex = TabId.IndexOf("?");
            if (TmpIndex != -1)
                PageId = TabId.Substring(0, TmpIndex);
            else
                PageId = TabId;

            EM.Set("ReloadTab", PageId.Trim().ToUpper());
            EM.Set("ReloadURL", URL);
        }

        EM.Set("ReloadTab", TabId.Trim().ToUpper());
        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void PlaySound(string MediaURL)
    {
        EventMessage EM = new EventMessage();

        EM.Cmd = EventMessage.enumCmd.PlaySound;
        EM.Set("MediaURL", MediaURL);
        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void ReloadActiveTab()
    {
        EventMessage EM = new EventMessage();

        EM.Cmd = EventMessage.enumCmd.ReloadActiveTab;
        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void ReloadActiveTab(string URL)
    {
        EventMessage EM = new EventMessage();

        EM.Cmd = EventMessage.enumCmd.ReloadActiveTab;
        EM.Set("ReloadURL", URL);
        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void Confirm(string Title, string Message, string PageNameSpace, string OKHandle, string CancelHandle = "")
    {
        string _handleOK = "";
        string _handleCancel = "";

        if (string.IsNullOrEmpty(OKHandle) == false)
            _handleOK = PageNameSpace + "." + OKHandle + "()";

        if (string.IsNullOrEmpty(CancelHandle) == false)
            _handleCancel = PageNameSpace + "." + CancelHandle + "()";

        Ext.Net.X.Msg.Confirm(Title, Message, new Ext.Net.MessageBoxButtonsConfig()
        {
            Yes = new Ext.Net.MessageBoxButtonConfig
                                                                ()
            {
                Handler = _handleOK,
                Text = "OK"
            },
            No = new Ext.Net.MessageBoxButtonConfig
                                                                ()
            {
                Handler = _handleCancel,
                Text = "Cancel"
            }
        }).Show();
    }

    public static void ShowMsg(string Title, string Message)
    {
        EventMessage EM = new EventMessage();

        EM.Cmd = EventMessage.enumCmd.ShowMsg;
        EM.Set("Title", Title);
        EM.Set("Message", Message);
        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void AlertMsg(string Title, string Message)
    {
        Ext.Net.X.Msg.Alert(Title, Message).Show();
    }

    public static void Notification(string Title, string Message)
    {
        EventMessage EM = new EventMessage();

        EM.Cmd = EventMessage.enumCmd.Notification;
        EM.Set("Title", Title);
        EM.Set("Message", Message);
        Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
    }

    public static void Redirect(string URL, string MaskMsg = "Loading...")
    {
        Ext.Net.X.Redirect(URL, MaskMsg);
    }

    private static System.Web.HttpRequest Request()
    {
        return System.Web.HttpContext.Current.Request;
    }

    private static System.Web.HttpResponse Response()
    {
        return System.Web.HttpContext.Current.Response;
    }

    private static System.Web.HttpServerUtility Server()
    {
        return System.Web.HttpContext.Current.Server;
    }

    private static System.Web.HttpApplicationState Application()
    {
        return System.Web.HttpContext.Current.Application;
    }

    private static System.Web.SessionState.HttpSessionState Session()
    {
        return System.Web.HttpContext.Current.Session;
    }

    [Serializable]
    public class EventMessage
    {
        public enum enumCmd
        {
            NewTabToURL,
            NewTabToURL2,
            CloseTab,
            CloseActiveTab,
            ReloadTab,
            ReloadActiveTab,
            ShowMsg,
            CallPanelScript,
            CallRootScript,
            Notification,
            PlaySound,
            StopSound
        }

        public enumCmd Cmd;
        public System.Collections.Generic.List<HeaderItemClass> Params = new System.Collections.Generic.List<HeaderItemClass>();

        public HeaderItemClass FindKey(string name)
        {
            HeaderItemClass RetValue = null;

            lock (Params)
            {
                foreach (HeaderItemClass EachHHI in Params)
                {
                    if (EachHHI.Name.Trim().ToUpper() == name.Trim().ToUpper())
                    {
                        RetValue = EachHHI;
                        break;
                    }
                }
            }

            return RetValue;
        }

        public void Set(string name, string value)
        {
            HeaderItemClass HHI;

            HHI = FindKey(name);

            if (HHI == null)
            {
                HHI = new HeaderItemClass();
                HHI.Name = name;
                HHI.Value = value;

                lock (Params)
                    Params.Add(HHI);
            }
            else
                HHI.Value = value;
        }

        public void ShowMsg(string Title, string Message)
        {
            EventMessage EM = new EventMessage();

            EM.Cmd = EventMessage.enumCmd.ShowMsg;
            EM.Set("Title", Title);
            EM.Set("Message", Message);
            Ext.Net.MessageBus.Default.Publish(MainFrameNameSpace, HttpContext.Current.Server.UrlEncode(CodingControl.XMLSerial(EM)));
        }

        public void AlertMsg(string Title, string Message)
        {
            Ext.Net.X.Msg.Alert(Title, Message).Show();
        }

        public string GetValue(string name)
        {
            string RetValue = null;
            HeaderItemClass HHI;

            lock (Params)
                HHI = FindKey(name);

            if (HHI != null)
                RetValue = HHI.Value;

            return RetValue;
        }

        [Serializable()]
        public class HeaderItemClass
        {
            public string Name;
            public string Value;
        }
    }
}
