using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Web;

/// <summary>
/// RedisCache 的摘要描述
/// </summary>
public static class RedisCache {

    public static class CompanyPoint {
        private static string XMLPath = "CompanyPoint";
        private static int DBIndex = 0;

        // Key1: UserAccountPoint:<UserID>:<CurrencyType>

        public static System.Data.DataTable GetCompanyPointByID(int CompanyID) {
            string KeyPoint;
            System.Data.DataTable PointDT = null;

            KeyPoint = XMLPath + ":" + CompanyID.ToString();
            if (KeyExists(DBIndex, KeyPoint) == true) {
                PointDT = DTReadFromRedis(DBIndex, KeyPoint);
            } else {
                PointDT = UpdateCompanyPointByID(CompanyID);
            }

            return PointDT;
        }

        public static System.Data.DataTable UpdateCompanyPointByID(int CompanyID) {
            string KeyPoint;
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable PointDT;

            KeyPoint = XMLPath + ":" + CompanyID.ToString();

            SS = "SELECT C.* " +
                 "FROM CompanyPoint AS C WITH (NOLOCK) " +
                 "WHERE forCompanyID = @CompanyID";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
            PointDT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (PointDT.Rows.Count > 0) {
                for (int i = 0; i < 3; i++) {
                    try {
                        DTWriteToRedis(DBIndex, PointDT, KeyPoint);
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return PointDT;
        }
    }

    public static class Company {
        private static string XMLPath = "Company";
        private static int DBIndex = 0;

        public static System.Data.DataTable GetCompanyByID(int CompanyID) {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath + ":" + CompanyID.ToString();
            if (KeyExists(DBIndex, Key1)) {
                DT = DTReadFromRedis(DBIndex, Key1);
            } else {
                DT = UpdateCompanyByID(CompanyID);
            }

            return DT;
        }

        public static System.Data.DataTable GetCompanyByCode(string CompanyCode) {
            string Key2;
            int CompanyID = 0;
            System.Data.DataTable DT;

            Key2 = XMLPath + ":CompanyCode:" + CompanyCode;

            if (KeyExists(DBIndex, Key2)) {
                CompanyID = Convert.ToInt32(RedisRead(DBIndex, Key2));
                DT = GetCompanyByID(CompanyID);
            } else {
                DT = UpdateCompanyByCode(CompanyCode);
            }

            return DT;
        }

        public static int GetCompanyIDByCode(string CompanyCode) {
            string Key2;
            int CompanyID = 0;

            Key2 = XMLPath + ":CompanyCode:" + CompanyCode;
            if (KeyExists(DBIndex, Key2)) {
                CompanyID = Convert.ToInt32(RedisRead(DBIndex, Key2));
            } else {
                System.Data.DataTable DT;

                DT = UpdateCompanyByCode(CompanyCode);
                if (DT != null) {
                    if (DT.Rows.Count > 0) {
                        CompanyID = (int)DT.Rows[0]["CompanyID"];
                    }
                }
            }

            return CompanyID;
        }


        public static System.Data.DataTable UpdateCompanyByCode(string CompanyCode) {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key1;
            string Key2;

            Key2 = XMLPath + ":CompanyCode:" + CompanyCode;

            SS = "SELECT * FROM CompanyTable WITH (NOLOCK) WHERE CompanyCode=@CompanyCode";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CompanyCode", System.Data.SqlDbType.VarChar).Value = CompanyCode;
            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {
                int CompanyID = (int)DT.Rows[0]["CompanyID"];

                Key1 = XMLPath + ":" + CompanyID;

                for (int _I = 0; _I < 10; _I++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key1);
                        break;
                    } catch (Exception ex) {
                    }
                }

                for (int _I = 0; _I < 10; _I++) {
                    try {
                        RedisWrite(DBIndex, Key2, CompanyID.ToString());
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }

        public static System.Data.DataTable UpdateCompanyByID(int CompanyID) {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key1;
            string Key2;

            Key1 = XMLPath + ":" + CompanyID.ToString();

            SS = "SELECT * FROM CompanyTable WITH (NOLOCK) WHERE CompanyID=@CompanyID";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {
                Key2 = XMLPath + ":CompanyCode:" + DT.Rows[0]["CompanyCode"].ToString();

                for (int _I = 0; _I < 10; _I++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key1);
                        break;
                    } catch (Exception ex) {
                    }
                }

                for (int _I = 0; _I < 10; _I++) {
                    try {
                        RedisWrite(DBIndex, Key2, CompanyID.ToString());
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }
    }

    public static class CompanyService {
        private static string XMLPath = "CompanyService";
        private static int DBIndex = 0;

        public static System.Data.DataTable GetCompanyService(int CompanyID, string ServiceType, string CurrencyType) {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath + ":" + CompanyID.ToString() + "." + ServiceType + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                DT = DTReadFromRedis(DBIndex, Key1);
            } else {
                DT = UpdateCompanyService(CompanyID, ServiceType, CurrencyType);
            }

            return DT;
        }

        public static System.Data.DataTable UpdateCompanyService(int CompanyID, string ServiceType, string CurrencyType) {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key;

            Key = XMLPath + ":" + CompanyID.ToString() + "." + ServiceType + "." + CurrencyType;

            SS = " SELECT * FROM CompanyService WITH (NOLOCK) WHERE forCompanyID=@CompanyID AND ServiceType=@ServiceType AND CurrencyType=@CurrencyType ";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID.ToString();
            DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
            DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {
                for (int i = 0; i < 3; i++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key);
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }
    }

    public static class ProviderCode {
        private static string XMLPath = "ProviderCode";
        private static int DBIndex = 0;

        public static System.Data.DataTable GetProviderCode(string ProviderCode) {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath + ":" + ProviderCode;
            if (KeyExists(DBIndex, Key1)) {
                DT = DTReadFromRedis(DBIndex, Key1);
            } else {
                DT = UpdateProviderCode(ProviderCode);
            }

            return DT;
        }

        public static System.Data.DataTable UpdateProviderCode(string ProviderCode) {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key;

            Key = XMLPath + ":" + ProviderCode.ToString();

            SS = " SELECT * FROM ProviderCode WITH (NOLOCK) WHERE ProviderCode=@ProviderCode ";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {
                for (int i = 0; i < 3; i++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key);
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }
    }

    public static class ProviderService {
        private static string XMLPath = "ProviderService";
        private static int DBIndex = 0;

        public static System.Data.DataTable GetProviderService(string ProviderCode, string ServiceType, string CurrencyType) {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath + ":" + ProviderCode + "." + ServiceType + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                DT = DTReadFromRedis(DBIndex, Key1);
            } else {
                DT = UpdateProviderService(ProviderCode, ServiceType , CurrencyType);
            }

            return DT;
        }

        public static System.Data.DataTable UpdateProviderService(string ProviderCode, string ServiceType, string CurrencyType) {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key;

            Key = XMLPath + ":" + ProviderCode + "." + ServiceType + "." + CurrencyType;

            SS = "SELECT * FROM ProviderService WITH (NOLOCK) WHERE ProviderCode=@ProviderCode AND ServiceType=@ServiceType AND CurrencyType=@CurrencyType ";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
            DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
            DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {
                for (int i = 0; i < 3; i++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key);
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }
    }

    public static class GPayRelation {
        private static string XMLPath = "GPayRelation";
        private static int DBIndex = 0;

        public static System.Data.DataTable GetGPayRelation(int CompanyID, string ServiceType, string CurrencyType) {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath + ":" + CompanyID.ToString() + "." + ServiceType + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                DT = DTReadFromRedis(DBIndex, Key1);
            } else {
                DT = UpdateGPayRelation(CompanyID, ServiceType, CurrencyType);
            }

            return DT;
        }

        public static System.Data.DataTable UpdateGPayRelation(int CompanyID, string ServiceType, string CurrencyType) {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key;

            Key = XMLPath + ":" + CompanyID.ToString() + "." + ServiceType + "." + CurrencyType;

            SS = "SELECT * FROM GPayRelation WITH (NOLOCK) WHERE forCompanyID=@CompanyID AND ServiceType=@ServiceType AND CurrencyType=@CurrencyType";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
            DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
            DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {
                for (int i = 0; i < 3; i++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key);
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }

        public static void DeleteGPayRelation(int CompanyID, string ServiceType, string CurrencyType) {
            string Key1;

            Key1 = XMLPath + ":" + CompanyID.ToString() + "." + ServiceType + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                KeyDelete(DBIndex, Key1);
            } 
        }
    }

    public static class GPayWithdrawRelation {
        private static string XMLPath = "GPayWithdrawRelation";
        private static int DBIndex = 0;

        public static System.Data.DataTable GetGPayWithdrawRelation(int CompanyID, string CurrencyType) {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath + ":" + CompanyID.ToString() + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                DT = DTReadFromRedis(DBIndex, Key1);
            } else {
                DT = UpdateGPayWithdrawRelation(CompanyID, CurrencyType);
            }

            return DT;
        }

        public static System.Data.DataTable UpdateGPayWithdrawRelation(int CompanyID, string CurrencyType) {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key;

            Key = XMLPath + ":" + CompanyID.ToString() + "."  + CurrencyType;

            SS = "SELECT * FROM GPayWithdrawRelation WITH (NOLOCK) WHERE forCompanyID=@CompanyID  AND CurrencyType=@CurrencyType";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
            DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {
                for (int i = 0 ; i < 3 ; i++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key);
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }

        public static void DeleteGPayWithdrawRelation(int CompanyID, string CurrencyType) {
            string Key1;

            Key1 = XMLPath + ":" + CompanyID.ToString() +  "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                KeyDelete(DBIndex, Key1);
            }
        }
    }

    public static class CompanyWithdrawLimit {
        private static string XMLPath_Company_API = "WithdrawLimit_CompanyAPI";
        private static string XMLPath_Company_Backend = "WithdrawLimit_CompanyBackend";
        private static int DBIndex = 0;

        public static System.Data.DataTable GetCompanyAPIWithdrawLimit(int CompanyID, string CurrencyType) {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath_Company_API + ":" + CompanyID.ToString() + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                DT = DTReadFromRedis(DBIndex, Key1);
            } else {
                DT = UpdateCompanyAPIWithdrawLimit(CompanyID, CurrencyType);
            }

            return DT;
        }

        public static System.Data.DataTable GetCompanyBackendtWithdrawLimit(int CompanyID, string CurrencyType) {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath_Company_Backend + ":" + CompanyID.ToString() + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                DT = DTReadFromRedis(DBIndex, Key1);
            } else {
                DT = UpdateCompanyBackendWithdrawLimit(CompanyID, CurrencyType);
            }

            return DT;
        }
      
        public static System.Data.DataTable UpdateCompanyAPIWithdrawLimit(int CompanyID, string CurrencyType) {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key;

            Key = XMLPath_Company_API + ":" + CompanyID.ToString() + "." + CurrencyType;

            SS = "SELECT * FROM WithdrawLimit WITH (NOLOCK) WHERE WithdrawLimitType=2 AND CurrencyType=@CurrencyType AND forCompanyID=@forCompanyID";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
            DBCmd.Parameters.Add("@forCompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
    

            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {

                for (int i = 0 ; i < 3 ; i++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key);
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }

        public static System.Data.DataTable UpdateCompanyBackendWithdrawLimit(int CompanyID, string CurrencyType) {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key;

            Key = XMLPath_Company_Backend + ":" + CompanyID.ToString() + "." + CurrencyType;

            SS = "SELECT * FROM WithdrawLimit WITH (NOLOCK) WHERE WithdrawLimitType=1 AND CurrencyType=@CurrencyType AND forCompanyID=@forCompanyID";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
            DBCmd.Parameters.Add("@forCompanyID", System.Data.SqlDbType.Int).Value = CompanyID;


            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {

                for (int i = 0 ; i < 3 ; i++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key);
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }       

        public static void DeleteCompanyAPIWithdrawLimit(int CompanyID, string CurrencyType) {
            string Key1;

            Key1 = XMLPath_Company_API + ":" + CompanyID.ToString() + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                KeyDelete(DBIndex, Key1);
            }
        }

        public static void DeletCompanyBackendeWithdrawLimit(int CompanyID, string CurrencyType) {
            string Key1;

            Key1 = XMLPath_Company_Backend + ":" + CompanyID + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                KeyDelete(DBIndex, Key1);
            }
        }
    }

    public static class ProviderWithdrawLimit {
        private static string XMLPath_ProviderCode_API = "WithdrawLimit_ProviderAPI";
        private static string XMLPath_ProviderCode_Backend = "WithdrawLimit_ProviderBackend";
        private static int DBIndex = 0;
    
        public static System.Data.DataTable GetProviderAPIWithdrawLimit(string ProviderCode, string CurrencyType) {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath_ProviderCode_API + ":" + ProviderCode + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                DT = DTReadFromRedis(DBIndex, Key1);
            } else {
                DT = UpdateProviderAPIWithdrawLimit(ProviderCode, CurrencyType);
            }

            return DT;
        }

        public static System.Data.DataTable GetProviderBackendWithdrawLimit(string ProviderCode, string CurrencyType) {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath_ProviderCode_Backend + ":" + ProviderCode + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                DT = DTReadFromRedis(DBIndex, Key1);
            } else {
                DT = UpdateProviderBackendWithdrawLimit(ProviderCode, CurrencyType);
            }

            return DT;
        }

        public static System.Data.DataTable UpdateProviderAPIWithdrawLimit(string ProviderCode, string CurrencyType) {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key;

            Key = XMLPath_ProviderCode_API + ":" + ProviderCode + "." + CurrencyType;

            SS = "SELECT * FROM WithdrawLimit WITH (NOLOCK) WHERE WithdrawLimitType=0 AND CurrencyType=@CurrencyType AND ProviderCode=@ProviderCode";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
            DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;


            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {

                for (int i = 0 ; i < 3 ; i++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key);
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }

        public static System.Data.DataTable UpdateProviderBackendWithdrawLimit(string ProviderCode, string CurrencyType) {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key;

            Key = XMLPath_ProviderCode_Backend + ":" + ProviderCode + "." + CurrencyType;

            SS = "SELECT * FROM WithdrawLimit WITH (NOLOCK) WHERE WithdrawLimitType=3 AND CurrencyType=@CurrencyType AND ProviderCode=@ProviderCode";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
            DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;


            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {

                for (int i = 0 ; i < 3 ; i++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key);
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }

        public static void DeleteProviderAPIWithdrawLimit(string ProviderCode, string CurrencyType) {
            string Key1;

            Key1 = XMLPath_ProviderCode_API + ":" + ProviderCode + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                KeyDelete(DBIndex, Key1);
            }
        }

        public static void DeleteProviderBackendWithdrawLimit(string ProviderCode, string CurrencyType) {
            string Key1;

            Key1 = XMLPath_ProviderCode_Backend + ":" + ProviderCode + "." + CurrencyType;
            if (KeyExists(DBIndex, Key1)) {
                KeyDelete(DBIndex, Key1);
            }
        }
    }

    public static class BankCode {
        private static string XMLPath = "BankCode";
        private static int DBIndex = 0;

        public static System.Data.DataTable GetBankCode() {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath;
            if (KeyExists(DBIndex, Key1)) {
                DT = DTReadFromRedis(DBIndex, Key1);
            } else {
                DT = UpdateBankCode();
            }

            return DT;
        }

        public static System.Data.DataTable UpdateBankCode() {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key;

            Key = XMLPath;

            SS = "SELECT * FROM BankCode WITH (NOLOCK)";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0) {
                for (int i = 0; i < 3; i++) {
                    try {
                        DTWriteToRedis(DBIndex, DT, Key);
                        break;
                    } catch (Exception ex) {
                    }
                }
            }

            return DT;
        }
    }

