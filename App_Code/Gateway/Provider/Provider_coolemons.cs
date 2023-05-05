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
public class Provider_coolemons : GatewayCommon.ProviderGateway, GatewayCommon.ProviderGatewayByWithdraw
{

    public Provider_coolemons()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("coolemons");
    }

    Dictionary<string, string> GatewayCommon.ProviderGateway.GetSubmitData(GatewayCommon.Payment payment)
    {
        Dictionary<string, string> dataDic = new Dictionary<string, string>();
        return dataDic;
    }

    string GatewayCommon.ProviderGateway.GetCompleteUrl(GatewayCommon.Payment payment)
    {
        Int32 unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        string sign;
        string signStr = "";
        int tradeTypeValue = int.Parse(SettingData.ServiceSettings.Find(x => x.ServiceType == payment.ServiceType).TradeType);
        Dictionary<string, object> sendDic = new Dictionary<string, object>();
        Dictionary<string, object> signDic = new Dictionary<string, object>();

        signDic.Add("merchantNo", SettingData.MerchantCode);//
        signDic.Add("orderNo", payment.PaymentSerial);//
        signDic.Add("userNo", 0);
        signDic.Add("channelNo", tradeTypeValue);
        signDic.Add("amount",payment.OrderAmount.ToString("#.00"));
        signDic.Add("discount", "0.00");
        signDic.Add("exchangeRate", 1);
        signDic.Add("payeeName", payment.UserName);
        signDic.Add("extra", "extra");
        signDic.Add("datetime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        signDic.Add("notifyUrl", SettingData.NotifyAsyncUrl);//
        signDic.Add("time", unixTimestamp);//
        signDic.Add("appSecret", SettingData.OtherDatas[0]);//

      
        signDic = CodingControl.AsciiDictionary2(signDic);

        foreach (KeyValuePair<string, object> item in signDic)
        {
            if (item.Key!= "userName"&& item.Key != "channelNo" && item.Key != "payeeName" && item.Key != "bankName" && item.Key != "appSecret"&& item.Key != "exchangeRate")
            {
                signStr += item.Key + "=" + item.Value + "&";
            }
        }
        signStr = signStr.Substring(0, signStr.Length - 1);
        signStr = signStr + SettingData.MerchantKey;

        sign = CodingControl.GetSHA256(signStr,false);
        sign = CodingControl.GetMD5(sign, false).ToUpper();
        signDic.Add("sign", sign);
     
        PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(signDic), 1, payment.PaymentSerial, payment.ProviderCode);
        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.ProviderUrl, JsonConvert.SerializeObject(signDic), payment.PaymentSerial, payment.ProviderCode);

        //This line executes whether or not the exception occurs.
        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "0")
                {
                    PayDB.UpdatePaymentProviderOrderID(payment.PaymentID, revjsonObj["tradeNo"].ToString());
                    PayDB.InsertPaymentTransferLog("申请订单完成", 1, payment.PaymentSerial, payment.ProviderCode);
                    return revjsonObj["targetUrl"].ToString();

                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商回传有误:" + jsonStr, 1, payment.PaymentSerial, payment.ProviderCode);
                    return HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + 800;
                }
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 1, payment.PaymentSerial, payment.ProviderCode);
                return HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + 801;
            }
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 1, payment.PaymentSerial, payment.ProviderCode);
            return HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + 802;
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
        Int32 unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        string GUID = Guid.NewGuid().ToString("N");
        Dictionary<string, object> sendDic = new Dictionary<string, object>();
        Ret.IsQuerySuccess = false;
        Ret.IsPaymentSuccess = false;
        Ret.ProviderCode = SettingData.ProviderCode;


        sendDic.Add("merchantNo", SettingData.MerchantCode);//1
        sendDic.Add("tradeNo", payment.ProviderOrderID);//
        sendDic.Add("orderNo", payment.PaymentSerial);//
        sendDic.Add("fee", (payment.OrderAmount*payment.CollectRate*(decimal)0.01).ToString("#.00"));
        sendDic.Add("exchangeRate", 1);
        sendDic.Add("time", unixTimestamp);//
        sendDic.Add("appSecret", SettingData.OtherDatas[0]);//

        sendDic = CodingControl.AsciiDictionary2(sendDic);

        foreach (KeyValuePair<string, object> item in sendDic)
        {
            if (item.Key != "userName" && item.Key != "channelNo" && item.Key != "payeeName" && item.Key != "bankName" && item.Key != "appSecret" && item.Key != "exchangeRate" && item.Key != "fee")
            {
                signStr += item.Key + "=" + item.Value + "&";
            }
        }
        signStr = signStr.Substring(0, signStr.Length - 1);
        signStr = signStr + SettingData.MerchantKey;

        sign = CodingControl.GetSHA256(signStr, false);
        sign = CodingControl.GetMD5(sign, false).ToUpper();
        sendDic.Add("sign", sign);

        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.QueryOrderUrl, JsonConvert.SerializeObject(sendDic), payment.PaymentSerial, payment.ProviderCode);

        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString()== "0")
                {
                    if (revjsonObj["status"].ToString().ToUpper() == "PAID"|| revjsonObj["status"].ToString().ToUpper() == "MANUAL PAID")
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
        GatewayCommon.WithdrawalByProvider Ret = new GatewayCommon.WithdrawalByProvider();
        return Ret;

    }

    GatewayCommon.ReturnWithdrawByProvider GatewayCommon.ProviderGatewayByWithdraw.SendWithdrawal(GatewayCommon.Withdrawal withdrawal)
    {
        GatewayCommon.ReturnWithdrawByProvider retValue = new GatewayCommon.ReturnWithdrawByProvider() { ReturnResult = "", UpOrderID = "" };
        Dictionary<string, object> sendDic = new Dictionary<string, object>();
        string sign;
        string signStr = "";
        Int32 unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        RSAUtil rsaUtil = new RSAUtil();


        try
        {
            var array = GatewayCommon.GetWithdrawBankSettingData();
            JObject jo = array.Children<JObject>()
        .FirstOrDefault(o => o["BankName"] != null && o["BankName"].ToString() == withdrawal.BankName && o[SettingData.ProviderCode] != null);

            if (jo==null)
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

            sendDic.Add("merchantNo", SettingData.MerchantCode);//
            sendDic.Add("orderNo", withdrawal.WithdrawSerial);//
            sendDic.Add("amount", int.Parse(withdrawal.Amount.ToString("#")));//
            sendDic.Add("name", withdrawal.BankCardName);//
            sendDic.Add("exchangeRate", 1);//
            sendDic.Add("bankName", jo[SettingData.ProviderCode].ToString());//
            sendDic.Add("bankAccount", withdrawal.BankBranchName+ "-"+ withdrawal.BankCard);//
            sendDic.Add("bankBranch", "bankBranch");//
            sendDic.Add("memo", "memo");//
            sendDic.Add("mobile", "mobile");//
            sendDic.Add("datetime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sendDic.Add("notifyUrl", SettingData.WithdrawNotifyAsyncUrl);//
            sendDic.Add("reverseUrl", SettingData.WithdrawNotifyAsyncUrl);//
            sendDic.Add("extra", "extra");//
            sendDic.Add("time", unixTimestamp);//
            sendDic.Add("appSecret", SettingData.OtherDatas[0]);//

            sendDic = CodingControl.AsciiDictionary2(sendDic);

            foreach (KeyValuePair<string, object> item in sendDic)
            {
                if (item.Key != "bankBranch" && item.Key != "memo" && item.Key != "exchangerate" && item.Key != "appSecret" && item.Key != "exchangeRate")
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }
            signStr = signStr.Substring(0, signStr.Length - 1);
            signStr = signStr + SettingData.MerchantKey;

            sign = CodingControl.GetSHA256(signStr, false);
            sign = CodingControl.GetMD5(sign, false).ToUpper();
            sendDic.Add("sign", sign);


            PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(sendDic), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.WithdrawUrl, JsonConvert.SerializeObject(sendDic), withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);



                if (revjsonObj != null && revjsonObj["code"].ToString().ToUpper() == "0" && revjsonObj["text"].ToString().ToUpper() == "SUCCESS")
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