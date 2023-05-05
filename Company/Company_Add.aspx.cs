using Ext.Net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

public partial class Company_Company_Add : System.Web.UI.Page
{
    protected override void InitializeCulture()
    {
        CodingControl.CheckingLanguage(Request["BackendLang"]);
        base.InitializeCulture();
    }
    protected void Page_Load(object sender, EventArgs e)
    {        
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;
        var DT = new System.Data.DataTable();
        if (Session["_CompanyLogined"] == null)
        {
            Response.Redirect("RefreshParent.aspx?Login.aspx", true);
        }
        //貨幣
        SS = "SELECT *  FROM CurrencyTable With(Nolock)";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DT = DBAccess.GetDB(EWin.DBConnStr, DBCmd);
        if (DT.Rows.Count > 0)
        {            
            foreach(DataRow dr in DT.Rows)
            {
                groupCompanyCurrentTypeList.Items.Add(
                    new Checkbox {
                        BoxLabel = EWin.GetLanguage(dr["CurrencyName"].ToString()),
                        InputValue = dr["CurrencyType"].ToString(), Checked = false }
                    );
                groupDefaultCurrencyType.Items.Add(
                    new Radio {
                        BoxLabel = EWin.GetLanguage(dr["CurrencyName"].ToString()),
                        InputValue = dr["CurrencyType"].ToString(), Checked = false
                    });
            }
            
        }
    }

    protected void CheckCompanyCodeExist(object sender, Ext.Net.RemoteValidationEventArgs e)
    {
        Ext.Net.TextField Text = (Ext.Net.TextField)sender;
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;

        SS = "SELECT COUNT(*) FROM CompanyTable WITH (NOLOCK) WHERE CompanyCode=@CompanyCode";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyCode", System.Data.SqlDbType.VarChar).Value = Text.Value;

        if (int.Parse(DBAccess.GetDBValue(EWin.DBConnStr, DBCmd).ToString()) <= 0)
            e.Success = true;
        else
        {
            e.Success = false;
            e.ErrorMessage = EWin.GetLanguage("公司代碼已經存在").ToString();
        }
    }

    protected void btnSave_DirectClick(object sender, Ext.Net.DirectEventArgs e)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;
        // AdminLoginState Admin;
        bool AllowUpdate = false;
        string myPointType = string.Empty;
        int AdminRoleID;
        int CompanyID;
        DataTable PermissionDT;
        string CompanyChipsList = string.Empty;
        string CurrentTypeList = string.Empty;
        string DefaultCurrency = string.Empty;

        if (string.IsNullOrEmpty(txtCompanyCode.Text))
        {
            ExtControl.ShowMsg("Exception", EWin.GetLanguage("請填寫公司代碼").ToString());
            return;
        }
        if (string.IsNullOrEmpty(txtCompanyName.Text))
        {
            ExtControl.ShowMsg("Exception", EWin.GetLanguage("請填寫公司名稱").ToString());
            return;
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
        }
        if(groupDefaultCurrencyType.CheckedItems.Count>0)
        {
            DefaultCurrency=groupDefaultCurrencyType.CheckedItems[0].InputValue;
        }


        SS = "SELECT COUNT(*) FROM CompanyTable WITH (NOLOCK) WHERE CompanyCode=@CompanyCode";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyCode", System.Data.SqlDbType.VarChar).Value = txtCompanyCode.Text;
        if (int.Parse(DBAccess.GetDBValue(EWin.DBConnStr, DBCmd).ToString()) <= 0)
        {
            SS = "INSERT INTO CompanyTable (CompanyCode, CompanyName,  Description,CompanyUserRate,CompanyBuyChipRate,DomainURL,CompanyChipsList,CompanyCurrentTypeList,DefaultCurrencyType,GuestAccount,CompanyEthWalletAddress) " +
                "                  VALUES (@CompanyCode, @CompanyName, @Description,@CompanyUserRate,@CompanyBuyChipRate,@DomainURL,@CompanyChipsList,@CompanyCurrentTypeList,@DefaultCurrencyType,@GuestAccount,@CompanyEthWalletAddress);SELECT @@IDENTITY";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CompanyCode", System.Data.SqlDbType.VarChar).Value = txtCompanyCode.Text;
            DBCmd.Parameters.Add("@CompanyName", System.Data.SqlDbType.NVarChar).Value = txtCompanyName.Text;
            DBCmd.Parameters.Add("@CompanyUserRate", System.Data.SqlDbType.Decimal).Value = Decimal.Parse(txtCompanyUserRate.Text, CultureInfo.InvariantCulture);
            DBCmd.Parameters.Add("@CompanyBuyChipRate", System.Data.SqlDbType.Decimal).Value = Decimal.Parse(txtCompanyBuyChipRate.Text, CultureInfo.InvariantCulture);
            DBCmd.Parameters.Add("@CompanyChipsList", System.Data.SqlDbType.VarChar).Value = CompanyChipsList;
            DBCmd.Parameters.Add("@Description", System.Data.SqlDbType.VarChar).Value = txtDescription.Text;
            DBCmd.Parameters.Add("@DomainURL", System.Data.SqlDbType.VarChar).Value = txtDomainURL.Text;
            DBCmd.Parameters.Add("@CompanyCurrentTypeList", System.Data.SqlDbType.VarChar).Value = CurrentTypeList;
            DBCmd.Parameters.Add("@DefaultCurrencyType", System.Data.SqlDbType.VarChar).Value = DefaultCurrency;
            DBCmd.Parameters.Add("@GuestAccount", System.Data.SqlDbType.VarChar).Value = txtGuestAccount.Text; ;
            DBCmd.Parameters.Add("@CompanyEthWalletAddress", System.Data.SqlDbType.VarChar).Value = txtCompanyEthWalletAddress.Text;
            CompanyID = Convert.ToInt32(DBAccess.GetDBValue(EWin.DBConnStr, DBCmd));


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


            // iMgmAPI.UpdateCompanyByCompanyCode(txtCompanyCode.Text)
            RedisCache.Company.UpdateCompanyByCode(txtCompanyCode.Text);
            ExtControl.CloseActiveTab("Company_Maint.aspx", "Message", EWin.GetLanguage("儲存成功").ToString());

        }
        else
        {
            ExtControl.ShowMsg("Exception", EWin.GetLanguage("公司代碼已經存在").ToString());
        }
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
    
}