    public static class WebSetting
    {
        private static string XMLPath = "WebSetting";
        private static int DBIndex = 0;

        public static System.Data.DataTable GetWebSetting(string SettingKey)
        {
            string Key1;
            System.Data.DataTable DT;

            Key1 = XMLPath + ":" + SettingKey;
            if (KeyExists(DBIndex, Key1))
            {
                DT = DTReadFromRedis(DBIndex, Key1);
            }
            else
            {
                DT = UpdateSettingKey(SettingKey);
            }

            return DT;
        }

        public static System.Data.DataTable UpdateSettingKey(string SettingKey)
        {
            string SS;
            System.Data.SqlClient.SqlCommand DBCmd;
            System.Data.DataTable DT;
            string Key;

            Key = XMLPath + ":" + SettingKey;

            SS = "SELECT * FROM WebSetting WITH (NOLOCK) WHERE SettingKey=@SettingKey ";
            DBCmd = new System.Data.SqlClient.SqlCommand();
            DBCmd.CommandText = SS;
            DBCmd.CommandType = System.Data.CommandType.Text;
            DBCmd.Parameters.Add("@SettingKey", System.Data.SqlDbType.VarChar).Value = SettingKey;
            DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

            if (DT.Rows.Count > 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        DTWriteToRedis(DBIndex, DT, Key);
                        break;
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }

            return DT;
        }
    }

