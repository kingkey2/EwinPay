using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// ReportSystem 的摘要描述
/// </summary>
public static class ReportSystem {
    static System.Collections.ArrayList iSyncRoot = new System.Collections.ArrayList();

    public static string GetPaymentHistoryByCompanyID(DateTime SummaryDate, int CompanyID) {
        string Folder;
        string Filename;
        string RetValue = string.Empty;

        Folder = GetReportFolder("/PaymentHistory/" + SummaryDate.ToString("yyyy-MM-dd") + "/ByCompanyID");
        Filename = Folder + "\\" + CompanyID.ToString() + ".json";

        if (System.IO.File.Exists(Filename)) {
            RetValue = System.IO.File.ReadAllText(Filename);
        }

        return RetValue;
    }

    public static string GetPaymentHistoryByProviderCode(DateTime SummaryDate, string ProviderCode) {
        string Folder;
        string Filename;
        string RetValue = string.Empty;

        Folder = GetReportFolder("/PaymentHistory/" + SummaryDate.ToString("yyyy-MM-dd") + "/ByProviderCode");
        Filename = Folder + "\\" + ProviderCode + ".json";

        if (System.IO.File.Exists(Filename)) {
            RetValue = System.IO.File.ReadAllText(Filename);
        }

        return RetValue;
    }

    public static string GetPaymentHistoryByAll(DateTime SummaryDate) {
        string Folder;
        string Filename;
        string RetValue = string.Empty;

        Folder = GetReportFolder("/PaymentHistory/" + SummaryDate.ToString("yyyy-MM-dd"));
        Filename = Folder + "\\" + "All.json";

        if (System.IO.File.Exists(Filename)) {
            RetValue = System.IO.File.ReadAllText(Filename);
        }

        return RetValue;
    }

    public static string GetWithdrawalHistoryByCompanyID(DateTime SummaryDate, int CompanyID) {
        string Folder;
        string Filename;
        string RetValue = string.Empty;

        Folder = GetReportFolder("/WithdrawalHistory/" + SummaryDate.ToString("yyyy-MM-dd") + "/ByCompanyID");
        Filename = Folder + "\\" + CompanyID.ToString() + ".json";

        if (System.IO.File.Exists(Filename)) {
            RetValue = System.IO.File.ReadAllText(Filename);
        }

        return RetValue;
    }

    public static string GetWithdrawalHistory(DateTime SummaryDate) {
        string Folder;
        string Filename;
        string RetValue = string.Empty;

        Folder = GetReportFolder("/WithdrawalHistory/" + SummaryDate.ToString("yyyy-MM-dd"));
        Filename = Folder + "\\" + "All.json";

        if (System.IO.File.Exists(Filename)) {
            RetValue = System.IO.File.ReadAllText(Filename);
        }

        return RetValue;
    }

    public static string GetSummaryCompanyByDate(DateTime SummaryDate, int CompanyID) {
        string Folder;
        string Filename;
        string RetValue = string.Empty;


        Folder = GetReportFolder("/SummaryCompanyByDate/" + CompanyID.ToString());
        Filename = Folder + "\\" + SummaryDate.ToString("yyyy-MM-dd") + ".json";
        if (System.IO.File.Exists(Filename)) {
            RetValue = System.IO.File.ReadAllText(Filename);
        }

        return RetValue;
    }

    public static string GetSummaryProviderByDate(DateTime SummaryDate, string ProviderCode) {
        string Folder;
        string Filename;
        string RetValue = string.Empty;


        Folder = GetReportFolder("/SummaryProviderByDate/" + ProviderCode);
        Filename = Folder + "\\" + SummaryDate.ToString("yyyy-MM-dd") + ".json";
        if (System.IO.File.Exists(Filename)) {
            RetValue = System.IO.File.ReadAllText(Filename);
        }

        return RetValue;
    }


