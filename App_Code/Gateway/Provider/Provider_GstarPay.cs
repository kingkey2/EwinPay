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
public class Provider_GstarPay : GatewayCommon.ProviderGateway, GatewayCommon.ProviderGatewayByWithdraw
{

    public Provider_GstarPay()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("GstarPay");
    }

    Dictionary<string, string> GatewayCommon.ProviderGateway.GetSubmitData(GatewayCommon.Payment payment)
    {
        Dictionary<string, string> dataDic = new Dictionary<string, string>();
        return dataDic;
    }

    private string GetAuthToken(string merchantNo,string dateTime,string PaymentSerial,string ProviderCode) {
        Dictionary<string, string> signDic = new Dictionary<string, string>();
        string signStr = "";
        string sign = "";
        signDic.Add("merchantNo", merchantNo);//
        signDic.Add("dateTime", dateTime);//

        foreach (KeyValuePair<string, string> item in signDic)
        {
            signStr += item.Key + "=" + item.Value + "&";
        }

        signStr = signStr + "authKey=" + SettingData.ProviderPublicKey;
        sign = CodingControl.GetMD5(signStr, false);
        signDic.Add("signature", sign);//

        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.QueryBalanceUrl, JsonConvert.SerializeObject(signDic), PaymentSerial, ProviderCode);
        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "0")
                {
                    return revjsonObj["data"]["auth_token"].ToString();
                }
                else {
                    return "";
                }

            }
            else
            {
                return "";
            }
        }
        catch (Exception ex)
        {
            return "";
            throw;
        }
    }


    string GatewayCommon.ProviderGateway.GetCompleteUrl(GatewayCommon.Payment payment)
    {

        string sign;
        string signStr = "";
        Dictionary<string, string> signDic = new Dictionary<string, string>();
        string tradeTypeValue = SettingData.ServiceSettings.Find(x => x.ServiceType == payment.ServiceType).TradeType;
        string datetime = DateTime.Now.ToString("yyMMddHHmmss");
        string AuthToken = GetAuthToken(SettingData.MerchantCode, datetime, payment.PaymentSerial, payment.ProviderCode);
        signDic.Add("merchantNo", SettingData.MerchantCode);//
        signDic.Add("merchantUser", "merchantUser");//
        signDic.Add("merchantOrder", payment.PaymentSerial);//
        signDic.Add("channel", tradeTypeValue);//
        signDic.Add("amount", payment.OrderAmount.ToString("#.##"));//
        signDic.Add("currency", "PHP");//
        signDic.Add("dateTime", datetime);//


        foreach (KeyValuePair<string, string> item in signDic)
        {
            signStr += item.Key + "=" + item.Value + "&";
        }

        signStr = signStr + "payKey=" + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false);

        signDic.Add("signature", sign);
        signDic.Add("callbackUrl", SettingData.NotifyAsyncUrl);//

        PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(signDic), 1, payment.PaymentSerial, payment.ProviderCode);
        var jsonStr = GatewayCommon.RequestJsonAPIByAuthorization(SettingData.ProviderUrl, JsonConvert.SerializeObject(signDic), payment.PaymentSerial, payment.ProviderCode, AuthToken);

        //This line executes whether or not the exception occurs.
        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "0")
                {

                    PayDB.InsertPaymentTransferLog("申请订单完成", 1, payment.PaymentSerial, payment.ProviderCode);
                    return revjsonObj["data"]["pageUrl"].ToString();

                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商回传有误:" + jsonStr, 1, payment.PaymentSerial, payment.ProviderCode);
                    return "error:" + revjsonObj["message"].ToString();
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
        string datetime = DateTime.Now.ToString("yyMMddHHmmss");
        string AuthToken = GetAuthToken(SettingData.MerchantCode, datetime, payment.PaymentSerial, payment.ProviderCode);
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        Ret.IsQuerySuccess = false;
        Ret.IsPaymentSuccess = false;
        Ret.ProviderCode = SettingData.ProviderCode;

        sendDic.Add("merchantNo", SettingData.MerchantCode);//
        sendDic.Add("merchantOrder", payment.PaymentSerial);//
        sendDic.Add("dateTime", datetime);//
      
        foreach (KeyValuePair<string, string> item in sendDic)
        {
            signStr += item.Key + "=" + item.Value + "&";
        }

        signStr = signStr + "payKey=" + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false).ToLower();

        sendDic.Add("signature", sign);

        var jsonStr = GatewayCommon.RequestJsonAPIByAuthorization(SettingData.QueryOrderUrl, JsonConvert.SerializeObject(sendDic), payment.PaymentSerial, payment.ProviderCode, AuthToken);

        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "0")
                {
                    if (revjsonObj["data"]["status"].ToString() == "2")
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
        GatewayCommon.WithdrawalByProvider retValue = new GatewayCommon.WithdrawalByProvider() { IsQuerySuccess = false };
        string sign;
        string signStr = "";
        string datetime = DateTime.Now.ToString("yyMMddHHmmss");
        string AuthToken = GetAuthToken(SettingData.MerchantCode, datetime, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
        Dictionary<string, string> sendDic = new Dictionary<string, string>();

        try
        {

            sendDic.Add("merchantNo", SettingData.MerchantCode);//
            sendDic.Add("merchantOrder", withdrawal.WithdrawSerial);//
            sendDic.Add("dateTime", datetime);//


            foreach (KeyValuePair<string, string> item in sendDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "payKey=" + SettingData.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            sendDic.Add("signature", sign);

            var jsonStr = GatewayCommon.RequestJsonAPIByAuthorization(SettingData.QueryWithdrawUrl, JsonConvert.SerializeObject(sendDic), withdrawal.WithdrawSerial, withdrawal.ProviderCode, AuthToken);
            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);
                PayDB.InsertPaymentTransferLog("查詢代付訂單結果:" + jsonStr, 5, withdrawal.WithdrawSerial, SettingData.ProviderCode);
                //return HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + jsonStr;

                if (revjsonObj != null && revjsonObj["code"].ToString() == "0")
                {
                    if (revjsonObj["data"]["status"].ToString() == "2")
                    {
                        //已完成
                        retValue.WithdrawalStatus = 0;
                        retValue.Amount = 0;
                    }
                    else if (revjsonObj["data"]["status"].ToString() == "6"|| revjsonObj["data"]["status"].ToString() == "7")
                    {
                        //失敗
                        retValue.WithdrawalStatus = 1;
                        retValue.Amount = 0;
                    }
                    else
                    {
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
        Dictionary<string, string> sendDic = new Dictionary<string, string>();

        string sign;
        string signStr = "";
        string datetime = DateTime.Now.ToString("yyMMddHHmmss");
        string ProviderBankCode;
        string AuthToken = GetAuthToken(SettingData.MerchantCode, datetime, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
        string ProviderCode = "PoPay";
        try
        {
            var array = GatewayCommon.GetWithdrawBankSettingData("PHP");
            JObject jo = array.Children<JObject>()
        .FirstOrDefault(o => o["BankName"] != null && o["BankName"].ToString() == withdrawal.BankName&&o[ProviderCode]!=null);

            PayDB.InsertPaymentTransferLog("jo:" + jo.ToString(), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
            if (jo == null)
            {
                retValue.ReturnResult = "不支援此银行";
                PayDB.InsertPaymentTransferLog("不支援此银行,银行名称:" + withdrawal.BankName, 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
                retValue.SendStatus = 0;
                return retValue;
            }
        
            if (jo[ProviderCode].ToString() == "-1")
            {
                retValue.ReturnResult = "不支援此银行";
                PayDB.InsertPaymentTransferLog("不支援此银行,银行名称:" + jo["BankName"].ToString(), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
                retValue.SendStatus = 0;
                return retValue;
            }

            ProviderBankCode = jo[ProviderCode].ToString();

            sendDic.Add("merchantNo", SettingData.MerchantCode);//
            sendDic.Add("merchantOrder", withdrawal.WithdrawSerial);//
            sendDic.Add("channel", "007002102");//
            sendDic.Add("amount", withdrawal.Amount.ToString("#.##"));//

            sendDic.Add("currency", "PHP");//
            sendDic.Add("bankCode", ProviderBankCode);//
            sendDic.Add("bankAccountName", withdrawal.BankCardName);//
            sendDic.Add("bankAccountNo", withdrawal.BankCard);//
            sendDic.Add("dateTime", datetime);//


            foreach (KeyValuePair<string, string> item in sendDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "payKey=" + SettingData.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            sendDic.Add("signature", sign);
            sendDic.Add("callbackUrl", SettingData.WithdrawNotifyAsyncUrl);//


            PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(sendDic), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            var jsonStr = GatewayCommon.RequestJsonAPIByAuthorization(SettingData.WithdrawUrl, JsonConvert.SerializeObject(sendDic), withdrawal.WithdrawSerial, withdrawal.ProviderCode, AuthToken);
            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);


                if (revjsonObj != null && revjsonObj["code"].ToString() == "0")
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
                    retValue.ReturnResult = revjsonObj["message"].ToString();
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