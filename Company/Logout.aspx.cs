using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

public partial class Company_Logout : System.Web.UI.Page
{
    protected override void InitializeCulture()
    {
        CodingControl.CheckingLanguage(Request["BackendLang"]);
        base.InitializeCulture();
    }
    protected void Page_Load(object sender, EventArgs e)
    {
        if (Session["_CompanyLogined"] == null)
        {            
            Response.Redirect("RefreshParent.aspx?Login.aspx", true);
        }
        //if (!Ext.Net.X.IsAjaxRequest)
        //{
        //    CompanySessionState CSS;
        //    CSS = (CompanySessionState)Session["_CompanyLogined"];
        //    //Response.Redirect("RefreshParent.aspx?Login.aspx", true);
        //}
        //else
        //{
        //    //Response.Redirect("RefreshParent.aspx?Login.aspx", true);
        //}
    }
    protected void btnLogout_Click(object sender, EventArgs e)
    {
        Session.RemoveAll();
        Session["_CompanyLogined"] = null;
        Session.Abandon();
        ExtControl.ReloadActiveTab();        
    }
    protected void btnClose_DirectClick(object sender, Ext.Net.DirectEventArgs e)
    {
        ExtControl.CloseActiveTab();
    }


}