using Ext.Net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Timers;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using EventMessage = ExtControl.EventMessage;

public partial class MainFrame : System.Web.UI.Page
{
    protected override void InitializeCulture()
    {
        base.InitializeCulture();
    }

    protected void Page_Load(object sender, EventArgs e) {
        CompanySessionState CST = (CompanySessionState)Session["_CompanyLogined"];

        if (CST == null)
            Response.Redirect("Refresh.aspx?Login.aspx", true);
    }

    public void OnMessageBusEvent(object sender, Ext.Net.DirectEventArgs e)
    {

        String EventXML = Server.UrlDecode(e.ExtraParams["message"]);
        EventMessage EM = (EventMessage)CodingControl.XMLDeserial(EventXML, typeof(EventMessage));

        switch (EM.Cmd) {
            case EventMessage.enumCmd.NewTabToURL:
            case EventMessage.enumCmd.NewTabToURL2:
                var NewTab = new Ext.Net.Panel();

                NewTab.Title = EM.GetValue("Title");
                NewTab.ID = EM.GetValue("Id");
                NewTab.Closable = true;

                NewTab.Loader = new Ext.Net.ComponentLoader();
                NewTab.Loader.Url = EM.GetValue("URL");
                NewTab.Loader.Mode = Ext.Net.LoadMode.Frame;
                NewTab.Loader.LoadMask.ShowMask = true;

                NewTab.AddTo(MainTabPanel);
                MainTabPanel.SetActiveTab(NewTab);
                break;
            case EventMessage.enumCmd.CloseTab:
            case EventMessage.enumCmd.CloseActiveTab:               
                switch (EM.Cmd)
                {
                    case EventMessage.enumCmd.CloseActiveTab:
                        Ext.Net.X.AddScript("closeActiveTab();");
                        break;
                    case EventMessage.enumCmd.CloseTab:
                        Ext.Net.X.AddScript("closeTab('" + CodingControl.JSEncodeString(EM.GetValue("CloseId"))+  "');");
                        break;
                }
                if (!(string.IsNullOrEmpty(EM.GetValue("Message"))))
                    Ext.Net.X.Msg.Alert(EM.GetValue("Title"), EM.GetValue("Message")).Show();
                if (!(string.IsNullOrEmpty(EM.GetValue("ReloadTab"))))
                {
                    if (string.IsNullOrEmpty(EM.GetValue("ReloadURL")))                   
                        Ext.Net.X.AddScript("reloadTab('" +CodingControl.JSEncodeString(EM.GetValue("ReloadTab")) + "');");                   
                    else
                        Ext.Net.X.AddScript("reloadTabToURL('" + CodingControl.JSEncodeString(EM.GetValue("ReloadTab")) + "','"+ CodingControl.JSEncodeString(EM.GetValue("ReloadURL")) + "');");
                }
                break;
            case EventMessage.enumCmd.ReloadTab:
            case EventMessage.enumCmd.ReloadActiveTab:
                
                switch (EM.Cmd)
                {
                    case EventMessage.enumCmd.ReloadTab:
                        if (string.IsNullOrEmpty(EM.GetValue("ReloadURL")))
                                Ext.Net.X.AddScript("reloadTab('" + CodingControl.JSEncodeString(EM.GetValue("ReloadTab")) + "');");
                            else
                                Ext.Net.X.AddScript("reloadTabToURL('" + CodingControl.JSEncodeString(EM.GetValue("ReloadTab")) + "','" + CodingControl.JSEncodeString(EM.GetValue("ReloadURL")) + "');");
                        break;
                    case EventMessage.enumCmd.ReloadActiveTab:
                        if (string.IsNullOrEmpty(EM.GetValue("ReloadURL")))
                            Ext.Net.X.AddScript("reloadActiveTab();");
                        else
                            Ext.Net.X.AddScript("reloadActiveTabToURL('" + CodingControl.JSEncodeString(EM.GetValue("ReloadURL")) + "');");
                        break;
                }
                break;
            case EventMessage.enumCmd.ShowMsg:
                Ext.Net.X.Msg.Alert(EM.GetValue("Title"), EM.GetValue("Message")).Show();
                break;
            case EventMessage.enumCmd.CallPanelScript:
                Ext.Net.X.AddScript("callPanelScript('" + CodingControl.JSEncodeString(EM.GetValue("Panel")) + "', '" + CodingControl.JSEncodeString(EM.GetValue("Fn")) + "');");
                break;
            case EventMessage.enumCmd.CallRootScript:
                Ext.Net.X.AddScript(EM.GetValue("Fn"));
                break;
            case EventMessage.enumCmd.Notification:
                Ext.Net.NotificationConfig NC = new Ext.Net.NotificationConfig();
                NC.Title = EM.GetValue("Title");
                NC.Html = EM.GetValue("Message");
                NC.HideFx = new SwitchOff();
                Ext.Net.Notification.Show(NC);
                break;

            case EventMessage.enumCmd.PlaySound:
                if (!(string.IsNullOrEmpty(EM.GetValue("ReloadURL"))))
                    Ext.Net.X.AddScript("playSound('" + CodingControl.JSEncodeString(EM.GetValue("MediaURL")) + "');");
                break;
            case EventMessage.enumCmd.StopSound:
                Ext.Net.X.AddScript("stopSound();");
                break;
        }
    }

    public void NodeItem_Click(object sender, Ext.Net.DirectEventArgs e)
    {
        int Method = 1;
        if (!(String.IsNullOrEmpty(e.ExtraParams["Method"])))
        {
            System.Int32.TryParse(e.ExtraParams["Method"],out Method);
        }
        switch (Method) {
            case 1:
                ExtControl.NewTabToURL(e.ExtraParams["Text"],e.ExtraParams["URL"]);
                break;
            case 2:
                ExtControl.NewTabToURL2(e.ExtraParams["Text"], e.ExtraParams["URL"]);               
                break;
        }
       
    }

    public void Logout_Click(object sender, Ext.Net.DirectEventArgs e)
    {
        Session.RemoveAll();
        ExtControl.ReloadActiveTab();
    }
}