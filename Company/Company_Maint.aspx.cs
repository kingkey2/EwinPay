using Ext.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

public partial class Company_Company_Maint : System.Web.UI.Page {
    protected override void InitializeCulture()
    {
        CodingControl.CheckingLanguage(Request["BackendLang"]);
        base.InitializeCulture();
    }

    protected void Page_Load(object sender, EventArgs e) {
        CompanySessionState CSS = null;
        if (Session["_CompanyLogined"] == null) {
            Response.Redirect("RefreshParent.aspx?Login.aspx", true);
        }
        CSS = (CompanySessionState)Session["_CompanyLogined"];
        //if (EWin.CheckSessionStatePermission((CompanySessionState)Session["_CompanyLogined"], "Company_Maint") != EWin.enumSessionStatePermission.AccessSuccess) {
        if (!EWin.IsAdminCompany(CSS.Company.CompanyID))
        { 
            ExtControl.ShowMsg(EWin.GetLanguage("沒有權限").ToString(), EWin.GetLanguage("您沒有存取這個項目的權限").ToString());
            ExtControl.CloseActiveTab();
        }

        if (Ext.Net.X.IsAjaxRequest == false) {
            selectCompanyState.SelectedItems.Add(new Ext.Net.ListItem(value: "2"));
            Store1_SetDataBind();
        }
    }

    private enum enumUserAccountStateDisplay {
        All = 0,
        Disabled = 1,
        Enabled = 2
    }

    public void Store1_RefreshData(object sender, Ext.Net.StoreReadDataEventArgs e) {
        Store1_SetDataBind();
    }

    public void Store1_SetDataBind() {
        System.Data.DataTable DT;
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;
        enumUserAccountStateDisplay selectCompanyStateDisplay = new enumUserAccountStateDisplay();
        string Param = string.Empty;

        selectCompanyStateDisplay = enumUserAccountStateDisplay.Enabled;
        Param = " And CompanyState = 0";
        switch (selectCompanyState.SelectedItem.Value.ToString()) {
            case "0": {
                    selectCompanyStateDisplay = enumUserAccountStateDisplay.All;
                    Param = "";
                    break;
                }

            case "1": {
                    selectCompanyStateDisplay = enumUserAccountStateDisplay.Disabled;
                    Param = " And CompanyState = 1";
                    break;
                }

            case "2": {
                    selectCompanyStateDisplay = enumUserAccountStateDisplay.Enabled;
                    Param = " And CompanyState = 0";
                    break;
                }
        }

        SS = "  SELECT *," +
                 "       (SELECT RoadMapTable.RoadMapNumber + ','" +
                 "        FROM CompanyRoadMap" +
                 "               INNER JOIN RoadMapTable" +
                 "                       ON CompanyRoadMap.forRoadMapID = RoadMapTable.RoadMapID" +
                 "        WHERE CompanyRoadMap.forCompanyID = CompanyTable.CompanyID" +
                 "        FOR XML PATH('')) AS CompanyRoadMap" +
                 "  FROM CompanyTable WITH(NOLOCK)" +
                 "  WHERE  1 = 1" + Param;
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DT = DBAccess.GetDB(EWin.DBConnStr, DBCmd);
        foreach (System.Data.DataRow item in DT.Rows) {
            if (item["CompanyRoadMap"].ToString()!="") {
                item["CompanyRoadMap"] = item["CompanyRoadMap"].ToString().Substring(0, item["CompanyRoadMap"].ToString().Length - 1);
            }
        }
        Store1.DataSource = DT;
        Store1.DataBind();
    }

    public void Store1_Submit(object sender, Ext.Net.StoreSubmitDataEventArgs e) {
        System.Xml.XmlNode xml;

        xml = e.Xml;
        Response.Clear();

        switch (FormatType.Value.ToString()) {
            case "xml": {
                    string strXml = xml.OuterXml;

                    Response.AddHeader("Content-Disposition", "attachment; filename=submittedData.xml");
                  //  Response.AddHeader("Content-Length", strXml.Length.ToString());
                    Response.ContentType = "application/xml";
                    Response.Write(strXml);
                    break;
                }

            case "xls": {
                    System.Xml.Xsl.XslCompiledTransform xtExcel;

                    Response.ContentType = "application/vnd.ms-excel";
                    Response.AddHeader("Content-Disposition", "attachment; filename=submittedData.xls");

                    xtExcel = new System.Xml.Xsl.XslCompiledTransform();
                    xtExcel.Load(Server.MapPath("../Files/Excel.xsl"));
                    xtExcel.Transform(xml, null/* TODO Change to default(_) if this is not a reference type */, Response.OutputStream);
                    break;
                }

            case "csv": {
                    System.Xml.Xsl.XslCompiledTransform xtCsv;

                    Response.ContentType = "application/octet-stream";
                    Response.AddHeader("Content-Disposition", "attachment; filename=submittedData.csv");

                    xtCsv = new System.Xml.Xsl.XslCompiledTransform();
                    xtCsv.Load(Server.MapPath("../Files/Csv.xsl"));
                    xtCsv.Transform(xml, null/* TODO Change to default(_) if this is not a reference type */, Response.OutputStream);
                    break;
                }
        }

        Response.Flush();
        Response.End();
    }

