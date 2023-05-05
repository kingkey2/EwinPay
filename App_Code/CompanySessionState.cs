using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// CompanySessionState 的摘要描述
/// </summary>
public class CompanySessionState
{
    public enum enumLoginState
    {
        None = 0,
        Logined,
        AccountIsLocked
    }

    public AdminAccountInfo AdminAccount;
    public CompanyInfo Company;
    public enumLoginState LoginState = enumLoginState.None;


    public class AdminAccountInfo
    {
        public int AdminID;
        public string LoginAccount;
        public string RealName;
        public int AdminRoleID;
        public string[] PermissionList;

        public bool CheckAdminHasPermission(string PermissionName)
        {
            var RetValue = false;

            foreach (string EachPermission in PermissionList)
            {
                if (EachPermission.Trim().ToUpper() == PermissionName.Trim().ToUpper())
                {
                    RetValue = true;
                    break;
                }
            }

            return RetValue;
        }
    }

    public class CompanyInfo
    {
        public int CompanyID;
        public string CompanyCode;
    }
}