    public static void CreatePaymentHistory(int PaymentID) {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;
        System.Data.DataTable OrderDT;
        System.Data.DataTable SummaryDT;
        string SummaryContent = string.Empty;
        string Folder;
        string Filename;
        string SummaryDateString = string.Empty;
        //System.Collections.Generic.Dictionary<int, string> SummaryDict = new Dictionary<int, string>();

        SS = "SELECT P.* ,  " +
             "       dbo.GetReportDate(ISNULL(P.OrderDate, P.CreateDate)) AS SummaryDate, " +
             "       S.ServiceTypeName, " +
             "       PC.ProviderName, " +
             "       B.BankName, " +
             "       B.BankType, " +
             "       C.CompanyName, " +
             "       C.CompanyCode, " +
             "       C.MerchantCode " +
             "FROM PaymentTable AS P " +
             "LEFT JOIN ServiceType AS S ON P.ServiceType = S.ServiceType " +
             "LEFT JOIN ProviderCode AS PC ON PC.ProviderCode = P.ProviderCode " +
             "LEFT JOIN CompanyTable AS C  ON C.CompanyID = P.forCompanyID " +
             "LEFT JOIN BankCode AS B ON B.BankCode = P.BankCode " +
             "WHERE P.PaymentID = @PaymentID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@PaymentID", System.Data.SqlDbType.Int).Value = PaymentID;
        OrderDT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);
        foreach (System.Data.DataRow DR in OrderDT.Rows) {
            string Content = GetPaymentHistoryJSON(DR);


            SummaryDateString = ((DateTime)DR["SummaryDate"]).ToString("yyyy-MM-dd");

            Folder = PrepareReportFolder("/PaymentHistory/" + SummaryDateString + "/ByCompanyID");
            Filename = Folder + "\\" + ((int)DR["forCompanyID"]).ToString() + ".json";
            //AppendAllText(Filename, Content);
            CheckAndAppendJSONRecord(Filename, Content, "PaymentID");

            Folder = PrepareReportFolder("/PaymentHistory/" + SummaryDateString);
            Filename = Folder + "\\" + "All.json";
            //AppendAllText(Filename, Content);
            CheckAndAppendJSONRecord(Filename, Content, "PaymentID");

            Folder = PrepareReportFolder("/PaymentHistory/" + SummaryDateString + "/ByProviderCode/");
            Filename = Folder + "\\" + (string)DR["ProviderCode"] + ".json";
            //AppendAllText(Filename, Content);
            CheckAndAppendJSONRecord(Filename, Content, "PaymentID");

        }

        //SS = "SELECT SC.*, " +
        //           "       S.ServiceTypeName, " +
        //           "       C.CompanyCode, " +
        //           "       C.CompanyName  " +
        //           "FROM SummaryCompanyByDate SC " +
        //           "LEFT JOIN CompanyTable AS C ON SC.forCompanyID = C.CompanyID " +
        //           "LEFT JOIN ServiceType AS S ON S.ServiceType = SC.ServiceType " +
        //           "WHERE SC.forCompanyID = (SELECT PaymentTable.forCompanyID FROM PaymentTable WHERE PaymentID = @PaymentID) ";
        //DBCmd = new System.Data.SqlClient.SqlCommand();
        //DBCmd.CommandText = SS;
        //DBCmd.CommandType = System.Data.CommandType.Text;
        //DBCmd.Parameters.Add("@PaymentID", System.Data.SqlDbType.Int).Value = PaymentID;
        //SummaryDT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);
        //foreach (System.Data.DataRow DR in SummaryDT.Rows) {
        //    SummaryDateString = ((DateTime)DR["SummaryDate"]).ToString("yyyy-MM-dd");

        //    SummaryContent = "{\"SummaryDate\":\"" + SummaryDateString + "\"," +
        //              "\"forCompanyID\":" + ((int)DR["forCompanyID"]).ToString() + "," +
        //              "\"CurrencyType\":\"" + (string)DR["CurrencyType"] + "\"," +
        //              "\"ServiceType\":\"" + (string)DR["ServiceType"] + "\"," +
        //              "\"CompanyCode\":\"" + (string)DR["CompanyCode"] + "\"," +
        //              "\"CompanyName\":\"" + (string)DR["CompanyName"] + "\"," +
        //              "\"SuccessCount\":" + ((int)DR["SuccessCount"]).ToString() + "," +
        //              "\"TotalCount\":" + ((int)DR["TotalCount"]).ToString() + "," +
        //              "\"SummaryAmount\":" + ((decimal)DR["SummaryAmount"]).ToString() + "," +
        //              "\"SummaryNetAmount\":" + ((decimal)DR["SummaryNetAmount"]).ToString() + "," +
        //              "\"SummaryAgentAmount\":" + ((decimal)DR["SummaryAgentAmount"]).ToString() + "}\r\n";

        //    if (string.IsNullOrEmpty(SummaryContent) == false) {
        //        Folder = PrepareReportFolder("/SummaryCompanyByDate/" + ((int)DR["forCompanyID"]).ToString());
        //        Filename = Folder + "\\" + SummaryDateString + ".json";
        //        WriteAllText(Filename, SummaryContent);
        //    }
        //}

        //SS = "SELECT SP.*, " +
        //    "       S.ServiceTypeName, " +
        //    "       P.ProviderName  " +
        //    "FROM SummaryProviderByDate SP " +
        //    "LEFT JOIN ProviderCode AS P ON SP.ProviderCode = P.ProviderCode " +
        //    "LEFT JOIN ServiceType AS S ON S.ServiceType = SP.ServiceType " +
        //    "WHERE SP.ProviderCode = (SELECT PaymentTable.ProviderCode FROM PaymentTable WHERE PaymentID = @PaymentID) ";
        //DBCmd = new System.Data.SqlClient.SqlCommand();
        //DBCmd.CommandText = SS;
        //DBCmd.CommandType = System.Data.CommandType.Text;
        //DBCmd.Parameters.Add("@PaymentID", System.Data.SqlDbType.Int).Value = PaymentID;
        //SummaryDT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        //foreach (System.Data.DataRow DR in SummaryDT.Rows) {
        //    SummaryDateString = ((DateTime)DR["SummaryDate"]).ToString("yyyy-MM-dd");

        //    SummaryContent = "{\"SummaryDate\":\"" + SummaryDateString + "\"," +
        //              "\"ProviderCode\":\"" + (string)DR["ProviderCode"] + "\"," +
        //              "\"CurrencyType\":\"" + (string)DR["CurrencyType"] + "\"," +
        //              "\"ServiceType\":\"" + (string)DR["ServiceType"] + "\"," +
        //              "\"ProviderName\":\"" + (string)DR["ProviderName"] + "\"," +
        //              "\"SuccessCount\":" + ((int)DR["SuccessCount"]).ToString() + "," +
        //              "\"TotalCount\":" + ((int)DR["TotalCount"]).ToString() + "," +
        //              "\"SummaryAmount\":" + ((decimal)DR["SummaryAmount"]).ToString() + "," +
        //              "\"SummaryNetAmount\":" + ((decimal)DR["SummaryNetAmount"]).ToString() + "}\r\n";

        //    if (string.IsNullOrEmpty(SummaryContent) == false) {
        //        Folder = PrepareReportFolder("/SummaryProviderByDate/" + (string)DR["ProviderCode"]);
        //        Filename = Folder + "\\" + SummaryDateString + ".json";
        //        WriteAllText(Filename, SummaryContent);
        //    }
        //}
    }

    public static void CreateWithdrawalHistory(int WithdrawID) {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;
        System.Data.DataTable DT;
        string Folder;
        string Filename;
        string SummaryDateString = string.Empty;
        //System.Collections.Generic.Dictionary<int, string> SummaryDict = new Dictionary<int, string>();

        SS = "SELECT W.*,  " +
             "  dbo.GetReportDate(W.CreateDate) AS SummaryDate,  " +
             "  C.CompanyCode, " +
             "  C.CompanyName, " +
             "  BC.BankCardName, " +
             "  BC.BankBranchName, " +
             "  BC.OwnProvince, " +
             "  BC.OwnCity, " +
             "  B.BankName, " +
             "  B.BankType " +
             "FROM Withdrawal AS W " +
             "LEFT JOIN CompanyTable AS C ON C.CompanyID = W.forCompanyID " +
             "LEFT JOIN BankCard AS BC ON BC.BankCard = W.BankCard " +
             "LEFT JOIN BankCode AS B ON BC.BankCode = B.BankCode " +
             "WHERE W.WithdrawID = @WithdrawID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@WithdrawID", System.Data.SqlDbType.Int).Value = WithdrawID;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);
        foreach (System.Data.DataRow DR in DT.Rows) {
            SummaryDateString = ((DateTime)DR["SummaryDate"]).ToString("yyyy-MM-dd");

            string FinishDate = "";
            string FinishTime = "";

            if (!Convert.IsDBNull(DR["FinishDate"])) {
                FinishDate = ((DateTime)DR["FinishDate"]).ToString("yyyy-MM-dd");
                FinishTime = ((DateTime)DR["FinishDate"]).ToString("HH:mm:ss");
            }

            string Content = "{\"SummaryDate\":\"" + SummaryDateString + "\"," +
                             "\"CreateDate\":\"" + ((DateTime)DR["CreateDate"]).ToString("yyyy-MM-dd") + "\"," +
                             "\"CreateTime\":\"" + ((DateTime)DR["CreateDate"]).ToString("HH:mm:ss") + "\"," +
                             "\"WithdrawID\":" + ((int)DR["WithdrawID"]).ToString() + "," +
                             "\"forCompanyID\":" + ((int)DR["forCompanyID"]).ToString() + "," +
                             "\"CurrencyType\":\"" + (string)DR["CurrencyType"] + "\"," +
                             "\"Amount\":" + ((decimal)DR["Amount"]).ToString() + "," +
                             "\"FinishDate\":\"" + FinishDate + "\"," +
                             "\"FinishTime\":\"" + FinishTime + "\"," +
                             "\"Status\":" + ((int)DR["Status"]).ToString() + "," +
                             "\"BankCard\":" + ((int)DR["BankCard"]).ToString() + "," +
                             "\"WithdrawSerial\":\"" + (string)DR["WithdrawSerial"] + "\"," +
                             "\"CompanyCode\":\"" + (string)DR["CompanyCode"] + "\"," +
                             "\"CompanyName\":\"" + (string)DR["CompanyName"] + "\"," +
                             "\"BankCardName\":\"" + (string)DR["BankCardName"] + "\"," +
                             "\"BankName\":\"" + (string)DR["BankName"] + "\"," +
                             "\"OwnProvince\":\"" + DR["OwnProvince"].ToString() + "\"," +
                             "\"OwnCity\":\"" + DR["OwnCity"].ToString() + "\"," +
                             "\"BankBranchName\":\"" + DR["BankBranchName"].ToString() + "\"," +
                             "\"BankType\":" + ((int)DR["BankType"]).ToString() + "}\r\n";

            if (string.IsNullOrEmpty(Content) == false) {
                Folder = PrepareReportFolder("/WithdrawalHistory/" + SummaryDateString + "/ByCompanyID");
                Filename = Folder + "\\" + ((int)DR["forCompanyID"]).ToString() + ".json";
                CheckAndAppendJSONRecord(Filename, Content, "WithdrawID");

                Folder = PrepareReportFolder("/WithdrawalHistory/" + SummaryDateString);
                Filename = Folder + "\\" + "All.json";

                CheckAndAppendJSONRecord(Filename, Content, "WithdrawID");
            }

        }
    }

    private static void CheckAndAppendJSONRecord(string Filename, string NewRecordJSON, string KeyField) {
        string AllContent = null;
        System.Text.StringBuilder DestJSON = new System.Text.StringBuilder();
        bool newRecordIsAppend = false;

        if (System.IO.File.Exists(Filename)) {
            AllContent = System.IO.File.ReadAllText(Filename);
        }

        if (string.IsNullOrEmpty(AllContent) == false) {
            Newtonsoft.Json.Linq.JObject new_o = null;

            try {
                new_o = Newtonsoft.Json.Linq.JObject.Parse(NewRecordJSON);
            } catch (Exception ex) {
            }

            if (new_o != null) {
                object new_KeyValue = Pay.GetJValue(new_o, KeyField);

                foreach (string EachJSON in AllContent.Split('\r', '\n')) {
                    if (string.IsNullOrEmpty(EachJSON) == false) {
                        bool stringIsExist = false;
                        string tmpJSON = EachJSON.Replace("\r", "").Replace("\n", "");
                        Newtonsoft.Json.Linq.JObject src_o = null;

                        try {
                            src_o = Newtonsoft.Json.Linq.JObject.Parse(tmpJSON);
                        } catch (Exception ex) {
                        }

                        if (src_o != null) {
                            object src_KeyValue = Pay.GetJValue(src_o, KeyField);

                            if (src_KeyValue != null) {
                                if (Convert.ToString(src_KeyValue) == Convert.ToString(new_KeyValue)) {
                                    stringIsExist = true;
                                }
                            }
                        }

                        if (stringIsExist == false) {
                            DestJSON.Append(tmpJSON + "\r\n");
                        } else {
                            DestJSON.Append(NewRecordJSON);
                            newRecordIsAppend = true;
                        }
                    }
                }
            }
        }

        if (newRecordIsAppend == false)
            DestJSON.Append(NewRecordJSON);

        WriteAllText(Filename, DestJSON.ToString());
    }

    private static void AppendAllText(string Filename, string Content) {
        Exception throwEx = null;

        for (var i = 0; i < 100; i++) {
            lock (iSyncRoot) {
                try {
                    System.IO.File.AppendAllText(Filename, Content);
                    throwEx = null;
                    break;
                } catch (Exception ex) {
                    throwEx = ex;
                }
            }

            System.Threading.Thread.Sleep(100);
        }

        if (throwEx != null)
            throw throwEx;
    }

    private static void WriteAllText(string Filename, string Content) {
        Exception throwEx = null;

        for (var i = 0; i < 100; i++) {
            lock (iSyncRoot) {
                try {
                    System.IO.File.WriteAllText(Filename, Content);
                    throwEx = null;
                    break;
                } catch (Exception ex) {
                    throwEx = ex;
                }
            }

            System.Threading.Thread.Sleep(100);
        }

        if (throwEx != null)
            throw new Exception(throwEx.ToString() + "\r\n" + "  Filename:" + Filename);
    }

    private static string GetPaymentHistoryJSON(System.Data.DataRow DR) {
        string Content = string.Empty;
        string SummaryDate;
        string FinishDate = "";
        string FinishTime = "";

        if (DR["SummaryDate"] != DBNull.Value) {
            SummaryDate = ((DateTime)DR["SummaryDate"]).ToString("yyyy-MM-dd");
        } else {
            SummaryDate = ((DateTime)DR["CreateDate"]).ToString("yyyy-MM-dd");
        }

        if (DR["FinishDate"] != DBNull.Value)
        {
            FinishDate = ((DateTime)DR["FinishDate"]).ToString("yyyy-MM-dd");
            FinishTime = ((DateTime)DR["FinishDate"]).ToString("HH:mm:ss");
        }

        Content = "{\"CreateDate\":\"" + ((DateTime)DR["CreateDate"]).ToString("yyyy-MM-dd") + "\"," +
                  "\"CreateTime\":\"" + ((DateTime)DR["CreateDate"]).ToString("HH:mm:ss") + "\"," +
                  "\"OrderDate\":\"" + ((DateTime)DR["OrderDate"]).ToString("yyyy-MM-dd") + "\"," +
                  "\"OrderTime\":\"" + ((DateTime)DR["OrderDate"]).ToString("HH:mm:ss") + "\"," +
                  "\"FinishDate\":\"" + FinishDate + "\"," +
                  "\"FinishTime\":\"" + FinishTime + "\"," +
                  "\"SummaryDate\":\"" + SummaryDate + "\"," +
                  "\"PaymentID\":" + ((int)DR["PaymentID"]).ToString() + "," +
                  "\"forCompanyID\":" + ((int)DR["forCompanyID"]).ToString() + "," +
                  "\"PaymentSerial\":\"" + (string)DR["PaymentSerial"] + "\"," +
                  "\"CurrencyType\":\"" + (string)DR["CurrencyType"] + "\"," +
                  "\"ServiceType\":\"" + (string)DR["ServiceType"] + "\"," +
                  "\"BankCode\":\"" + (string)DR["BankCode"] + "\"," +
                  "\"ProviderCode\":\"" + (string)DR["ProviderCode"] + "\"," +
                  "\"PatchDescription\":\"" + DR["PatchDescription"].ToString() + "\"," +
                  "\"MerchantCode\":\"" + DR["MerchantCode"].ToString() + "\"," +
                  "\"forPaymentSerial\":\"" + DR["forPaymentSerial"].ToString() + "\"," +
                  "\"ProcessStatus\":" + ((int)DR["ProcessStatus"]).ToString() + "," +
                  "\"ReturnURL\":\"" + (string)DR["ReturnURL"] + "\"," +
                  "\"PaymentResult\":" + (Convert.IsDBNull(DR["PaymentResult"]) ? "null" : DR["PaymentResult"].ToString()) + "," +
                  "\"State\":\"" + (string)DR["State"] + "\"," +
                  "\"BankSequenceID\":\"" + (string)DR["BankSequenceID"] + "\"," +
                  "\"ClientIP\":\"" + (string)DR["ClientIP"] + "\"," +
                  "\"OrderID\":\"" + (string)DR["OrderID"] + "\"," +
                  "\"OrderAmount\":" + ((decimal)DR["OrderAmount"]).ToString() + "," +
                  "\"PartialOrderAmount\":" + ((decimal)DR["PartialOrderAmount"]).ToString() + "," +
                  "\"PaymentAmount\":" + ((decimal)DR["PaymentAmount"]).ToString() + "," +
                  "\"CostRate\":" + ((decimal)DR["CostRate"]).ToString() + "," +
                  "\"CostCharge\":" + ((decimal)DR["CostCharge"]).ToString() + "," +
                  "\"CollectRate\":" + ((decimal)DR["CollectRate"]).ToString() + "," +
                  "\"CollectCharge\":" + ((decimal)DR["CollectCharge"]).ToString() + "," +
                  "\"Accounting\":" + ((int)DR["Accounting"]).ToString() + "," +
                  "\"ServiceTypeName\":\"" + (string)DR["ServiceTypeName"] + "\"," +
                  "\"ProviderName\":\"" + (string)DR["ProviderName"] + "\"," +
                  "\"ProviderOrderID\":\"" + (string)DR["ProviderOrderID"] + "\"," +
                  "\"BankName\":\"" + (Convert.IsDBNull(DR["BankName"]) ? "" : DR["BankName"].ToString()) + "\"," +
                  "\"BankType\":" + (Convert.IsDBNull(DR["BankType"]) ? "-1" : ((int)DR["BankType"]).ToString()) + "," +
                  "\"CompanyName\":\"" + (string)DR["CompanyName"] + "\"," +
                  "\"CompanyCode\":\"" + (string)DR["CompanyCode"] + "\"}\r\n";


        return Content;
    }

    private static string PrepareReportFolder(string C) {
        string ReportFolder;

        lock (iSyncRoot) {
            ReportFolder = GetReportFolder(C);
            if (System.IO.Directory.Exists(ReportFolder) == false) {
                try { System.IO.Directory.CreateDirectory(ReportFolder); } catch (Exception ex) { }
            }
        }

        return ReportFolder;
    }

    private static string GetReportFolder(string C) {
        string ReportFolder;

        ReportFolder = Pay.SharedFolder + C.Replace('/', '\\');

        return ReportFolder;
    }
}