    protected void btnNew_DirectClick(object sender, Ext.Net.DirectEventArgs e) {
        ExtControl.NewTabToURL(EWin.GetLanguage("公司帳號新增").ToString(), "Company_Add.aspx");
    }

    protected void btnEdit_DirectClick(object sender, Ext.Net.DirectEventArgs e) {
        int CompanyID;

        if (RowSelectionModel1.SelectedRows.Count > 0) {
            CompanyID = int.Parse(RowSelectionModel1.SelectedRows[0].RecordID);

            ExtControl.NewTabToURL(EWin.GetLanguage("公司帳號編輯").ToString(), "Company_Edit.aspx?CompanyID=" + CompanyID);
        }
    }

    protected void btnDelete_DirectClick(object sender, Ext.Net.DirectEventArgs e) {
        //if (RowSelectionModel1.SelectedRows.Count > 0) {
        //    foreach (Ext.Net.SelectedRow EachRow in RowSelectionModel1.SelectedRows) {
        //        string SS;
        //        System.Data.SqlClient.SqlCommand DBCmd;

        //        SS = "UPDATE CompanyTable SET CompanyState=1 WHERE CompanyID=@CompanyID";
        //        DBCmd = new System.Data.SqlClient.SqlCommand();
        //        DBCmd.CommandText = SS;
        //        DBCmd.CommandType = System.Data.CommandType.Text;
        //        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = EachRow.RecordID;
        //        DBAccess.ExecuteDB(EWin.DBConnStr, DBCmd);

        //        ExtControl.ReloadActiveTab();
        //    }
        //}
    }

    protected void selectCompanyState_Select(object sender, Ext.Net.DirectEventArgs e) {
        Store1_SetDataBind();
    }

    [DirectMethod]
    public void DoConfirm() {
        
            X.Msg.Confirm(EWin.GetLanguage("注意").ToString(), EWin.GetLanguage("確定停用選擇的項目?").ToString(), new MessageBoxButtonsConfig {
                Yes = new MessageBoxButtonConfig {
                    Handler = "Company_Maint.btnDeleteOK()",
                    Text = EWin.GetLanguage("確定").ToString()
                },
                No = new MessageBoxButtonConfig {
                    Handler = "Company_Maint.btnDeleteCancel()",
                    Text = EWin.GetLanguage("取消").ToString()
                }
            }).Show();
    }

    [DirectMethod]
    public void btnDeleteCancel() {
    }

    [DirectMethod]
    public void btnDeleteOK() {
        if (RowSelectionModel1.SelectedRows.Count > 0) {
            foreach (Ext.Net.SelectedRow EachRow in RowSelectionModel1.SelectedRows) {
                string SS;
                System.Data.SqlClient.SqlCommand DBCmd;

                SS = "UPDATE CompanyTable SET CompanyState=1 WHERE CompanyID=@CompanyID";
                DBCmd = new System.Data.SqlClient.SqlCommand();
                DBCmd.CommandText = SS;
                DBCmd.CommandType = System.Data.CommandType.Text;
                DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = EachRow.RecordID;
                DBAccess.ExecuteDB(EWin.DBConnStr, DBCmd);

                ExtControl.ReloadActiveTab();
            }
        }
    }
    
    //private void BuildTree(Ext.Net.Node RootNode, int ParentUserAccountID, enumUserAccountStateDisplay UserAccountStateDisplay, string SearchText) {
    //    System.Data.DataTable DT;
    //    string SS;
    //    System.Data.SqlClient.SqlCommand DBCmd;

    //    SS = "SELECT * FROM CompanyTable WITH (NOLOCK)";
    //    DBCmd = new System.Data.SqlClient.SqlCommand();
    //    DBCmd.CommandText = SS;
    //    DBCmd.CommandType = System.Data.CommandType.Text;
    //    DT = DBAccess.GetDB(EWin.DBConnStr, DBCmd);

    //    DT.DefaultView.Sort = "CompanyCode";
    //    foreach (System.Data.DataRowView EachDRV in DT.DefaultView) {
    //        if ((System.Convert.ToInt32(EachDRV["CompanyState"]) == 0 & (UserAccountStateDisplay == enumUserAccountStateDisplay.All | UserAccountStateDisplay == enumUserAccountStateDisplay.Enabled)) | (System.Convert.ToInt32(EachDRV["UserAccountState"]) == 1 & (UserAccountStateDisplay == enumUserAccountStateDisplay.All | UserAccountStateDisplay == enumUserAccountStateDisplay.Disabled))) {
    //            Ext.Net.Node N = new Ext.Net.Node();
    //            bool AllowAdd = false;

    //            AllowAdd = true;

    //            if (AllowAdd) {
    //                N.NodeID = EachDRV["CompanyID"].ToString();
    //                N.CustomAttributes.Add(new Ext.Net.ConfigItem("CompanyState", EachDRV["CompanyState"]));
    //                N.CustomAttributes.Add(new Ext.Net.ConfigItem("CompanyCode", EachDRV["CompanyCode"]));
    //                N.CustomAttributes.Add(new Ext.Net.ConfigItem("CompanyName", EachDRV["CompanyName"]));
    //                N.CustomAttributes.Add(new Ext.Net.ConfigItem("Description", EachDRV["Description"]));

    //                N.Leaf = true;
    //                RootNode.Children.Add(N);
    //            }
    //        }
    //    }
    //}

}