    public static class CDNList
    {
        private static string Key = "iprange";
        private static int DBIndex = 0;

        public static dynamic GetCDNList(long IPToInt)
        {
            //string ListKey = Key + ":" + CompnayID.ToString();

            if (!KeyExists(DBIndex, Key))
            {
                CodingControl.UpdateCDNList();
            }

            return RedisGetSortedRange(DBIndex, Key, IPToInt);
        }

        public static void AddCDNList(string Value, long IPToInt)
        {
            //string ListKey = Key + ":" + CompnayID.ToString();

            RedisAddSortedRange(DBIndex, Key, Value, IPToInt);
        }
    }
    public static void DTWriteToRedis(int DBIndex, System.Data.DataTable DT, string Key, int ExpireTimeoutSeconds = 0) {
        string XMLContent;

        XMLContent = DTSerialize(DT);
        RedisWrite(DBIndex, Key, XMLContent, ExpireTimeoutSeconds);
    }

    public static void DSWriteToRedis(int DBIndex, System.Data.DataSet DS, string Key, int ExpireTimeoutSeconds = 0) {
        string XMLContent;

        XMLContent = DSSerialize(DS);
        RedisWrite(DBIndex, Key, XMLContent, ExpireTimeoutSeconds);
    }

    public static System.Data.DataTable DTReadFromRedis(int DBIndex, string Key) {
        string XMLContent;

        XMLContent = RedisRead(DBIndex, Key);

        return DTDeserialize(XMLContent);
    }

