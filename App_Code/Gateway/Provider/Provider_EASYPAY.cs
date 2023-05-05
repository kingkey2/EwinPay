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
public class Provider_EASYPAY : GatewayCommon.ProviderGateway, GatewayCommon.ProviderGatewayByWithdraw
{

    public Provider_EASYPAY()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("EASYPAY");
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


        signDic.Add("client_id", SettingData.MerchantCode);//
        signDic.Add("bill_number", payment.PaymentSerial);//
        signDic.Add("type", "02");//
        signDic.Add("amount", payment.OrderAmount.ToString("#.##"));//
        signDic.Add("notify_url", SettingData.NotifyAsyncUrl);//
        signDic.Add("depositor_name", "depositor_name");//
        
        signDic = CodingControl.AsciiDictionary(signDic);

        foreach (KeyValuePair<string, string> item in signDic)
        {
            signStr += item.Key+"="+ item.Value+"&";
        }

        signStr = signStr+ "key=" + SettingData.MerchantKey;

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

                if (revjsonObj != null && revjsonObj["code"].ToString() == "0")
                {

                 
                    PayDB.InsertPaymentTransferLog("申请订单完成", 1, payment.PaymentSerial, payment.ProviderCode);
                    return revjsonObj["url"].ToString();
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商回传有误:" + jsonStr, 1, payment.PaymentSerial, payment.ProviderCode);
                    return "error:"+ revjsonObj["message"].ToString();
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

        sendDic.Add("client_id", SettingData.MerchantCode);//
        sendDic.Add("bill_number", payment.PaymentSerial);//
        sendDic.Add("timestamp", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));//
        sendDic = CodingControl.AsciiDictionary(sendDic);

        foreach (KeyValuePair<string, string> item in sendDic)
        {
            signStr += item.Key + "=" + item.Value + "&";
        }

        signStr = signStr + "key=" + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false);

        sendDic.Add("sign", sign);

        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.QueryOrderUrl,JsonConvert.SerializeObject(sendDic), payment.PaymentSerial, payment.ProviderCode);

        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "0")
                {
                    if (revjsonObj["status"].ToString() == "已完成")
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

            sendDic.Add("client_id", SettingData.MerchantCode);//
            sendDic.Add("bill_number", withdrawal.WithdrawSerial);//
            sendDic.Add("amount", withdrawal.Amount.ToString("#.##"));//
            sendDic.Add("notify_url", SettingData.WithdrawNotifyAsyncUrl);//
      
            sendDic.Add("receiver_name", withdrawal.BankCardName);//
            sendDic.Add("receiver_account", withdrawal.BankCard);//
            sendDic.Add("bank", ProviderBankCode);//
            sendDic.Add("bank_branch", "N/A");//
            sendDic.Add("remark", "no");//
            sendDic = CodingControl.AsciiDictionary(sendDic);

            foreach (KeyValuePair<string, string> item in sendDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + SettingData.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            sendDic.Add("sign", sign);


            PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(sendDic), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.WithdrawUrl, JsonConvert.SerializeObject(sendDic), withdrawal.WithdrawSerial, withdrawal.ProviderCode);

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