using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// EWinDB 的摘要描述
/// </summary>
public static class PayDB
{
    public enum enumPaymentType
    {
        CNBankCard,
        CreditCard,
        Paypal,
        WechatCode,
        WechatH5,
        AlipayCode,
        AlipayH5
    }

    #region Company

    public static System.Data.DataTable GetCompanyByCode(string CompanyCode, bool IsAll)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;
        if (IsAll)
        {
            SS = "SELECT * FROM CompanyTable WITH (NOLOCK) WHERE CompanyCode=@CompanyCode";
        }
        else
        {
            SS = "SELECT * FROM CompanyTable WITH (NOLOCK) WHERE CompanyCode=@CompanyCode AND CompanyState = 0";
        }
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyCode", System.Data.SqlDbType.VarChar).Value = CompanyCode;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetCompanyByID(int CompanyID, bool CheckCanUse)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;
        if (CheckCanUse)
        {
            SS = "SELECT * FROM CompanyTable WITH (NOLOCK) WHERE CompanyID=@CompanyID";
        }
        else
        {
            SS = "SELECT * FROM CompanyTable WITH (NOLOCK) WHERE CompanyID=@CompanyID AND CompanyState = 0";
        }
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetCompanyPoint(int CompanyID, string CurrencyType)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;


        SS = "SELECT * FROM CompanyPoint WITH (NOLOCK) WHERE forCompanyID=@CompanyID AND CurrencyType=@CurrencyType";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }


    public static System.Data.DataTable GetCompanyServicePoint(int CompanyID, string CurrencyType, string ServiceType)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = " SELECT(CSP.SystemPointValue - (SELECT ISNULL(SUM(W.Amount + W.CollectCharge), 0)" +
              " FROM Withdrawal W " +
              " WHERE W.Status <> 2 " +
              " AND W.Status <> 3 " +
              " AND W.Status <> 90 " +
              " AND W.Status <> 91 " +
              " AND W.forCompanyID = CSP.CompanyID " +
              " AND W.ServiceType = CSP.ServiceType " +
              " AND W.CurrencyType = CSP.CurrencyType)) AS CanUsePoint, " +
              " ISNULL((Select SUM(CompanyFrozenAmount) FROM " +
              " FrozenPoint where FrozenPoint.forCompanyID = CSP.CompanyID " +
              " AND FrozenPoint.CurrencyType = CSP.CurrencyType " +
              " AND FrozenPoint.ServiceType = CSP.ServiceType " +
              " AND FrozenPoint.Status = 0),0) AS FrozenPoint," +
              " ServiceTypeName,WithdrawLimit.MaxLimit,WithdrawLimit.MinLimit,WithdrawLimit.Charge, " +
              " CSP.*" +
              " FROM CompanyServicePoint AS CSP" +
              " LEFT JOIN ServiceType ON CSP.ServiceType=ServiceType.ServiceType" +
              " LEFT JOIN WithdrawLimit ON WithdrawLimit.ServiceType=CSP.ServiceType And WithdrawLimit.CurrencyType= CSP.CurrencyType And WithdrawLimit.WithdrawLimitType=1 And CSP.CompanyID=WithdrawLimit.forCompanyID" +
              " WHERE CSP.CompanyID = @CompanyID " +
              " AND CSP.CurrencyType = @CurrencyType " +
              " AND CSP.ServiceType = @ServiceType ";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }


    public static System.Data.DataTable GetCanUseCompanyPoint(int CompanyID, string CurrencyType)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;


        SS = " SELECT (CP.PointValue " +
               "	-(SELECT ISNULL(SUM(W.Amount + W.CollectCharge),0) " +
               "   	   FROM Withdrawal W " +
               "	   WHERE W.Status <> 2 AND W.Status <> 3 AND W.Status <> 8 AND W.Status <> 90 AND W.Status <> 91 AND W.forCompanyID = CP.forCompanyID AND W.CurrencyType = CP.CurrencyType)" +
               "	-(SELECT ISNULL(SUM(SC.SummaryNetAmount),0) " +
               "	   FROM CompanyService CS " +
               "	   INNER JOIN SummaryCompanyByDate SC ON CS.forCompanyID = SC.forCompanyID AND CS.CurrencyType = SC.CurrencyType AND SC.ServiceType = CS.ServiceType " +
               "			AND   ((CS.CheckoutType = 0 AND 1 = 0 ) " +
               "			      OR (CS.CheckoutType = 1 AND SC.SummaryDate = dbo.GetReportDate(GETDATE()))" +
               "			      OR (CS.CheckoutType = 2 AND DATEPART(WEEKDAY, GETDATE()-1) = 7 AND " +
               "	                  (SC.SummaryDate = dbo.GetReportDate(GETDATE()) OR SC.SummaryDate =  dbo.GetReportDate(DATEADD(day, -1, getdate()))))" +
               "				  OR (CS.CheckoutType = 2 AND DATEPART(WEEKDAY, GETDATE()-1) = 6 AND  SC.SummaryDate = dbo.GetReportDate(GETDATE())))" +
               "	   WHERE CS.forCompanyID = CP.forCompanyID AND CS.CurrencyType = CP.CurrencyType)) AS CanUsePoint, " +
               " 	 ISNULL((Select SUM(CompanyFrozenAmount) FROM FrozenPoint where FrozenPoint.forCompanyID=CP.forCompanyID AND FrozenPoint.CurrencyType=CP.CurrencyType AND FrozenPoint.Status=0),0) AS FrozenPoint," +
               "	   CP.* " +
               " FROM CompanyPoint AS CP " +
               " WHERE CP.forCompanyID = @CompanyID " +
               " AND CP.CurrencyType = @CurrencyType ";


        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetCompanyService(int CompanyID, string CurrencyType, string ServiceType, bool IsAll)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        if (IsAll)
        {
            SS = "SELECT * FROM CompanyService WITH (NOLOCK) WHERE forCompanyID=@CompanyID AND CurrencyType=@CurrencyType AND ServiceType=@ServiceType";
        }
        else
        {
            SS = "SELECT * FROM CompanyService WITH (NOLOCK) WHERE forCompanyID=@CompanyID AND CurrencyType=@CurrencyType AND ServiceType=@ServiceType AND State=0";
        }

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetCompanyServiceAll(int CompanyID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;
        SS = "SELECT C.* ,S.ServicePaymentType ,S.ServiceSupplyType ,S.ServiceTypeName FROM CompanyService C WITH (NOLOCK) LEFT JOIN ServiceType AS S ON S.ServiceType = C.ServiceType WHERE forCompanyID=@CompanyID AND C.State = 0";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetCompanySummary(DateTime SummaryDate, int CompanyID, string CurrencyType, string ServiceType)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;


        SS = "SELECT * FROM SummaryCompanyByDate WITH (NOLOCK) WHERE SummaryDate=@SummaryDate AND forCompanyID=@CompanyID AND CurrencyType=@CurrencyType AND ServiceType=@ServiceType";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@SummaryDate", System.Data.SqlDbType.DateTime).Value = SummaryDate;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    #endregion

    #region CompanyPoint
    public static System.Data.DataTable GetCompanyPointByID(int CompanyID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;
        System.Data.DataTable PointDT;

        SS = "SELECT C.* " +
             "FROM CompanyPoint AS C WITH (NOLOCK) " +
             "WHERE forCompanyID = @CompanyID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        PointDT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return PointDT;
    }
    #endregion

    #region Payment

    public static System.Data.DataTable GetPaymentByCompanyOrderID(int CompanyID, string OrderID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = "SELECT * FROM PaymentTable WITH (NOLOCK) WHERE forCompanyID=@CompanyID AND OrderID=@OrderID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DBCmd.Parameters.Add("@OrderID", System.Data.SqlDbType.VarChar).Value = OrderID;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetPaymentByPaymentID(int PaymentID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = "SELECT * FROM PaymentTable WITH (NOLOCK) WHERE PaymentID=@PaymentID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@PaymentID", System.Data.SqlDbType.Int).Value = PaymentID;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetPaymentByProviderOrderID(string ProviderOrderID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = "SELECT * FROM PaymentTable WITH (NOLOCK) WHERE ProviderOrderID=@ProviderOrderID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@ProviderOrderID", System.Data.SqlDbType.VarChar).Value = ProviderOrderID;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetPaymentByPaymentID(string PaymentSerial)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = "SELECT * FROM PaymentTable WITH (NOLOCK) WHERE PaymentSerial=@PaymentSerial";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@PaymentSerial", System.Data.SqlDbType.VarChar).Value = PaymentSerial;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static int InsertPayment(GatewayCommon.Payment payment)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int PaymentID;


        SS = " IF NOT EXISTS " +
             " (SELECT  1 " +
             " FROM PaymentTable " +
             " WHERE OrderID = @OrderID " +
             " AND forCompanyID = @CompanyID " +
             " ) " +
             "BEGIN" +
             " INSERT INTO PaymentTable (forCompanyID, CurrencyType, ServiceType, BankCode, ProviderCode," +
             "                          ProcessStatus, ReturnURL, State, ClientIP,UserIP, OrderID," +
             "                          OrderDate, OrderAmount, CostRate, CostCharge, CollectRate, CollectCharge, Accounting,UserName)" +
             "                  VALUES (@CompanyID, @CurrencyType, @ServiceType, @BankCode, @ProviderCode, @ProcessStatus, @ReturnURL," +
             "                          @State, @ClientIP,@UserIP, @OrderID, @OrderDate, @OrderAmount," +
             "                          @CostRate, @CostCharge, @CollectRate, @CollectCharge, @Accounting,@UserName)" +
             "  END;" +
             " IF @@ROWCOUNT = 0 " +
             " SELECT @@ROWCOUNT " +
             " ELSE " +
             " SELECT @@IDENTITY ";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = payment.forCompanyID;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = payment.CurrencyType;
        DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = payment.ServiceType;
        DBCmd.Parameters.Add("@BankCode", System.Data.SqlDbType.VarChar).Value = payment.BankCode;
        DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = payment.ProviderCode;
        DBCmd.Parameters.Add("@ProcessStatus", System.Data.SqlDbType.Int).Value = 0;
        DBCmd.Parameters.Add("@ReturnURL", System.Data.SqlDbType.VarChar).Value = payment.ReturnURL;
        DBCmd.Parameters.Add("@State", System.Data.SqlDbType.VarChar).Value = payment.State;
        DBCmd.Parameters.Add("@ClientIP", System.Data.SqlDbType.VarChar).Value = payment.ClientIP;
        DBCmd.Parameters.Add("@UserIP", System.Data.SqlDbType.VarChar).Value = payment.UserIP;
        DBCmd.Parameters.Add("@OrderID", System.Data.SqlDbType.VarChar).Value = payment.OrderID;
        DBCmd.Parameters.Add("@OrderDate", System.Data.SqlDbType.DateTime).Value = payment.OrderDate;
        DBCmd.Parameters.Add("@OrderAmount", System.Data.SqlDbType.Decimal).Value = payment.OrderAmount;
        DBCmd.Parameters.Add("@CostRate", System.Data.SqlDbType.Decimal).Value = payment.CostRate;
        DBCmd.Parameters.Add("@CostCharge", System.Data.SqlDbType.Decimal).Value = payment.CostCharge;
        DBCmd.Parameters.Add("@CollectRate", System.Data.SqlDbType.Decimal).Value = payment.CollectRate;
        DBCmd.Parameters.Add("@CollectCharge", System.Data.SqlDbType.Decimal).Value = payment.CollectCharge;
        DBCmd.Parameters.Add("@UserName", System.Data.SqlDbType.NVarChar).Value =string.IsNullOrEmpty(payment.UserName)?"": payment.UserName;
        DBCmd.Parameters.Add("@Accounting", System.Data.SqlDbType.Int).Value = 0;

        PaymentID = Convert.ToInt32(DBAccess.GetDBValue(Pay.DBConnStr, DBCmd));
        //int.TryParse(DBAccess.GetDBValue(Pay.DBConnStr, DBCmd).ToString(), out PaymentID);

        return PaymentID;
    }

    public static int UpdatePaymentSerial(string PaymentSerial, int PaymentID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE PaymentTable SET PaymentSerial=@PaymentSerial, ProcessStatus=1 WHERE ProcessStatus=0 AND PaymentID=@PaymentID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@PaymentSerial", System.Data.SqlDbType.VarChar).Value = PaymentSerial;
        DBCmd.Parameters.Add("@PaymentID", System.Data.SqlDbType.Int).Value = PaymentID;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdatePaymentProcessStatus(string PaymentSerial, int ProcessStatus)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE PaymentTable SET ProcessStatus=@ProcessStatus, ProcessStatus=1 WHERE PaymentSerial=@PaymentSerial";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@PaymentSerial", System.Data.SqlDbType.VarChar).Value = PaymentSerial;
        DBCmd.Parameters.Add("@ProcessStatus", System.Data.SqlDbType.Int).Value = ProcessStatus;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdatePaymentUserName(int PaymentID,string UserName)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE PaymentTable SET UserName=@UserName WHERE PaymentID=@PaymentID And ProcessStatus=1 And (UserName is null or UserName = '')";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@PaymentID", System.Data.SqlDbType.Int).Value = PaymentID;
        DBCmd.Parameters.Add("@UserName", System.Data.SqlDbType.NVarChar).Value = UserName;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdatePaymentComplete(int PaymentID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE PaymentTable SET ProcessStatus=4 WHERE ProcessStatus=2 AND PaymentID=@PaymentID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@PaymentID", System.Data.SqlDbType.Int).Value = PaymentID;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdatePaymentForRunPay(int PaymentID,string ProviderOrderID,string RunPayDescription)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE PaymentTable SET RunPayDescription=@RunPayDescription,ProviderOrderID=@ProviderOrderID WHERE PaymentID=@PaymentID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@PaymentID", System.Data.SqlDbType.Int).Value = PaymentID;
        DBCmd.Parameters.Add("@RunPayDescription", System.Data.SqlDbType.VarChar).Value = RunPayDescription;
        DBCmd.Parameters.Add("@ProviderOrderID", System.Data.SqlDbType.VarChar).Value = ProviderOrderID;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }


    public static int UpdatePaymentProviderOrderID(int PaymentID, string ProviderOrderID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE PaymentTable SET ProviderOrderID=@ProviderOrderID WHERE PaymentID=@PaymentID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@PaymentID", System.Data.SqlDbType.Int).Value = PaymentID;
        DBCmd.Parameters.Add("@ProviderOrderID", System.Data.SqlDbType.VarChar).Value = ProviderOrderID;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdatePaymentComplete(string PaymentSerial)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE PaymentTable SET ProcessStatus=4 WHERE ProcessStatus=2 AND PaymentSerial=@PaymentSerial";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@PaymentSerial", System.Data.SqlDbType.VarChar).Value = PaymentSerial;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int SetPaymentProcessStatus(string PaymentSerial, int ProcessStatus, string PaymentResult, string BankSequenceID, decimal ProviderOrderAmount, string ProviderOrderID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "spSetPaymentProcessStatus_ver4";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.StoredProcedure;
        DBCmd.Parameters.Add("@PaymentSerial", System.Data.SqlDbType.VarChar).Value = PaymentSerial;
        DBCmd.Parameters.Add("@ProcessStatus", System.Data.SqlDbType.Int).Value = ProcessStatus;
        DBCmd.Parameters.Add("@PaymentResult", System.Data.SqlDbType.NVarChar).Value = PaymentResult;
        DBCmd.Parameters.Add("@BankSequenceID", System.Data.SqlDbType.VarChar).Value = BankSequenceID;
        DBCmd.Parameters.Add("@ProviderOrderAmount", System.Data.SqlDbType.Decimal).Value = ProviderOrderAmount;
        DBCmd.Parameters.Add("@ProviderOrderID", System.Data.SqlDbType.VarChar).Value = ProviderOrderID;
        DBCmd.Parameters.Add("@Return", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.ReturnValue;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        RetValue = (int)(DBCmd.Parameters["@Return"].Value);

        return RetValue;
    }

    public static int SetPaymentProcessStatuOffsetPayment(string PaymentSerial, int ProcessStatus, string PaymentResult, string BankSequenceID, decimal ProviderOrderAmount, string ProviderOrderID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "spSetPaymentProcessStatuOffsetPayment";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.StoredProcedure;
        DBCmd.Parameters.Add("@PaymentSerial", System.Data.SqlDbType.VarChar).Value = PaymentSerial;
        DBCmd.Parameters.Add("@ProcessStatus", System.Data.SqlDbType.Int).Value = ProcessStatus;
        DBCmd.Parameters.Add("@PaymentResult", System.Data.SqlDbType.NVarChar).Value = PaymentResult;
        DBCmd.Parameters.Add("@BankSequenceID", System.Data.SqlDbType.VarChar).Value = BankSequenceID;
        DBCmd.Parameters.Add("@ProviderOrderAmount", System.Data.SqlDbType.Decimal).Value = ProviderOrderAmount;
        DBCmd.Parameters.Add("@ProviderOrderID", System.Data.SqlDbType.VarChar).Value = ProviderOrderID;
        DBCmd.Parameters.Add("@Return", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.ReturnValue;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        RetValue = (int)(DBCmd.Parameters["@Return"].Value);

        return RetValue;
    }


    #endregion

    #region Withdrawal
    public static System.Data.DataTable GetWithdrawalByWithdrawID(int WithdrawID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = "SELECT * FROM Withdrawal WITH (NOLOCK) WHERE WithdrawID=@WithdrawID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@WithdrawID", System.Data.SqlDbType.Int).Value = WithdrawID;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetWithdrawalByDownOrderID(string DownOrderID, int CompanyID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;
        //SS = "SELECT * FROM Withdrawal WITH (NOLOCK) WHERE DownOrderID=@DownOrderID And forCompanyID=@CompanyID";
        SS = "SELECT * FROM Withdrawal WITH (NOLOCK) WHERE DownOrderID=@DownOrderID And forCompanyID=@CompanyID";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@DownOrderID", System.Data.SqlDbType.VarChar).Value = DownOrderID;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetWithdrawalByDownOrderIDAndStatus0(string DownOrderID, int CompanyID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;
        //SS = "SELECT * FROM Withdrawal WITH (NOLOCK) WHERE DownOrderID=@DownOrderID And forCompanyID=@CompanyID";
        SS = "SELECT * FROM Withdrawal WITH (NOLOCK) WHERE DownOrderID=@DownOrderID And forCompanyID=@CompanyID And Status=0";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@DownOrderID", System.Data.SqlDbType.VarChar).Value = DownOrderID;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetWithdrawalByWithdrawID(string WithdrawSerial)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = "SELECT * FROM Withdrawal WITH (NOLOCK) WHERE WithdrawSerial=@WithdrawSerial";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@WithdrawSerial", System.Data.SqlDbType.VarChar).Value = WithdrawSerial;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetWithdrawalByWithdrawID(int CompanyID, string DownOrderID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = "SELECT * FROM Withdrawal WITH (NOLOCK) WHERE forCompanyID=@forCompanyID AND DownOrderID=@DownOrderID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@forCompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DBCmd.Parameters.Add("@DownOrderID", System.Data.SqlDbType.VarChar).Value = DownOrderID;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetWithdrawalLimit(string CurrencyType, int WithdrawLimitType, string ProviderCode = null, int forCompanyID = 0)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        if (WithdrawLimitType == 0)
        {
            SS = "SELECT * FROM WithdrawLimit WITH (NOLOCK) WHERE WithdrawLimitType=@WithdrawLimitType AND CurrencyType=@CurrencyType AND ProviderCode=@ProviderCode";
        }
        else if (WithdrawLimitType == 1)
        {
            SS = "SELECT * FROM WithdrawLimit WITH (NOLOCK) WHERE WithdrawLimitType=@WithdrawLimitType AND CurrencyType=@CurrencyType AND forCompanyID=@forCompanyID";
        }
        else
        {
            return null;
        }

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@WithdrawLimitType", System.Data.SqlDbType.Int).Value = WithdrawLimitType;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        if (WithdrawLimitType == 0)
        {
            DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
        }
        else if (WithdrawLimitType == 1)
        {
            DBCmd.Parameters.Add("@forCompanyID", System.Data.SqlDbType.Int).Value = forCompanyID;
        }

        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    //DownData => API(GateWay)
    public static int InsertWithdrawalByDownData(GatewayCommon.Withdrawal withdrawal) {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int WithdrawID = 0;
        //SS = " IF NOT EXISTS " +
        //     " (SELECT  1 " +
        //     " FROM Withdrawal " +
        //     " WHERE DownOrderID = @DownOrderID " +
        //     " AND forCompanyID = @forCompanyID " +
        //     " ) " +
        //     " BEGIN " +
        //     " INSERT INTO Withdrawal (WithdrawType, forCompanyID, ProviderCode, CurrencyType, Amount," +
        //     "                          CollectCharge, CostCharge, Status, BankCard, BankCardName," +
        //     "                          BankName, BankBranchName, OwnProvince, OwnCity, " +
        //     "                          DownStatus, DownUrl, DownOrderID, DownOrderDate, DownClientIP,FloatType,ServiceType )" +
        //     "                  VALUES (@WithdrawType, @forCompanyID, @ProviderCode, @CurrencyType, @Amount, " +
        //     "                          @CollectCharge, @CostCharge, @Status, @BankCard, @BankCardName, " +
        //     "                          @BankName, @BankBranchName, @OwnProvince, @OwnCity, " +
        //     "                          @DownStatus, @DownUrl, @DownOrderID, @DownOrderDate, @DownClientIP,@FloatType,@ServiceType )" +
        //     "  END;" +
        //     " IF @@ROWCOUNT = 0 " +
        //     " SELECT @@ROWCOUNT " +
        //     " ELSE " +
        //     " SELECT @@IDENTITY ";
        //DBCmd = new System.Data.SqlClient.SqlCommand();
        //DBCmd.CommandText = SS;
        //DBCmd.CommandType = System.Data.CommandType.Text;
        SS = "spAddWithdrawalByDownData";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.StoredProcedure;

        DBCmd.Parameters.Add("@WithdrawType", System.Data.SqlDbType.Int).Value = withdrawal.WithdrawType;
        DBCmd.Parameters.Add("@forCompanyID", System.Data.SqlDbType.Int).Value = withdrawal.forCompanyID;
        DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = withdrawal.ProviderCode;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = withdrawal.CurrencyType;
        DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = withdrawal.ServiceType;
        DBCmd.Parameters.Add("@Amount", System.Data.SqlDbType.Decimal).Value = withdrawal.Amount;
        DBCmd.Parameters.Add("@CollectCharge", System.Data.SqlDbType.Decimal).Value = withdrawal.CollectCharge;
        DBCmd.Parameters.Add("@CostCharge", System.Data.SqlDbType.Decimal).Value = withdrawal.CostCharge; ;
        DBCmd.Parameters.Add("@Status", System.Data.SqlDbType.Int).Value = withdrawal.Status;
        DBCmd.Parameters.Add("@BankCard", System.Data.SqlDbType.VarChar).Value = withdrawal.BankCard;
        DBCmd.Parameters.Add("@BankCardName", System.Data.SqlDbType.NVarChar).Value = withdrawal.BankCardName;
        DBCmd.Parameters.Add("@BankName", System.Data.SqlDbType.NVarChar).Value = withdrawal.BankName;
        DBCmd.Parameters.Add("@BankBranchName", System.Data.SqlDbType.NVarChar).Value = withdrawal.BankBranchName;
        DBCmd.Parameters.Add("@OwnProvince", System.Data.SqlDbType.NVarChar).Value = withdrawal.OwnProvince;
        DBCmd.Parameters.Add("@OwnCity", System.Data.SqlDbType.NVarChar).Value = withdrawal.OwnCity;
        DBCmd.Parameters.Add("@DownStatus", System.Data.SqlDbType.Int).Value = withdrawal.DownStatus;
        DBCmd.Parameters.Add("@DownUrl", System.Data.SqlDbType.VarChar).Value = withdrawal.DownUrl;
        DBCmd.Parameters.Add("@DownOrderID", System.Data.SqlDbType.VarChar).Value = withdrawal.DownOrderID;
        DBCmd.Parameters.Add("@DownOrderDate", System.Data.SqlDbType.DateTime).Value = withdrawal.DownOrderDate;
        DBCmd.Parameters.Add("@DownClientIP", System.Data.SqlDbType.VarChar).Value = withdrawal.DownClientIP;
        DBCmd.Parameters.Add("@State", System.Data.SqlDbType.NVarChar).Value = withdrawal.State;
        DBCmd.Parameters.Add("@FloatType", System.Data.SqlDbType.Int).Value = withdrawal.FloatType;
        DBCmd.Parameters.Add(new System.Data.SqlClient.SqlParameter("@WithdrawID", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output });
        DBCmd.Parameters.Add("@RETURN", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.ReturnValue;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        if ((int)DBCmd.Parameters["@RETURN"].Value == 0) {
            WithdrawID = (int)DBCmd.Parameters["@WithdrawID"].Value;
            //WithdrawID = Convert.ToInt32(DBAccess.GetDBValue(Pay.DBConnStr, DBCmd));
            return WithdrawID;
        }
        else
            return (int)DBCmd.Parameters["@RETURN"].Value;
    }

    public static void UpdateWithdrawalByManualWithdrawalReview(GatewayCommon.Withdrawal withdrawal)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;


        SS = "UPDATE Withdrawal SET  WithdrawType=@WithdrawType, forCompanyID=@forCompanyID, ProviderCode=@ProviderCode, " +
             "                          CollectCharge=@CollectCharge, CostCharge=@CostCharge, Status=@Status," +
             "                          DownStatus=@DownStatus,DownUrl=@DownUrl,DownOrderID=@DownOrderID,FloatType=@FloatType " +
             " WHERE WithdrawSerial=@WithdrawSerial";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@WithdrawType", System.Data.SqlDbType.Int).Value = withdrawal.WithdrawType;
        DBCmd.Parameters.Add("@forCompanyID", System.Data.SqlDbType.Int).Value = withdrawal.forCompanyID;
        DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = withdrawal.ProviderCode;
        DBCmd.Parameters.Add("@WithdrawSerial", System.Data.SqlDbType.VarChar).Value = withdrawal.WithdrawSerial;
        DBCmd.Parameters.Add("@CollectCharge", System.Data.SqlDbType.Decimal).Value = withdrawal.CollectCharge;
        DBCmd.Parameters.Add("@CostCharge", System.Data.SqlDbType.Decimal).Value = withdrawal.CostCharge;
        DBCmd.Parameters.Add("@Status", System.Data.SqlDbType.Int).Value = withdrawal.Status;
        DBCmd.Parameters.Add("@DownStatus", System.Data.SqlDbType.Int).Value = withdrawal.DownStatus;
        DBCmd.Parameters.Add("@DownUrl", System.Data.SqlDbType.VarChar).Value = withdrawal.DownUrl;
        DBCmd.Parameters.Add("@DownOrderID", System.Data.SqlDbType.VarChar).Value = withdrawal.DownOrderID;

        DBCmd.Parameters.Add("@FloatType", System.Data.SqlDbType.Int).Value = withdrawal.FloatType;
        DBAccess.GetDBValue(Pay.DBConnStr, DBCmd);
    }

    public static int UpdateWithdrawUpStatus(int UpStatus, string WithdrawSerial)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE Withdrawal SET UpStatus=@UpStatus WHERE WithdrawSerial=@WithdrawSerial";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@UpStatus", System.Data.SqlDbType.Int).Value = UpStatus;
        DBCmd.Parameters.Add("@WithdrawSerial", System.Data.SqlDbType.VarChar).Value = WithdrawSerial;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdateWithdrawProvider(int WithdrawID, string ProviderCode, string ServiceType)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE Withdrawal SET ServiceType=@ServiceType,ProviderCode=@ProviderCode WHERE WithdrawID=@WithdrawID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@WithdrawID", System.Data.SqlDbType.Int).Value = WithdrawID;
        DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
        DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdateWithdrawSerialByUpData(int UpStatus, string UpResult, string UpOrderID, decimal UpDidAmount, string WithdrawSerial)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE Withdrawal SET UpStatus=@UpStatus, UpResult=@UpResult, UpOrderID=@UpOrderID, UpDidAmount=@UpDidAmount WHERE WithdrawSerial=@WithdrawSerial";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@UpStatus", System.Data.SqlDbType.Int).Value = UpStatus;
        DBCmd.Parameters.Add("@UpResult", System.Data.SqlDbType.VarChar).Value = UpResult;
        DBCmd.Parameters.Add("@UpOrderID", System.Data.SqlDbType.VarChar).Value = UpOrderID;
        DBCmd.Parameters.Add("@UpDidAmount", System.Data.SqlDbType.Decimal).Value = UpDidAmount;
        DBCmd.Parameters.Add("@WithdrawSerial", System.Data.SqlDbType.VarChar).Value = WithdrawSerial;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdateWithdrawSerialByUpData(int UpStatus, string UpResult, string UpOrderID, decimal UpDidAmount, int Status, string WithdrawSerial)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE Withdrawal SET UpStatus=@UpStatus, UpResult=@UpResult, UpOrderID=@UpOrderID, UpDidAmount=@UpDidAmount, Status=@Status WHERE WithdrawSerial=@WithdrawSerial";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@UpStatus", System.Data.SqlDbType.Int).Value = UpStatus;
        DBCmd.Parameters.Add("@UpResult", System.Data.SqlDbType.VarChar).Value = UpResult;
        DBCmd.Parameters.Add("@UpOrderID", System.Data.SqlDbType.VarChar).Value = UpOrderID;
        DBCmd.Parameters.Add("@UpDidAmount", System.Data.SqlDbType.Decimal).Value = UpDidAmount;
        DBCmd.Parameters.Add("@Status", System.Data.SqlDbType.Int).Value = Status;
        DBCmd.Parameters.Add("@WithdrawSerial", System.Data.SqlDbType.VarChar).Value = WithdrawSerial;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdateWithdrawSerialByUpData(int UpStatus, string UpResult, string UpOrderID, decimal UpDidAmount, string WithdrawSerial, int WithdrawID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE Withdrawal SET UpStatus=@UpStatus, UpResult=@UpResult, UpOrderID=@UpOrderID, UpDidAmount=@UpDidAmount, WithdrawSerial=@WithdrawSerial WHERE WithdrawID=@WithdrawID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@UpStatus", System.Data.SqlDbType.Int).Value = UpStatus;
        DBCmd.Parameters.Add("@UpResult", System.Data.SqlDbType.VarChar).Value = UpResult;
        DBCmd.Parameters.Add("@UpOrderID", System.Data.SqlDbType.VarChar).Value = UpOrderID;
        DBCmd.Parameters.Add("@UpDidAmount", System.Data.SqlDbType.Decimal).Value = UpDidAmount;
        DBCmd.Parameters.Add("@WithdrawSerial", System.Data.SqlDbType.VarChar).Value = WithdrawSerial;
        DBCmd.Parameters.Add("@WithdrawID", System.Data.SqlDbType.Int).Value = WithdrawID;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdateWithdrawSerialByUpData( string UpOrderID, int WithdrawID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE Withdrawal SET UpOrderID=@UpOrderID WHERE WithdrawID=@WithdrawID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@UpOrderID", System.Data.SqlDbType.VarChar).Value = UpOrderID;
        DBCmd.Parameters.Add("@WithdrawID", System.Data.SqlDbType.Int).Value = WithdrawID;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }



    public static int UpdateWithdrawSerialByStatus(int DownStatus, int WithdrawID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE Withdrawal SET DownStatus=@DownStatus WHERE WithdrawID=@WithdrawID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@DownStatus", System.Data.SqlDbType.Int).Value = DownStatus;
        DBCmd.Parameters.Add("@WithdrawID", System.Data.SqlDbType.Int).Value = WithdrawID;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdateWithdrawStatus(int Status, int WithdrawID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE Withdrawal SET Status=@Status WHERE WithdrawID=@WithdrawID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@Status", System.Data.SqlDbType.Int).Value = Status;
        DBCmd.Parameters.Add("@WithdrawID", System.Data.SqlDbType.Int).Value = WithdrawID;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }


    public static int UpdateWithdrawStatus(int Status, string WithdrawSerial)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE Withdrawal SET Status=@Status WHERE WithdrawSerial=@WithdrawSerial";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@Status", System.Data.SqlDbType.Int).Value = Status;
        DBCmd.Parameters.Add("@WithdrawSerial", System.Data.SqlDbType.VarChar).Value = WithdrawSerial;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdateWithdrawStatusAndFloatTypeForProxyProvider(int Status, int WithdrawID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE Withdrawal SET Status=@Status,FloatType=1 WHERE WithdrawID=@WithdrawID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@Status", System.Data.SqlDbType.Int).Value = Status;
        DBCmd.Parameters.Add("@WithdrawID", System.Data.SqlDbType.Int).Value = WithdrawID;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int UpdateWithdrawStatusAndFloatType(int Status, string WithdrawSerial)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "UPDATE Withdrawal SET Status=@Status,FloatType=1,ServiceType='' WHERE WithdrawSerial=@WithdrawSerial";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@Status", System.Data.SqlDbType.Int).Value = Status;
        DBCmd.Parameters.Add("@WithdrawSerial", System.Data.SqlDbType.VarChar).Value = WithdrawSerial;
        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static int ReviewWithdrawal(string WithdrawSerial)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "spReviewWithdrawal";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.StoredProcedure;
        DBCmd.Parameters.Add("@WithdrawSerial", System.Data.SqlDbType.VarChar).Value = WithdrawSerial;
        DBCmd.Parameters.Add("@Return", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.ReturnValue;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        RetValue = (int)(DBCmd.Parameters["@Return"].Value);

        return RetValue;
    }

    public static int ReviewWithdrawalProccessingtoDefault(int WithdrawID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "spReviewWithdrawalProccessingtoDefault";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.StoredProcedure;
        DBCmd.Parameters.Add("@WithdrawID", System.Data.SqlDbType.Int).Value = WithdrawID;
        DBCmd.Parameters.Add("@Return", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.ReturnValue;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        RetValue = (int)(DBCmd.Parameters["@Return"].Value);

        return RetValue;
    }

    public static int ReviewWithdrawaltoProccessing(int WithdrawID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "spReviewWithdrawaltoProccessing";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.StoredProcedure;
        DBCmd.Parameters.Add("@WithdrawID", System.Data.SqlDbType.Int).Value = WithdrawID;
        DBCmd.Parameters.Add("@Return", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.ReturnValue;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        RetValue = (int)(DBCmd.Parameters["@Return"].Value);

        return RetValue;
    }

    public static int InsertProxyProviderOrder(string OrderSerial, int GroupID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;


        SS = "INSERT INTO ProxyProviderOrder (forOrderSerial,GroupID,Type) " +
            "                          VALUES (@forOrderSerial,@GroupID,1)";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@forOrderSerial", System.Data.SqlDbType.VarChar).Value = OrderSerial;
        DBCmd.Parameters.Add("@GroupID", System.Data.SqlDbType.Int).Value = GroupID;

        RetValue = DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        return RetValue;
    }

    public static System.Data.DataTable GetProxyProviderOrderByOrderSerial(string OrderSerial)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = "SELECT * FROM ProxyProviderOrder WHERE forOrderSerial =@OrderSerial";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@OrderSerial", System.Data.SqlDbType.VarChar).Value = OrderSerial;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetProxyProviderGroupByState(string ProviderCode, int State)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = " SELECT * " +
            " FROM ProxyProviderGroup WITH (NOLOCK) " +
             " WHERE ProxyProviderGroup.forProviderCode = @forProviderCode And State=@State";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@forProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
        DBCmd.Parameters.Add("@State", System.Data.SqlDbType.Int).Value = State;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetProxyProviderGroupByStateWithWithdrawingCount(string ProviderCode, int State)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;


        SS = " SELECT *,CanWithdrawingCount, " +
             " (SELECT ISNULL(COUNT(*), 0) FROM Withdrawal W " +
             " JOIN ProxyProviderOrder PPO " +
             " ON PPO.forOrderSerial = W.WithdrawSerial" +
             " AND PPO.Type = 1 AND W.Status = 1 AND PPO.ManualChangeGroup=1 " +
             " WHERE ProxyProviderGroup.GroupID = PPO.GroupID ) as WithdrawingCount" +
             " FROM ProxyProviderGroup WITH (NOLOCK) " +
             " WHERE ProxyProviderGroup.forProviderCode = @forProviderCode And State=@State";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@forProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
        DBCmd.Parameters.Add("@State", System.Data.SqlDbType.Int).Value = State;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static int spUpdateProxyProviderOrderGroupByAdmin(string OrderSerial, int GroupID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "spChangeWithdrawalGroupByAuto";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.StoredProcedure;

        DBCmd.Parameters.Add("@WithdrawSerial", System.Data.SqlDbType.VarChar).Value = OrderSerial;
        DBCmd.Parameters.Add("@ChangeGroupID", System.Data.SqlDbType.Int).Value = GroupID;
        DBCmd.Parameters.Add("@Return", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.ReturnValue;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        RetValue = (int)(DBCmd.Parameters["@Return"].Value);

        return RetValue;
    }

    public static System.Data.DataTable GetAutoDistributionGroupWithdraw()
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = " SELECT WithdrawSerial FROM Withdrawal "+
             " JOIN ProxyProviderOrder ON WithdrawSerial = forOrderSerial And ProxyProviderOrder.Type = 1 "+
             " WHERE Status = 1 And HandleByAdminID = 0 "+
             " And GroupID = 1 And ManualChangeGroup = 1 ";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }
    
    #endregion

    #region Provider
    public static System.Data.DataTable GetProviderSummary(DateTime SummaryDate, string ProviderCode, string CurrencyType, string ServiceType)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;


        SS = "SELECT * FROM SummaryProviderByDate WITH (NOLOCK) WHERE SummaryDate=@SummaryDate AND ProviderCode=@ProviderCode AND CurrencyType=@CurrencyType AND ServiceType=@ServiceType";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@SummaryDate", System.Data.SqlDbType.DateTime).Value = SummaryDate;
        DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetProviderServiceByServiceType(string ServiceType, string CurrencyType, bool IsAll)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;
        if (IsAll)
        {
            SS = "SELECT * FROM ProviderService WITH (NOLOCK) WHERE ServiceType=@ServiceType AND CurrencyType=@CurrencyType";
        }
        else
        {
            SS = "SELECT * FROM ProviderService WITH (NOLOCK) WHERE ServiceType=@ServiceType AND CurrencyType=@CurrencyType AND State=0";
        }
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetProviderServiceByProviderServiceType(string ProviderCode, string ServiceType, string CurrencyType, bool IsAll)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;
        if (IsAll)
        {
            SS = "SELECT * FROM ProviderService WITH (NOLOCK) WHERE ServiceType=@ServiceType AND CurrencyType=@CurrencyType AND ProviderCode=@ProviderCode";
        }
        else
        {
            SS = "SELECT * FROM ProviderService WITH (NOLOCK) WHERE ServiceType=@ServiceType AND CurrencyType=@CurrencyType AND ProviderCode=@ProviderCode AND State=0";
        }
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetProviderPointList()
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;

        SS = "SELECT * FROM ProviderCode WITH (NOLOCK) Where (ProviderCode.ProviderAPIType & 4) = 4 And ProviderCode.ProviderState=0 ";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }


    public static System.Data.DataTable GetProviderByProviderCode(string ProviderCode)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;
        SS = "SELECT * FROM ProviderCode WITH (NOLOCK) WHERE ProviderCode=@ProviderCode";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    public static System.Data.DataTable GetGPayRelation(string CompanyID)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;
        SS = "SELECT * FROM GPayRelation WITH (NOLOCK) WHERE forCompanyID=@CompanyID";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.VarChar).Value = CompanyID;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    #endregion

    #region SummaryByDate

    public static int SetUpdateSummaryCount(int CompanyID, string ProviderCode, string CurrencyType, string ServiceType)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "spUpdateSummaryCount";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.StoredProcedure;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
        DBCmd.Parameters.Add("@Return", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.ReturnValue;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        RetValue = (int)(DBCmd.Parameters["@Return"].Value);

        return RetValue;
    }

    #endregion

    #region GPayRelation

    public static System.Data.DataTable GetTopParentGPayRelation(int CompanyID, string ServiceType, string CurrencyType, string SortKey)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;


        SS = "SELECT @CompanyID AS forCompanyID, G.ProviderCode, G.ServiceType, G.CurrencyType, G.Weight "
           + " FROM CompanyTable C "
           + " LEFT JOIN GPayRelation G ON C.CompanyID = G.forCompanyID "
           + " WHERE @SortKey LIKE C.SortKey + '%' "
           + " AND C.InsideLevel = 0 "
           + " AND G.ServiceType = @ServiceType "
           + " AND G.CurrencyType = @CurrencyType ";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DBCmd.Parameters.Add("@SortKey", System.Data.SqlDbType.VarChar).Value = SortKey;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    #endregion

    #region GPayWithdrawRelation

    public static System.Data.DataTable GetTopParentGPayWithdrawRelation(int CompanyID, string CurrencyType, string SortKey)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;


        SS = "SELECT @CompanyID AS forCompanyID, G.ProviderCode, G.ServiceType, G.CurrencyType, G.Weight,G.WithdrawType "
           + " FROM CompanyTable C "
           + " LEFT JOIN GPayWithdrawRelation G ON C.CompanyID = G.forCompanyID "
           + " WHERE @SortKey LIKE C.SortKey + '%' "
           + " AND C.InsideLevel = 0 "
           + " AND G.ServiceType = @ServiceType "
           + " AND G.CurrencyType = @CurrencyType ";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        DBCmd.Parameters.Add("@SortKey", System.Data.SqlDbType.VarChar).Value = SortKey;
        DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    #endregion

    #region PaymentTransferLog
    public static void InsertPaymentTransferLog(string Message, int Type, string PaymentSerial, string ProviderCode)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;

        SS = "INSERT INTO PaymentTransferLog (forPaymentSerial, Message, Type,forProviderCode)" +
             "                  VALUES (@PaymentSerial,@Message, @Type,@ProviderCode);";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@Type", System.Data.SqlDbType.Int).Value = Type;
        DBCmd.Parameters.Add("@PaymentSerial", System.Data.SqlDbType.VarChar).Value = PaymentSerial;
        DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
        DBCmd.Parameters.Add("@Message", System.Data.SqlDbType.NVarChar).Value = Message;

        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

    }
    #endregion

    #region DownOrderTransferLog
    public static void InsertDownOrderTransferLog(string Message, int Type, string OrderID, string DownOrderID, string CompanyCode, bool isErrorOrder)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;

        SS = "INSERT INTO DownOrderTransferLog (OrderID,DownOrderID, Message, Type,CompanyCode,isErrorOrder)" +
             "                  VALUES (@OrderID,@DownOrderID,@Message, @Type,@CompanyCode,@isErrorOrder);";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@Type", System.Data.SqlDbType.Int).Value = Type;
        DBCmd.Parameters.Add("@isErrorOrder", System.Data.SqlDbType.Int).Value = isErrorOrder ? 0 : 1;
        DBCmd.Parameters.Add("@OrderID", System.Data.SqlDbType.VarChar).Value = OrderID;
        DBCmd.Parameters.Add("@DownOrderID", System.Data.SqlDbType.VarChar).Value = DownOrderID;
        DBCmd.Parameters.Add("@CompanyCode", System.Data.SqlDbType.VarChar).Value = CompanyCode;
        DBCmd.Parameters.Add("@Message", System.Data.SqlDbType.NVarChar).Value = Message;

        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

    }
    #endregion

    #region BlackList
    public static int GetBlackListCountResult(string UserIP, string BankCard, string BankCardName, string CheckType)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT = null;

        if (CheckType == "Payment")
        {
            SS = " SELECT count(*) " +
                 " FROM   BlackList  " +
                 " WHERE ( UserIP = @UserIP ) AND Status = 0";
        }
        else
        {
            SS = " SELECT count(*) " +
            " FROM   BlackList  " +
            " WHERE ( BankCard = @BankCard OR BankCardName = @BankCardName ) AND Status = 0";
        }
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@UserIP", System.Data.SqlDbType.VarChar).Value = UserIP;
        DBCmd.Parameters.Add("@BankCard", System.Data.SqlDbType.VarChar).Value = BankCard;
        DBCmd.Parameters.Add("@BankCardName", System.Data.SqlDbType.NVarChar).Value = BankCardName;

        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);
        return (int)DT.Rows[0][0];
    }

    public static int CheckPaymentUserName(string BankCardName)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT = null;

        
            SS = " SELECT count(*) " +
                 " FROM   BlackList  " +
                 " WHERE (BankCardName = @BankCardName ) AND Status = 0";
      
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@BankCardName", System.Data.SqlDbType.NVarChar).Value = BankCardName;

        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);
        return (int)DT.Rows[0][0];
    }
    #endregion

    #region WithdrawalIP

    public static int GetWithdrawalIP(string WithdrawalIP, string CompanyCode)
    {

        int returnValue = -1;

        //if (CompanyCode == "vjo168" || CompanyCode == "Ewin")
        //{
        //    var splitWithdrawalIP = WithdrawalIP.Split('.');
        //    if (splitWithdrawalIP[0] == "218" && splitWithdrawalIP[1] == "213" && splitWithdrawalIP[2] == "221" && (int.Parse(splitWithdrawalIP[3]) >= 0) && (int.Parse(splitWithdrawalIP[3]) <=255 )) {
        //        returnValue = 1;
        //    }
        //}
      
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;
        SS = "SELECT * FROM WithdrawalIP WITH (NOLOCK) WHERE CompanyCode=@CompanyCode And WithdrawalIP=@WithdrawalIP ";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyCode", System.Data.SqlDbType.VarChar).Value = CompanyCode;
        DBCmd.Parameters.Add("@WithdrawalIP", System.Data.SqlDbType.VarChar).Value = WithdrawalIP;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        if (DT != null && DT.Rows.Count > 0)
        {
            returnValue = DT.Rows.Count;
        }

        return returnValue;
    }
    #endregion

    #region ProxyProvider

    public static System.Data.DataTable GetProxyProviderResult(string ProviderCode)
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable DT;
        SS = "SELECT * FROM ProxyProvider WITH(NOLOCK) WHERE forProviderCode =@ProviderCode";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        return DT;
    }

    #endregion

    #region RiskControlWithdrawalTable
    public static void InsertRiskControlWithdrawal(string CompanyCode, string BankCard, string BankCardName, string BankName) {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;

        SS = "  INSERT INTO RiskControlWithdrawal (forCompanyCode,BankCard,BankCardName,BankName) VALUES(@CompanyCode,@BankCard,@BankCardName,@BankName)";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@CompanyCode", System.Data.SqlDbType.VarChar).Value = CompanyCode;
        DBCmd.Parameters.Add("@BankCard", System.Data.SqlDbType.VarChar).Value = BankCard;
        DBCmd.Parameters.Add("@BankCardName", System.Data.SqlDbType.NVarChar).Value = BankCardName;
        DBCmd.Parameters.Add("@BankName", System.Data.SqlDbType.NVarChar).Value = BankName;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);
    }
    #endregion

    #region 系統警示通知
    public static void InsertBotSendLog(string CompanyCode, string MsgContent) {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;

        SS = " INSERT INTO BotSendLog(forCompanyCode,MsgContent)" +
      " VALUES (@forCompanyCode,@MsgContent) ";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@forCompanyCode", System.Data.SqlDbType.VarChar).Value = CompanyCode;
        DBCmd.Parameters.Add("@MsgContent", System.Data.SqlDbType.NVarChar).Value = MsgContent;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

    }

    #endregion

    #region 反代IP名單
    public static List<string> GetProxyIPList() {

        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;
        List<string> returnValue = new List<string>();
        System.Data.DataTable DT;
        //System.Collections.Generic.Dictionary<int, string> SummaryDict = new Dictionary<int, string>();
        DBCmd = new System.Data.SqlClient.SqlCommand();
        SS = " SELECT IP FROM ProxyIP ";

        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;

        DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);

        if (DT != null) {
            if (DT.Rows.Count > 0) {
                foreach (System.Data.DataRow dr in DT.Rows) {
                    returnValue.Add(dr["IP"].ToString());
                }
                //returnValue = DataTableExtensions.ToList<string>(DT).ToList();
            }
        }
        return returnValue;
    }
    #endregion

    public static int CheckWitdrawal(string BankCard, string BankCardName, string BankName)
    {
        String SS = String.Empty;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int returnValue = -1;

        SS = " SELECT COUNT(*) as Count " +
             " FROM Withdrawal W WITH(NOLOCK) " +
             " Where CreateDate" +
             " between DATEADD(minute, -5, GETDATE())" +
             " And GETDATE()" +
             " AND W.BankCard = @BankCard" +
             " AND W.BankCardName = @BankCardName" +
             " AND W.BankName = @BankName  ";

        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;

        DBCmd.Parameters.Add("@BankCard", System.Data.SqlDbType.VarChar).Value = BankCard;
        DBCmd.Parameters.Add("@BankCardName", System.Data.SqlDbType.NVarChar).Value = BankCardName;
        DBCmd.Parameters.Add("@BankName", System.Data.SqlDbType.NVarChar).Value = BankName;
        var dbreturn = DBAccess.GetDBValue(Pay.DBConnStr, DBCmd);
        if (dbreturn != null)
        {
            returnValue = int.Parse(dbreturn.ToString());
        }

        return returnValue;
    }

    public static System.Data.DataTable GetUnSendMsgToBot(int LastMsgID) {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;
        System.Data.DataTable Dt = null;

        SS = " SELECT * FROM BotSendLog "
             + " WHERE Status = 0 AND MsgID > @LastMsgID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@LastMsgID", System.Data.SqlDbType.Int).Value = LastMsgID;
        Dt = DBAccess.GetDB(Pay.DBConnStr, DBCmd);
        //returnValue = Dream.ToList<DBModel.Product>(DT) as List<DBModel.Product>;
        return Dt;
    }

    public static void SetMsgToBotSended(int LastMsgID) {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd;

        SS = " Update BotSendLog "
             + " Set Status = 1 WHERE MsgID <= @LastMsgID";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.Text;
        DBCmd.Parameters.Add("@LastMsgID", System.Data.SqlDbType.Int).Value = LastMsgID;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);
        //returnValue = Dream.ToList<DBModel.Product>(DT) as List<DBModel.Product>;

    }

    public static int SetSummaryCompanyByHour()
    {
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        int RetValue;

        SS = "spSetSummaryCompanyByHour";
        DBCmd = new System.Data.SqlClient.SqlCommand();
        DBCmd.CommandText = SS;
        DBCmd.CommandType = System.Data.CommandType.StoredProcedure;
        //DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = CompanyID;
        //DBCmd.Parameters.Add("@ProviderCode", System.Data.SqlDbType.VarChar).Value = ProviderCode;
        //DBCmd.Parameters.Add("@CurrencyType", System.Data.SqlDbType.VarChar).Value = CurrencyType;
        //DBCmd.Parameters.Add("@ServiceType", System.Data.SqlDbType.VarChar).Value = ServiceType;
        DBCmd.Parameters.Add("@Return", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.ReturnValue;
        DBAccess.ExecuteDB(Pay.DBConnStr, DBCmd);

        RetValue = (int)(DBCmd.Parameters["@Return"].Value);

        return RetValue;
    }

    //public static void AddPayment(int PaymentID)
    //{
    //    string SS;
    //    System.Data.SqlClient.SqlCommand DBCmd = null;
    //    System.Data.DataTable DT;

    //    SS = "SELECT * FROM PaymentTable WITH (NOLOCK) WHERE PaymentID=@PaymentID";
    //    DBCmd = new System.Data.SqlClient.SqlCommand();
    //    DBCmd.CommandText = SS;
    //    DBCmd.CommandType = System.Data.CommandType.Text;
    //    DBCmd.Parameters.Add("@PaymentID", System.Data.SqlDbType.Int).Value = PaymentID;
    //    DT = DBAccess.GetDB(EWin.DBConnStr, DBCmd);

    //    return DT;
    //}
}