    public static System.Data.DataSet DSReadFromRedis(int DBIndex, string Key) {
        string XMLContent;

        XMLContent = RedisRead(DBIndex, Key);

        return DSDeserialize(XMLContent);
    }

    public static string DTSerialize(System.Data.DataTable _dt) {
        string result = string.Empty;

        if (_dt != null) {
            System.IO.StringWriter writer = new System.IO.StringWriter();

            if (string.IsNullOrEmpty(_dt.TableName)) {
                _dt.TableName = "Datatable";
            }

            _dt.WriteXml(writer, System.Data.XmlWriteMode.WriteSchema);
            result = writer.ToString();
        }

        return result;
    }

    public static string DSSerialize(System.Data.DataSet _ds) {
        string result = string.Empty;

        if (_ds != null) {
            System.IO.StringWriter writer = new System.IO.StringWriter();
            int I = 0;

            foreach (System.Data.DataTable EachTable in _ds.Tables) {
                I += 1;

                if (string.IsNullOrEmpty(EachTable.TableName)) {
                    EachTable.TableName = "Datatable." + I.ToString();
                }
            }

            _ds.WriteXml(writer, System.Data.XmlWriteMode.WriteSchema);
            result = writer.ToString();
        }

        return result;
    }

    public static System.Data.DataTable DTDeserialize(string _strData) {
        if (string.IsNullOrEmpty(_strData) == false) {
            System.Data.DataTable DT = new System.Data.DataTable();
            System.IO.StringReader StringStream = new System.IO.StringReader(_strData);

            DT.ReadXml(StringStream);

            return DT;
        } else {
            return null;
        }
    }

