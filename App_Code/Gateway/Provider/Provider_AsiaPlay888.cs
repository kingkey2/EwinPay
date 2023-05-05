using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Provider_AsiaPay 的摘要描述
/// </summary>
public class Provider_AsiaPlay888 : GatewayCommon.ProviderGateway, GatewayCommon.ProviderGatewayByWithdraw
{

    public Provider_AsiaPlay888()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("AsiaPlay888");
    }

    Dictionary<string, string> GatewayCommon.ProviderGateway.GetSubmitData(GatewayCommon.Payment payment)
    {
        Dictionary<string, string> dataDic = new Dictionary<string, string>();
        return dataDic;
    }

    string GatewayCommon.ProviderGateway.GetCompleteUrl(GatewayCommon.Payment payment)
    {

        string sign;
        string signStr = "";
        Dictionary<string, object> signDic = new Dictionary<string, object>();
        Dictionary<string, object> sendDic = new Dictionary<string, object>();

        signDic.Add("Account", SettingData.MerchantCode);//
        signDic.Add("OrderNo", payment.PaymentSerial);//
        signDic.Add("PayName", "");//
        signDic.Add("PayBankName", "");//
        signDic.Add("PayUserName", payment.UserName.Replace("　", " "));//
        signDic.Add("PayBankAccount", "");//
        signDic.Add("Amount", int.Parse(payment.OrderAmount.ToString("#")));//
        sendDic.Add("Data", signDic);

        signStr = JsonConvert.SerializeObject(signDic)+ SettingData.MerchantKey;
        sign = CodingControl.GetMD5(signStr, false);

        sendDic.Add("Hash", sign);

        PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(sendDic), 1, payment.PaymentSerial, payment.ProviderCode);
        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.ProviderUrl, JsonConvert.SerializeObject(sendDic), payment.PaymentSerial, payment.ProviderCode);

        //This line executes whether or not the exception occurs.
        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "1000"&& revjsonObj["message"].ToString().ToUpper() == "SUCCESSFUL")
                {
                    string URL = "";
                    string Prams = "uid=" + revjsonObj["result"]["uid"].ToString() + "&operatorAmount=" + revjsonObj["result"]["operatorAmount"].ToString() + "&bankAccountName=" + revjsonObj["result"]["bankAccountName"].ToString() + "&branchName=" + revjsonObj["result"]["branchName"].ToString() + "&bankAccount=" + revjsonObj["result"]["bankAccount"].ToString() + "&buyerID=" + revjsonObj["result"]["buyerID"].ToString();

                    if (Pay.IsTestSite)
                    {
                        URL= "http://epay.dev4.mts.idv.tw/RedirectView/PaymentGateway/PaymentGateway.cshtml?"+ Prams;
                    }
                    else {
                        URL= "https://ewin-pay.com/RedirectView/PaymentGateway/PaymentGateway.cshtml?"+ Prams;
                    }
                    PayDB.InsertPaymentTransferLog("申请订单完成", 1, payment.PaymentSerial, payment.ProviderCode);
                    return URL;

                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商回传有误:" + jsonStr, 1, payment.PaymentSerial, payment.ProviderCode);
                    return "";
                }
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 1, payment.PaymentSerial, payment.ProviderCode);
                return "";
            }
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 1, payment.PaymentSerial, payment.ProviderCode);
            return "";
            throw;
        }

    }

    GatewayCommon.ProviderRequestType GatewayCommon.ProviderGateway.GetRequestType()
    {
        return SettingData.RequestType;
    }

    public GatewayCommon.PaymentByProvider QueryPayment(GatewayCommon.Payment payment)
    {
        GatewayCommon.PaymentByProvider Ret = new GatewayCommon.PaymentByProvider();
        string sign;
        string signStr = "";
        string GUID = Guid.NewGuid().ToString("N");
        Dictionary<string, object> signDic = new Dictionary<string, object>();
        Dictionary<string, object> sendDic = new Dictionary<string, object>();
        Ret.IsQuerySuccess = false;
        Ret.IsPaymentSuccess = false;
        Ret.ProviderCode = SettingData.ProviderCode;

        signDic.Add("Account", SettingData.MerchantCode);//
        signDic.Add("OrderNo", payment.PaymentSerial);//

        sendDic.Add("Data", signDic);

        signStr = JsonConvert.SerializeObject(signDic) + SettingData.MerchantKey;
        sign = CodingControl.GetMD5(signStr, false);

        sendDic.Add("Hash", sign);

        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.QueryOrderUrl, JsonConvert.SerializeObject(sendDic), payment.PaymentSerial, payment.ProviderCode);

        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "1000" && revjsonObj["message"].ToString().ToUpper() == "SUCCESSFUL")
                {
                    if (revjsonObj["result"]["status"].ToString().ToUpper() == "TRUE")
                    {
                        PayDB.InsertPaymentTransferLog("反查订单成功:订单状态为成功", 1, payment.PaymentSerial, payment.ProviderCode);
                        Ret.IsPaymentSuccess = true;
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("反查订单成功:订单状态为处理中", 1, payment.PaymentSerial, payment.ProviderCode);
                        Ret.IsPaymentSuccess = false;
                    }

                    return Ret;

                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单回传有误:" + jsonStr, 1, payment.PaymentSerial, payment.ProviderCode);
                    Ret.IsPaymentSuccess = false;
                    return Ret;
                }
            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单回传有误:回传为空值", 1, payment.PaymentSerial, payment.ProviderCode);
                Ret.IsPaymentSuccess = false;
                return Ret;
            }
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 1, payment.PaymentSerial, payment.ProviderCode);
            Ret.IsPaymentSuccess = false;
            return Ret;
            throw;
        }
    }

    public GatewayCommon.BalanceByProvider QueryPoint(string Currency)
    {
        GatewayCommon.BalanceByProvider Ret = null;
        return null;
    }


    GatewayCommon.WithdrawalByProvider GatewayCommon.ProviderGatewayByWithdraw.QueryWithdrawal(GatewayCommon.Withdrawal withdrawal)
    {
        GatewayCommon.WithdrawalByProvider retValue = new GatewayCommon.WithdrawalByProvider() { IsQuerySuccess=false };
        string sign;
        string signStr = "";
        string GUID = Guid.NewGuid().ToString("N");
        Dictionary<string, object> signDic = new Dictionary<string, object>();
        Dictionary<string, object> sendDic = new Dictionary<string, object>();

        try
        {

            signDic.Add("Account", SettingData.MerchantCode);//
            signDic.Add("OrderNo", withdrawal.WithdrawSerial);//

            sendDic.Add("Data", signDic);

            signStr = JsonConvert.SerializeObject(signDic) + SettingData.MerchantKey;
            sign = CodingControl.GetMD5(signStr, false);

            sendDic.Add("Hash", sign);

            var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.QueryWithdrawUrl, JsonConvert.SerializeObject(sendDic), withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            PayDB.InsertPaymentTransferLog("查詢代付訂單:" + JsonConvert.SerializeObject(signDic), 5, withdrawal.WithdrawSerial, SettingData.ProviderCode);

            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);
                PayDB.InsertPaymentTransferLog("查詢代付訂單結果:" + jsonStr, 5, withdrawal.WithdrawSerial, SettingData.ProviderCode);
                //return HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + jsonStr;

                if (revjsonObj != null && revjsonObj["code"].ToString() == "1000" && revjsonObj["message"].ToString().ToUpper() == "SUCCESSFUL")
                {
                    if (revjsonObj["result"]["status"].ToString() == "0")
                    {
                        //處理中

                        retValue.WithdrawalStatus = 2;
                        retValue.Amount = 0;
                    }
                    else if (revjsonObj["result"]["status"].ToString() == "1")
                    {
                        //已完成
                        retValue.WithdrawalStatus = 0;
                        retValue.Amount = decimal.Parse(revjsonObj["result"]["amount"].ToString());
                    }
                    else if (revjsonObj["result"]["status"].ToString() == "2")
                    {
                        //失敗
                        retValue.WithdrawalStatus = 1;
                        retValue.Amount = decimal.Parse(revjsonObj["result"]["amount"].ToString());
                    }
                    else {
                        retValue.WithdrawalStatus = 2;
                        retValue.Amount = 0;
                    }

                    retValue.IsQuerySuccess = true;
                    retValue.UpOrderID = "";
                    retValue.ProviderCode = withdrawal.ProviderCode;
                    retValue.ProviderReturn = jsonStr;
                }
            }
            else
            {
                PayDB.InsertPaymentTransferLog(" get json retrun error;", 5, withdrawal.WithdrawSerial, SettingData.ProviderCode);
                retValue.WithdrawalStatus = 2;
                retValue.IsQuerySuccess = false;
                retValue.UpOrderID = "";
                retValue.ProviderCode = withdrawal.ProviderCode;
                retValue.ProviderReturn = "";
                retValue.Amount = 0;
            }
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("Exception:" + ex.Message, 5, withdrawal.WithdrawSerial, SettingData.ProviderCode);
            retValue.WithdrawalStatus = 2;
            retValue.IsQuerySuccess = false;
            retValue.UpOrderID = "";
            retValue.ProviderCode = withdrawal.ProviderCode;
            retValue.ProviderReturn = "";
            retValue.Amount = 0;
            throw;
        }

        return retValue;
    }

    GatewayCommon.ReturnWithdrawByProvider GatewayCommon.ProviderGatewayByWithdraw.SendWithdrawal(GatewayCommon.Withdrawal withdrawal)
    {
        GatewayCommon.ReturnWithdrawByProvider retValue = new GatewayCommon.ReturnWithdrawByProvider() { ReturnResult = "", UpOrderID = "" };

        string sign;
        string signStr = "";
        RSAUtil rsaUtil = new RSAUtil();
        Dictionary<string, object> signDic = new Dictionary<string, object>();
        Dictionary<string, object> sendDic = new Dictionary<string, object>();

        try
        {

            signDic.Add("Account", SettingData.MerchantCode);//
            signDic.Add("OrderNo", withdrawal.WithdrawSerial);//
            signDic.Add("BankName", withdrawal.BankName);//
            signDic.Add("BranchName", withdrawal.BankBranchName);//
            signDic.Add("BankAccount", withdrawal.BankCard);//
            signDic.Add("UserName", withdrawal.BankCardName.Replace("　",""));//
            signDic.Add("Amount", int.Parse(withdrawal.Amount.ToString("#")));//
            sendDic.Add("Data", signDic);
            
            signStr = JsonConvert.SerializeObject(signDic) + SettingData.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            sendDic.Add("Hash", sign);

            PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(sendDic), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.WithdrawUrl, JsonConvert.SerializeObject(sendDic), withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);



                if (revjsonObj != null && revjsonObj["code"].ToString() == "1000" && revjsonObj["message"].ToString().ToUpper() == "SUCCESSFUL")
                {
                    retValue.SendStatus = 1;
                    retValue.UpOrderID = "";
                    retValue.WithdrawSerial = withdrawal.WithdrawSerial;
                    retValue.ReturnResult = jsonStr;
                    PayDB.InsertPaymentTransferLog("申请订单完成", 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商回传有误:" + jsonStr, 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
                    retValue.ReturnResult = jsonStr;
                    retValue.SendStatus = 0;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
                retValue.ReturnResult = jsonStr;
                retValue.SendStatus = 0;
            }
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 5, withdrawal.WithdrawSerial, SettingData.ProviderCode);
            retValue.ReturnResult = ex.Message;
            retValue.SendStatus = 0;
            throw;
        }
        return retValue;
    }

    private GatewayCommon.ProviderSetting SettingData;




}