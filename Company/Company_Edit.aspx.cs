using Ext.Net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

public partial class Company_Company_Edit : System.Web.UI.Page
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
        // If CheckSessionStatePermission("Admin") <> enumSessionStatePermission.AccessSuccess Then AlertMessage("您沒有存取這個項目的權限", "javascript:window.parent.closeActiveTab();")
        //貨幣
        string SQS;
        System.Data.SqlClient.SqlCommand DBCmdS;
        SQS = "SELECT *  FROM CurrencyTable With(Nolock)";
        DataTable CurrencyDT = new System.Data.DataTable();

        DBCmdS = new System.Data.SqlClient.SqlCommand();
        DBCmdS.CommandText = SQS;
        DBCmdS.CommandType = System.Data.CommandType.Text;
        CurrencyDT = DBAccess.GetDB(EWin.DBConnStr, DBCmdS);
        if (CurrencyDT.Rows.Count > 0)
        {
            foreach (DataRow dr in CurrencyDT.Rows)
            {
                groupCompanyCurrentTypeList.Items.Add(
                    new Checkbox {
                        BoxLabel = EWin.GetLanguage(dr["CurrencyName"].ToString()),
                        InputValue = dr["CurrencyType"].ToString(), Checked = false
                    });
                groupDefaultCurrencyType.Items.Add(
                    new Radio {
                        BoxLabel = EWin.GetLanguage(dr["CurrencyName"].ToString()),
                        InputValue = dr["CurrencyType"].ToString(), Checked = false });
            }

        }
        if (Ext.Net.X.IsAjaxRequest == false)
        {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            string CompanyID = string.Empty;
            bool IsChecked = false;
            DataTable CompanyDT = new DataTable();
            DataTable RoadMapDT = new DataTable();
            DataTable RoadMapListDT = new DataTable();
            
            string strCompanyPointTypeList = "";
            string strCompanyChipsList = string.Empty;
            string strCompanyCurrencyList = string.Empty;

            CompanyID = Request["CompanyID"];

            SS = " SELECT *" +
                     " FROM CompanyTable WITH(NOLOCK)" +
                     "       LEFT JOIN CompanyRoadMap" +
                     "              ON CompanyTable.CompanyID = CompanyRoadMap.forCompanyID" +
                     "  WHERE CompanyID = @CompanyID";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CompanyID", SqlDbType.VarChar).Value = CompanyID;
            CompanyDT = DBAccess.GetDB(EWin.DBConnStr, DBCmd);

            SS = "  SELECT RoadMapID," +
                     "  RoadMapNumber" +
                     "  FROM   RoadMapTable";

            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            RoadMapDT = DBAccess.GetDB(EWin.DBConnStr, DBCmd);
            //KeyRoadMapID.GetStore().DataSource = RoadMapDT.DefaultView;
            //KeyRoadMapID.GetStore().DataBind();

            SS = "  SELECT RoadMapTable.RoadMapID," +
                     "         RoadMapTable.RoadMapNumber," +
                     "         RoadMapArea.AreaName" +
                     "  FROM   RoadMapTable" +
                     "         INNER JOIN RoadMapArea" +
                     "                 ON RoadMapTable.RoadMapAreaCode = RoadMapArea.RoadMapAreaCode";

            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            RoadMapListDT = DBAccess.GetDB(EWin.DBConnStr, DBCmd);

            RoadMapListDT.DefaultView.Sort = "RoadMapNumber";
            gridCompanyRoadMap.GetStore().DataSource = RoadMapListDT.DefaultView;
            gridCompanyRoadMap.GetStore().DataBind();

            if (CompanyDT.Rows.Count > 0)
            {
                lblCompanyCode.Text = CompanyDT.Rows[0]["CompanyCode"].ToString();
                txtCompanyName.Text = CompanyDT.Rows[0]["CompanyName"].ToString();
                txtDescription.Text = CompanyDT.Rows[0]["Description"].ToString() == "" ? "" : CompanyDT.Rows[0]["Description"].ToString();
                txtCompanyUserRate.Text = CompanyDT.Rows[0]["CompanyUserRate"].ToString() == "" ? "" : (CodingControl.FormatDecimal(Decimal.Parse(CompanyDT.Rows[0]["CompanyUserRate"].ToString()))).ToString();
                txtCompanyBuyChipRate.Text = CompanyDT.Rows[0]["CompanyBuyChipRate"].ToString() == "" ? "" : (CodingControl.FormatDecimal(Decimal.Parse(CompanyDT.Rows[0]["CompanyBuyChipRate"].ToString()))).ToString();

                txtDomainURL.Text = CompanyDT.Rows[0]["DomainURL"].ToString() == "" ? "" : CompanyDT.Rows[0]["DomainURL"].ToString();
                txtGuestAccount.Text = CompanyDT.Rows[0]["GuestAccount"].ToString();
                txtCompanyEthWalletAddress.Text = CompanyDT.Rows[0]["CompanyEthWalletAddress"].ToString();

                switch (int.Parse(CompanyDT.Rows[0]["CompanyState"].ToString()))
                {
                    case 0:
                        {
                            rdoCompanyStateNormal.Checked = true;
                            break;
                        }

                    case 1:
                        {
                            rdoCompanyStateDisable.Checked = true;
                            break;
                        }
                }

                strCompanyChipsList = CompanyDT.Rows[0]["CompanyChipsList"].ToString();
                List<string> listChip = strCompanyChipsList.Split(';').ToList();
                foreach (var item in listChip)
                {
                    switch (item)
                    {
                        case "1":
                            Chip1.Checked = true;
                            break;
                        case "5":
                            Chip5.Checked = true;
                            break;
                        case "10":
                            Chip10.Checked = true;
                            break;
                        case "25":
                            Chip25.Checked = true;
                            break;
                        case "50":
                            Chip50.Checked = true;
                            break;
                        case "100":
                            Chip100.Checked = true;
                            break;
                        case "250":
                            Chip250.Checked = true;
                            break;
                        case "500":
                            Chip500.Checked = true;
                            break;
                        case "1000":
                            Chip1000.Checked = true;
                            break;
                        case "1250":
                            Chip1250.Checked = true;
                            break;
                        case "5000":
                            Chip5000.Checked = true;
                            break;
                        case "10000":
                            Chip10000.Checked = true;
                            break;
                        case "50000":
                            Chip50000.Checked = true;
                            break;
                        case "100000":
                            Chip100000.Checked = true;
                            break;
                        case "500000":
                            Chip500000.Checked = true;
                            break;
                        case "1000000":
                            Chip1000000.Checked = true;
                            break;
                        case "5000000":
                            Chip5000000.Checked = true;
                            break;
                        case "10000000":
                            Chip10000000.Checked = true;
                            break;
                    }
                }
                
                foreach (DataRow DR in CompanyDT.Rows)
                {
                    Ext.Net.RowSelectionModel CompanyRoadMapRSM = this.gridCompanyRoadMap.GetSelectionModel() as RowSelectionModel;
                    //KeyRoadMapID.SelectedItems.Add(new Ext.Net.ListItem(DR["forRoadMapID"].ToString()));
                    CompanyRoadMapRSM.SelectedRows.Add(new Ext.Net.SelectedRow(System.Convert.ToString(DR["forRoadMapID"].ToString())));
                }


                SS = "SELECT COUNT(*) " +
                     "FROM   AdminTable AS A WITH (NOLOCK) " +
                     "LEFT JOIN AdminRole AS AR WITH (NOLOCK) " +
                        "ON AR.AdminRoleID = A.forAdminRoleID " +
                     "WHERE A.forCompanyID=@CompanyID" +
                            "  AND AR.RoleName=@RoleName";

                DBCmd = new System.Data.SqlClient.SqlCommand();
                DBCmd.CommandText = SS;
                DBCmd.CommandType = System.Data.CommandType.Text;
                DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
                DBCmd.Parameters.Add("@RoleName", System.Data.SqlDbType.NVarChar).Value = "Admin";
                if (int.Parse(DBAccess.GetDBValue(EWin.DBConnStr, DBCmd).ToString()) <= 0)
                {
                    ExtControl.AlertMsg(EWin.GetLanguage("錯誤").ToString(), EWin.GetLanguage("此公司沒有帳號請建立").ToString());
                }
                else
                {
                    FieldSet2.Hide();
                }
                
                strCompanyCurrencyList = CompanyDT.Rows[0]["CompanyCurrentTypeList"].ToString();
                List<string> listCurrency = strCompanyCurrencyList.Split(';').ToList();
                foreach (var list in listCurrency)
                {
                    foreach (Checkbox item in groupCompanyCurrentTypeList.Items)
                    {
                        if(item.InputValue == list)
                        {
                            item.Checked = true;
                            break;
                        }
                        
                    }
                }
                foreach (Radio item in groupDefaultCurrencyType.Items)
                {
                    if (item.InputValue == CompanyDT.Rows[0]["DefaultCurrencyType"].ToString())
                    {
                        item.Checked = true;
                        break;
                    }

                }
                
                //KeyRoadMapID.UpdateSelectedItems();
                RedisCache.RoadMap.UpdateAvailableRoadMap(int.Parse(CompanyID));
            }
            else
            {
                ExtControl.ShowMsg("Exception", EWin.GetLanguage("資料錯誤").ToString());
            }
            
        }
        
    }

    protected void CheckCompanyCodeExist(object sender, Ext.Net.RemoteValidationEventArgs e)
    {
    }

    protected void btnSave_DirectClick(object sender, Ext.Net.DirectEventArgs e)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;
        //AdminLoginState Admin;
        bool AllowUpdate = false;
        string myPointType = string.Empty;
        string CompanyID = string.Empty;
        int CompanyState = 0;
        int AdminRoleID;
        DataTable PermissionDT;
        string CompanyChipsList = string.Empty;
        string CurrentTypeList = string.Empty;
        string DefaultCurrency = string.Empty;

        if (string.IsNullOrEmpty(txtCompanyName.Text))
        {
            ExtControl.ShowMsg("Exception", EWin.GetLanguage("請填寫公司名稱").ToString());
            return;
        }

        //Admin = GetAdminLogin();
        CompanyID = Request["CompanyID"];

        if (rdoCompanyStateNormal.Checked)
        {
            CompanyState = 0;
        }
        else if (rdoCompanyStateDisable.Checked)
        {
            CompanyState = 1;
        }

        if (Chip1.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip1.InputValue : Chip1.InputValue;
        }
        if (Chip5.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip5.InputValue : Chip5.InputValue;
        }
        if (Chip10.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip10.InputValue : Chip10.InputValue;
        }
        if (Chip25.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip25.InputValue : Chip25.InputValue;
        }
        if (Chip50.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip50.InputValue : Chip50.InputValue;
        }
        if (Chip100.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip100.InputValue : Chip100.InputValue;
        }
        if (Chip250.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip250.InputValue : Chip250.InputValue;
        }
        if (Chip500.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip500.InputValue : Chip500.InputValue;
        }
        if (Chip1000.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip1000.InputValue : Chip1000.InputValue;
        }
        if (Chip1250.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip1250.InputValue : Chip1250.InputValue;
        }
        if (Chip5000.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip5000.InputValue : Chip5000.InputValue;
        }
        if (Chip10000.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip10000.InputValue : Chip10000.InputValue;
        }
        if (Chip50000.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip50000.InputValue : Chip50000.InputValue;
        }
        if (Chip100000.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip100000.InputValue : Chip100000.InputValue;
        }
        if (Chip500000.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip500000.InputValue : Chip500000.InputValue;
        }
        if (Chip1000000.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip1000000.InputValue : Chip1000000.InputValue;
        }
        if (Chip5000000.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip5000000.InputValue : Chip5000000.InputValue;
        }
        if (Chip10000000.Checked)
        {
            CompanyChipsList += CompanyChipsList.Length > 0 ? ";" + Chip10000000.InputValue : Chip10000000.InputValue;
        }

        if (groupCompanyCurrentTypeList.CheckedItems.Count > 0)
        {
           
            for (int i = 0; i < groupCompanyCurrentTypeList.CheckedItems.Count; i++)
            {
                CurrentTypeList += CurrentTypeList.Length > 0 ? ";" + groupCompanyCurrentTypeList.CheckedItems[i].InputValue : groupCompanyCurrentTypeList.CheckedItems[i].InputValue;
            }
            //ExtControl.AlertMsg("1", CurrentTypeList);
        }

        if (groupDefaultCurrencyType.CheckedItems.Count > 0)
        {
            DefaultCurrency = groupDefaultCurrencyType.CheckedItems[0].InputValue;            
        }
        
        SS = "Update CompanyTable Set " +
                "CompanyState=@CompanyState, " +
                "CompanyName=@CompanyName, " +
                "Description=@Description, " +
                "CompanyUserRate=@CompanyUserRate, " +
                "CompanyBuyChipRate=@CompanyBuyChipRate," +
                "DomainURL=@DomainURL," +
                "CompanyChipsList=@CompanyChipsList," +
                "CompanyCurrentTypeList=@CompanyCurrentTypeList," +
                "DefaultCurrencyType=@DefaultCurrencyType, " +
                "GuestAccount=@GuestAccount, " +
                "CompanyEthWalletAddress=@CompanyEthWalletAddress " +
             "Where CompanyID=@CompanyID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyState", System.Data.SqlDbType.Int).Value = CompanyState;
        DBCmd.Parameters.Add("@CompanyName", System.Data.SqlDbType.NVarChar).Value = txtCompanyName.Text;
        DBCmd.Parameters.Add("@Description", System.Data.SqlDbType.VarChar).Value = txtDescription.Text;
        DBCmd.Parameters.Add("@DomainURL", System.Data.SqlDbType.VarChar).Value = txtDomainURL.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = int.Parse(CompanyID);
        DBCmd.Parameters.Add("@CompanyChipsList", System.Data.SqlDbType.VarChar).Value = CompanyChipsList;
        DBCmd.Parameters.Add("@CompanyUserRate", System.Data.SqlDbType.Decimal).Value = Decimal.Parse(txtCompanyUserRate.Text, CultureInfo.InvariantCulture);
        DBCmd.Parameters.Add("@CompanyBuyChipRate", System.Data.SqlDbType.Decimal).Value = Decimal.Parse(txtCompanyBuyChipRate.Text, CultureInfo.InvariantCulture);
        DBCmd.Parameters.Add("@CompanyCurrentTypeList", System.Data.SqlDbType.VarChar).Value = CurrentTypeList;
        DBCmd.Parameters.Add("@DefaultCurrencyType", System.Data.SqlDbType.VarChar).Value = DefaultCurrency;
        DBCmd.Parameters.Add("@GuestAccount", System.Data.SqlDbType.VarChar).Value = txtGuestAccount.Text; ;
        DBCmd.Parameters.Add("@CompanyEthWalletAddress", System.Data.SqlDbType.VarChar).Value = txtCompanyEthWalletAddress.Text;
        DBAccess.ExecuteDB(EWin.DBConnStr, DBCmd);


        SS = "DELETE FROM CompanyRoadMap WHERE forCompanyID=@CompanyID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = int.Parse(CompanyID);
        DBAccess.ExecuteDB(EWin.DBConnStr, DBCmd);

        if (Request.Form.GetValues("CompanyRoadMap") != null)
        {
            foreach (string EachItem in Request.Form.GetValues("CompanyRoadMap"))
            {
                SS = "INSERT INTO CompanyRoadMap (forCompanyID, forRoadMapID) VALUES (@CompanyID, @RoadMapID)";
                DBCmd = new System.Data.SqlClient.SqlCommand();
                DBCmd.CommandText = SS;
                DBCmd.CommandType = System.Data.CommandType.Text;
                DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = int.Parse(CompanyID);
                DBCmd.Parameters.Add("@RoadMapID", System.Data.SqlDbType.Int).Value = EachItem;
                DBAccess.ExecuteDB(EWin.DBConnStr, DBCmd);
            }
        }


        SS = "SELECT COUNT(*) " +
                     "FROM   AdminTable AS A WITH (NOLOCK) " +
                     "LEFT JOIN AdminRole AS AR WITH (NOLOCK) " +
                        "ON AR.AdminRoleID = A.forAdminRoleID " +
                     "WHERE A.forCompanyID=@CompanyID" +
                            "  AND AR.RoleName=@RoleName";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DBCmd.Parameters.Add("@RoleName", System.Data.SqlDbType.NVarChar).Value = "Admin";

        //判斷是否有帶Admin權限的帳號
        if (int.Parse(DBAccess.GetDBValue(EWin.DBConnStr, DBCmd).ToString()) <= 0)
        {

            //Create Admin Role
            SS = "INSERT INTO AdminRole (forCompanyID, RoleName) VALUES (@CompanyID, @RoleName);SELECT @@IDENTITY";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
            DBCmd.Parameters.Add("@RoleName", System.Data.SqlDbType.NVarChar).Value = "Admin";
            AdminRoleID = Convert.ToInt32(DBAccess.GetDBValue(EWin.DBConnStr, DBCmd));

            //PermissionTable
            SS = "SELECT * FROM PermissionTable WITH (NOLOCK)";
            PermissionDT = DBAccess.GetDB(EWin.DBConnStr, SS);

            //Create All Permission Role
            foreach (System.Data.DataRow EachPermission in PermissionDT.Rows)
            {
                if (string.IsNullOrEmpty(EachPermission["PermissionName"].ToString()) == false)
                {

                    SS = "INSERT INTO AdminRolePermission (forAdminRoleID, forCompanyID, forPermissionName) VALUES (@AdminRoleID, @CompanyID, @PermissionName)";
                    DBCmd = new System.Data.SqlClient.SqlCommand();
                    DBCmd.CommandText = SS;
                    DBCmd.CommandType = System.Data.CommandType.Text;
                    DBCmd.Parameters.Add("@AdminRoleID", System.Data.SqlDbType.Int).Value = AdminRoleID;
                    DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
                    DBCmd.Parameters.Add("@PermissionName", System.Data.SqlDbType.VarChar).Value = EachPermission["PermissionName"].ToString();
                    DBAccess.ExecuteDB(EWin.DBConnStr, DBCmd);

                }
            }


            //Create new Company Admin Account
            SS = "INSERT INTO AdminTable (forCompanyID, LoginAccount, LoginPassword, forAdminRoleID, RealName, Description,AdminType) VALUES (@CompanyID, @LoginAccount, @LoginPassword, @AdminRoleID, @RealName, @Description,@AdminType)";

            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
            DBCmd.Parameters.Add("@LoginAccount", System.Data.SqlDbType.VarChar).Value = txtLoginAccount.Text;
            DBCmd.Parameters.Add("@LoginPassword", System.Data.SqlDbType.VarChar).Value = CodingControl.GetMD5(txtLoginPassword.Text);
            DBCmd.Parameters.Add("@AdminType", System.Data.SqlDbType.Int).Value = 2;
            DBCmd.Parameters.Add("@AdminRoleID", System.Data.SqlDbType.Int).Value = AdminRoleID;
            DBCmd.Parameters.Add("@RealName", System.Data.SqlDbType.NVarChar).Value = txtRealname.Text;
            DBCmd.Parameters.Add("@Description", System.Data.SqlDbType.NVarChar).Value = txtDescription.Text;
            DBAccess.ExecuteDB(EWin.DBConnStr, DBCmd);

        }
        RedisCache.Company.UpdateCompanyByID(System.Convert.ToInt32(CompanyID));

        ExtControl.CloseActiveTab("Company_Maint.aspx", "Message", EWin.GetLanguage("儲存成功").ToString());
        ExtControl.ReloadActiveTab();
    }

    protected void btnClose_DirectClick(object sender, Ext.Net.DirectEventArgs e)
    {
        Ext.Net.KeyCode GetEnter = new Ext.Net.KeyCode();
        try
        {
            GetEnter = (Ext.Net.KeyCode)Enum.Parse(typeof(Ext.Net.KeyCode), e.ExtraParams["GetEnter"]);
        }
        catch (Exception)
        {

        }
        if (GetEnter != Ext.Net.KeyCode.ENTER)
        {
            ExtControl.CloseActiveTab();
        }
    }

    protected void btnAddMarqueeText_DirectClick(object sender, Ext.Net.DirectEventArgs e)
    {
        string CompanyID = Request["CompanyID"];
        ExtControl.NewTabToURL(EWin.GetLanguage("跑馬燈資訊").ToString(), "MarqueeText_Edit.aspx?CompanyID=" + CompanyID);
    }
    
}