    public static System.Data.DataSet DSDeserialize(string _strData) {
        if (string.IsNullOrEmpty(_strData) == false) {
            System.Data.DataSet DS = new System.Data.DataSet();
            System.IO.StringReader StringStream = new System.IO.StringReader(_strData);

            DS.ReadXml(StringStream);

            return DS;
        } else {
            return null;
        }
    }

    public static void KeyDelete(int DBIndex, string Key) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);

        Client.KeyDelete(Key.ToUpper());
    }

    public static bool KeyExists(int DBIndex, string Key) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);

        return Client.KeyExists(Key.ToUpper());
    }

    public static void RedisSetExpire(int DBIndex, string Key, int ExpireTimeoutSecond) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);

        Client.KeyExpire(Key.ToUpper(), new TimeSpan(0, 0, ExpireTimeoutSecond));
    }

    public static void RedisWrite(int DBIndex, string Key, string Content, int ExpireTimeoutSecond = 0) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);

        if (ExpireTimeoutSecond == 0) {
            Client.StringSet(Key.ToUpper(), Content);
        } else {
            StackExchange.Redis.ITransaction T = Client.CreateTransaction();

            T.StringSetAsync(Key.ToUpper(), Content);
            T.KeyExpireAsync(Key.ToUpper(), new TimeSpan(0, 0, ExpireTimeoutSecond));
            T.Execute();

            T = null;
        }
    }

    public static string RedisRead(int DBIndex, string Key) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);
        string RetValue = string.Empty;

        if (Client.KeyExists(Key.ToUpper())) {
            RetValue = Client.StringGet(Key.ToUpper()).ToString();
        }

        return RetValue;
    }

    public static bool RedisHashExists(int DBIndex, string Key, string HashName) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);

        return Client.HashExists(Key.ToUpper(), HashName.ToUpper());
    }

    public static void RedisHashDelete(int DBIndex, string Key, string HashName) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);

        Client.HashDelete(Key.ToUpper(), HashName.ToUpper());
    }

    public static void RedisHashWrite(int DBIndex, string Key, string HashName, string Content, int ExpireTimeoutSecond = 0) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);

        if (ExpireTimeoutSecond == 0) {
            Client.HashSet(Key.ToUpper(), HashName.ToUpper(), Content);
        } else {
            StackExchange.Redis.ITransaction T = Client.CreateTransaction();

            T.HashSetAsync(Key.ToUpper(), HashName.ToUpper(), Content);
            T.KeyExpireAsync(Key.ToUpper(), new TimeSpan(0, 0, ExpireTimeoutSecond));
            T.Execute();

            T = null;
        }
    }

    public static string RedisHashRead(int DBIndex, string Key, string HashName) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);
        string RetValue = string.Empty;

        if (Client.KeyExists(Key.ToUpper())) {
            RetValue = Client.HashGet(Key.ToUpper(), HashName.ToUpper());
        }

        return RetValue;
    }

    public static StackExchange.Redis.HashEntry[] RedisHashReadAll(int DBIndex, string Key) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);
        StackExchange.Redis.HashEntry[] RetValue = null;

        if (Client.KeyExists(Key.ToUpper())) {
            RetValue = Client.HashGetAll(Key.ToUpper());
        }

        return RetValue;
    }

    public static void RedisEnqueue(int DBIndex, string Key, string Content) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);

        Client.ListRightPush(Key.ToUpper(), Content);
    }

    public static string RedisDequeue(int DBIndex, string Key) {
        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);
        string RetValue = null;

        if (Client.KeyExists(Key.ToUpper())) {
            RetValue = Client.ListLeftPop(Key.ToUpper()).ToString();
        }

        return RetValue;
    }


    public static void RedisAddSortedRange(int DBIndex, string Key, string Value, long score)
    {

        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);

        Client.SortedSetAdd(Key.ToUpper(), Value, score, StackExchange.Redis.When.NotExists);

    }

    public static StackExchange.Redis.RedisValue[] RedisGetSortedRange(int DBIndex, string Key, long score)
    {

        StackExchange.Redis.IDatabase Client = Pay.GetRedisClient(DBIndex);
        StackExchange.Redis.RedisValue[] RetValue = null;

        if (Client.KeyExists(Key.ToUpper()))
        {
            RetValue = Client.SortedSetRangeByScore(Key.ToUpper(), score, Double.MaxValue, StackExchange.Redis.Exclude.None, StackExchange.Redis.Order.Ascending, 0, 1, StackExchange.Redis.CommandFlags.None);
        }

        return RetValue;
    }
}