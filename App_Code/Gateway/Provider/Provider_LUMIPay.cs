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
public class Provider_LUMIPay : GatewayCommon.ProviderGateway, GatewayCommon.ProviderGatewayByWithdraw
{

    public Provider_LUMIPay()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("LUMIPay");
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
        Dictionary<string, string> signDic = new Dictionary<string, string>();
        string tradeTypeValue = SettingData.ServiceSettings.Find(x => x.ServiceType == payment.ServiceType).TradeType;

        signDic.Add("channel_code", tradeTypeValue);//
        signDic.Add("username", SettingData.MerchantCode);//
        signDic.Add("amount", payment.OrderAmount.ToString("#.##"));//
        signDic.Add("order_number", payment.PaymentSerial);//
        signDic.Add("notify_url", SettingData.NotifyAsyncUrl);//
        signDic.Add("client_ip", CodingControl.GetUserIP());//

        signDic = CodingControl.AsciiDictionary(signDic);

        foreach (KeyValuePair<string, string> item in signDic)
        {
            signStr += item.Key + "=" + item.Value + "&";
        }

        signStr = signStr + "secret_key=" + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false);

        signDic.Add("sign", sign);


        PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(signDic), 1, payment.PaymentSerial, payment.ProviderCode);
        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.ProviderUrl, JsonConvert.SerializeObject(signDic), payment.PaymentSerial, payment.ProviderCode);

        //This line executes whether or not the exception occurs.
        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && (revjsonObj["http_status_code"].ToString() == "200"|| revjsonObj["http_status_code"].ToString() == "201"))
                {

                    PayDB.InsertPaymentTransferLog("申请订单完成", 1, payment.PaymentSerial, payment.ProviderCode);
                    return revjsonObj["data"]["casher_url"].ToString();

                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商回传有误:" + jsonStr, 1, payment.PaymentSerial, payment.ProviderCode);
                    return "error:" + revjsonObj["error_code"].ToString();
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

        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        Ret.IsQuerySuccess = false;
        Ret.IsPaymentSuccess = false;
        Ret.ProviderCode = SettingData.ProviderCode;

        sendDic.Add("username", SettingData.MerchantCode);//
        sendDic.Add("order_number", payment.PaymentSerial);//

        sendDic = CodingControl.AsciiDictionary(sendDic);

        foreach (KeyValuePair<string, string> item in sendDic)
        {
            signStr += item.Key + "=" + item.Value + "&";
        }

        signStr = signStr + "secret_key=" + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false);

        sendDic.Add("sign", sign);

        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.QueryOrderUrl, JsonConvert.SerializeObject(sendDic), payment.PaymentSerial, payment.ProviderCode);

        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && (revjsonObj["http_status_code"].ToString() == "200"|| revjsonObj["http_status_code"].ToString() == "201"))
                {
                    if (revjsonObj["data"]["status"].ToString() == "4"|| revjsonObj["data"]["status"].ToString() == "5")
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

        Dictionary<string, string> sendDic = new Dictionary<string, string>();

        try
        {

            sendDic.Add("username", SettingData.MerchantCode);//
            sendDic.Add("order_number", withdrawal.WithdrawSerial);//

            sendDic = CodingControl.AsciiDictionary(sendDic);

            foreach (KeyValuePair<string, string> item in sendDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "secret_key=" + SettingData.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            sendDic.Add("sign", sign);

            var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.QueryWithdrawUrl, JsonConvert.SerializeObject(sendDic), withdrawal.WithdrawSerial, withdrawal.ProviderCode);
            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);
                PayDB.InsertPaymentTransferLog("查詢代付訂單結果:" + jsonStr, 5, withdrawal.WithdrawSerial, SettingData.ProviderCode);
                //return HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + jsonStr;

                if (revjsonObj != null && (revjsonObj["http_status_code"].ToString() == "200" || revjsonObj["http_status_code"].ToString() == "201"))
                {
                    if (revjsonObj["data"]["status"].ToString() == "4"|| revjsonObj["data"]["status"].ToString() == "5")
                    {
                        //已完成
                        retValue.WithdrawalStatus = 0;
                        retValue.Amount = 0;
                    }
                    else if (revjsonObj["data"]["status"].ToString() == "6"|| revjsonObj["data"]["status"].ToString() == "7" || revjsonObj["data"]["status"].ToString() == "8")
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
        string ProviderBankCode;

        try
        {
            var array = GatewayCommon.GetWithdrawBankSettingData("PHP");
            JObject jo = array.Children<JObject>()
        .FirstOrDefault(o => o["BankName"] != null && o["BankName"].ToString() == withdrawal.BankName && o[SettingData.ProviderCode] != null);

            if (jo == null)
            {
                retValue.ReturnResult = "不支援此银行";
                PayDB.InsertPaymentTransferLog("不支援此银行,银行名称:" + withdrawal.BankName, 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
                retValue.SendStatus = 0;
                return retValue;
            }

            if (jo[SettingData.ProviderCode].ToString() == "-1")
            {
                retValue.ReturnResult = "不支援此银行";
                PayDB.InsertPaymentTransferLog("不支援此银行,银行名称:" + jo["BankName"].ToString(), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
                retValue.SendStatus = 0;
                return retValue;
            }

            ProviderBankCode = jo[SettingData.ProviderCode].ToString();

            sendDic.Add("username", SettingData.MerchantCode);//
            sendDic.Add("amount", withdrawal.Amount.ToString("#.##"));//
            sendDic.Add("order_number", withdrawal.WithdrawSerial);//
            sendDic.Add("notify_url", SettingData.WithdrawNotifyAsyncUrl);//

            if (withdrawal.BankName.ToUpper() == "GCASH")
            {
                sendDic.Add("bank_card_holder_name", withdrawal.BankCard);//
                sendDic.Add("bank_card_number", withdrawal.BankCard);//
              
            }
            else
            {
                sendDic.Add("bank_card_holder_name", withdrawal.BankCardName);//
                sendDic.Add("bank_card_number", withdrawal.BankCard);//
            }  
            
            sendDic.Add("bank_name", ProviderBankCode);//

            sendDic = CodingControl.AsciiDictionary(sendDic);

            foreach (KeyValuePair<string, string> item in sendDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "secret_key=" + SettingData.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            sendDic.Add("sign", sign);

            PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(sendDic), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.WithdrawUrl, JsonConvert.SerializeObject(sendDic), withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);



                if (revjsonObj != null && (revjsonObj["http_status_code"].ToString() == "200" || revjsonObj["http_status_code"].ToString() == "201"))
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
                    retValue.ReturnResult = revjsonObj["error_code"].ToString();
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