using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Ext.Net;

public partial class Login : System.Web.UI.Page {

    protected void Page_Load(object sender, EventArgs e) {
      
        btnLangC.DirectClick += btnLangC_DirectClick;

        if (HttpContext.Current.Request.Cookies["BackendLang"] !=null)
        {
            var lang = HttpContext.Current.Request.Cookies["BackendLang"].Value;
            switch (lang)
            {
                case "zh-TW":
                    btnLangC.Pressed = true;
                    break;
            }
        }
        
    }

    private void btnLangC_DirectClick(object sender, DirectEventArgs e)
    {
        Response.SetCookie(new HttpCookie("BackendLang", "zh-TW"));
        Response.Redirect("RefreshParent.aspx?Login.aspx");
    }

    protected override void InitializeCulture()
    {
        CodingControl.CheckingLanguage(Request["BackendLang"]);
        base.InitializeCulture();
    }

    protected void btnLogin_Click(object sender, EventArgs e) {
        var DBCmd = new System.Data.SqlClient.SqlCommand();
        var DT = new System.Data.DataTable();
        var LoginAccount = txtAccount.Text;
        var LoginPassword = txtPassword.Text;
        var CompanyCode = txtCompanyCode.Text;
        var LoginDetail = new CompanySessionState();

        //ExtControl.AlertMsg("登入失敗", LoginAccount+ LoginPassword+ CompanyCode);
        if (string.IsNullOrEmpty(LoginAccount)) {
            ExtControl.AlertMsg(GetLocalResourceObject("登入失敗").ToString(), GetLocalResourceObject("請輸入帳號").ToString());
        } else if (string.IsNullOrEmpty(LoginPassword)) {
            ExtControl.AlertMsg(GetLocalResourceObject("登入失敗").ToString(), GetLocalResourceObject("請輸入密碼").ToString());
        } else if (string.IsNullOrEmpty(CompanyCode)) {
            ExtControl.AlertMsg(GetLocalResourceObject("登入失敗").ToString(), GetLocalResourceObject("請輸入公司代碼").ToString());
        } else {
            LoginDetail = EWin.AdminLogin(CompanyCode, LoginAccount, LoginPassword);

            switch (LoginDetail.LoginState) {
                case CompanySessionState.enumLoginState.None:
                    ExtControl.AlertMsg(GetLocalResourceObject("登入失敗").ToString(), GetLocalResourceObject("登入失敗").ToString());
                    break;
                case CompanySessionState.enumLoginState.Logined:
                    Session["_CompanyLogined"] = LoginDetail;
                    ExtControl.Redirect("MainFrame.aspx");
                    break;
                case CompanySessionState.enumLoginState.AccountIsLocked:
                    ExtControl.AlertMsg(GetLocalResourceObject("登入失敗").ToString(), GetLocalResourceObject("因為失敗造成帳戶鎖定, 10 分鐘後才能解鎖").ToString());
                    break;
                default:
                    break;
            }
        }
    }
    
}