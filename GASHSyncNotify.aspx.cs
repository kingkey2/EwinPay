using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Xml;

public partial class GASHSyncNotify : System.Web.UI.Page
{
    public GASHSyncNotify()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("GASH");
        _TransactionKey = new CodingControl.TransactionKey() { Key = SettingData.MerchantKey, IV = SettingData.OtherDatas[0], Password = SettingData.OtherDatas[1] };
    }

    private GatewayCommon.ProviderSetting SettingData;

    private CodingControl.TransactionKey _TransactionKey;

    public bool IsPaymentSuccess;
    protected void Page_Load(object sender, EventArgs e)
    {
        if (Request["data"] != null)
        {

            //Load Data from HttpRequest
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(Encoding.UTF8.GetString(Convert.FromBase64String(Request["data"].ToString())));

            #region Process Transaction

            #region Configure for Content Provider

            //Settings for Cotent Provider (for Alpha Enviroment)

            string PaymentSerial = xmlDoc.DocumentElement["COID"].InnerText;
            string ProviderCode = "GASH";
            IsPaymentSuccess = false;

            PayDB.InsertPaymentTransferLog("交易結束 response:" + Request["data"].ToString(), 1, PaymentSerial, ProviderCode);
            #endregion

            //Check whether the Transaction is correlative
            if (xmlDoc.DocumentElement["MSG_TYPE"].InnerText != "0110" || xmlDoc.DocumentElement["PCODE"].InnerText != "300000")
            {
                PayDB.InsertPaymentTransferLog("交易回應失敗:訂單類型有誤", 1, PaymentSerial, ProviderCode);
                IsPaymentSuccess = false;           
            }
            else
            {
                //Verify the ERPC
                if (!CodingControl.VerifyERPC(ref xmlDoc, xmlDoc.DocumentElement["ERPC"].InnerText, _TransactionKey))
                {
                    PayDB.InsertPaymentTransferLog("交易回應失敗:簽名有誤", 1, PaymentSerial, ProviderCode);
                    IsPaymentSuccess = false;
                }
                else
                {
                    #region Retrieve Order Information
                    string orderID = xmlDoc.DocumentElement["COID"].InnerText;                  //Order Number
                    string orderRRN = xmlDoc.DocumentElement["RRN"].InnerText;                  //GPS Transaction Number
                    string orderPaymentStatus = xmlDoc.DocumentElement["PAY_STATUS"].InnerText; //Pay Status
                    string orderRCode = xmlDoc.DocumentElement["RCODE"].InnerText;              //Response Code
                
                    //Order Success
                    if (orderPaymentStatus == "S")
                    {
      
                        IsPaymentSuccess = GASHASyncNotify(xmlDoc);
                    }
                    //Order Failure
                    if (orderPaymentStatus == "F" || orderPaymentStatus == "T")
                    {
                        //Order Failure
                        IsPaymentSuccess = false;
                    }
                    //Order in Process
                    if (orderPaymentStatus == "0" || orderPaymentStatus == "W")
                    {
                        //Order in Process
                        IsPaymentSuccess = GASHASyncNotify(xmlDoc);
                
                    }
                    #endregion
                }
            }
            #endregion
        }
    }

    private bool GASHASyncNotify(XmlDocument xmlDoc)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("GASH");
        GatewayCommon.Payment paymentModel=new GatewayCommon.Payment();
        bool companyRequestResult = false;
        bool IsPaymentSuccess = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        int processStatus=-1;
        System.Data.DataTable DT = null;
       
        string PAY_STATUS = xmlDoc.DocumentElement["PAY_STATUS"].InnerText;
        string RCODE = xmlDoc.DocumentElement["RCODE"].InnerText;
        string PAID = xmlDoc.DocumentElement["PAID"].InnerText;
        paymentSerial = xmlDoc.DocumentElement["COID"].InnerText;

        DT = PayDB.GetPaymentByPaymentID(paymentSerial);
        if (DT != null && DT.Rows.Count > 0)
        {
            paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
        }

        if (paymentModel.ProcessStatus==2|| paymentModel.ProcessStatus == 4)
        {
            IsPaymentSuccess = true;
            return IsPaymentSuccess;
        }

        try
        {
     
            #region 轉換status代碼
            if (PAY_STATUS == "S")
            {
                GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                if (providerRequestData.IsPaymentSuccess == true)
                {
                    processStatus = 2;
                }
            }
            else if (PAY_STATUS == "0" || PAY_STATUS == "W" || RCODE == "9004" || RCODE == "9998" || RCODE == "2001" || RCODE == "9999")
            {//成功
                #region 反查訂單

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
         
                #endregion
            }
            #endregion

            if (processStatus>0)
            {

                var _ApplyWithdrawalData= new Provider_GASH().ApplyWithdrawal(paymentModel, PAID);

                if (_ApplyWithdrawalData.IsPaymentSuccess)
                {
                    int DBreturn = PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, xmlDoc.OuterXml, "", decimal.Parse(xmlDoc.DocumentElement["AMOUNT"].InnerText), xmlDoc.DocumentElement["RRN"].InnerText);

                    if (DBreturn == 0)
                    {
                        IsPaymentSuccess = true;
                        paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                        gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                        System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                        GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                        if (CompanyModel.IsProxyCallBack == 0)
                        {
                            companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                        }
                        else
                        {
                            companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                        }


                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,修改訂單狀態失敗:" + DBreturn, 3, paymentSerial, providerSetting.ProviderCode);
                    }
                }    
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return IsPaymentSuccess;
    }
}