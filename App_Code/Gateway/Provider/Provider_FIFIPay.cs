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
public class Provider_FIFIPay : GatewayCommon.ProviderGateway, GatewayCommon.ProviderGatewayByWithdraw
{

    public Provider_FIFIPay()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("FIFIPay");
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
        Int32 unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        string tradeTypeValue = SettingData.ServiceSettings.Find(x => x.ServiceType == payment.ServiceType).TradeType;
        signDic.Add("pay_customer_id", SettingData.MerchantCode);//
        signDic.Add("pay_apply_date", unixTimestamp.ToString());//
        signDic.Add("pay_order_id", payment.PaymentSerial);//
        signDic.Add("pay_notify_url", SettingData.NotifyAsyncUrl);//
        signDic.Add("pay_amount", payment.OrderAmount.ToString("#.##"));//
        signDic.Add("pay_channel_id", tradeTypeValue);//

        signDic = CodingControl.AsciiDictionary(signDic);

        foreach (KeyValuePair<string, string> item in signDic)
        {
            signStr += item.Key + "=" + item.Value + "&";
        }

        signStr = signStr + "key=" + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false).ToUpper();

        signDic.Add("pay_md5_sign", sign);

        PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(signDic), 1, payment.PaymentSerial, payment.ProviderCode);
        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.ProviderUrl, JsonConvert.SerializeObject(signDic), payment.PaymentSerial, payment.ProviderCode);

        //This line executes whether or not the exception occurs.
        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "0" && revjsonObj["message"].ToString().ToLower() == "success")
                {


                    PayDB.InsertPaymentTransferLog("申请订单完成", 1, payment.PaymentSerial, payment.ProviderCode);
                    return revjsonObj["data"]["view_url"].ToString().Replace("\\", "");
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商回传有误:" + jsonStr, 1, payment.PaymentSerial, payment.ProviderCode);
                    //return "error:" + revjsonObj["message"].ToString();
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
        Int32 unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        string GUID = Guid.NewGuid().ToString("N");
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        Ret.IsQuerySuccess = false;
        Ret.IsPaymentSuccess = false;
        Ret.ProviderCode = SettingData.ProviderCode;

        sendDic.Add("pay_customer_id", SettingData.MerchantCode);//
        sendDic.Add("pay_apply_date", unixTimestamp.ToString());//
        sendDic.Add("pay_order_id", payment.PaymentSerial);//

        sendDic = CodingControl.AsciiDictionary(sendDic);

        foreach (KeyValuePair<string, string> item in sendDic)
        {
            signStr += item.Key + "=" + item.Value + "&";
        }

        signStr = signStr + "key=" + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false).ToUpper();

        sendDic.Add("pay_md5_sign", sign);

        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.QueryOrderUrl, JsonConvert.SerializeObject(sendDic), payment.PaymentSerial, payment.ProviderCode);

        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "0" && revjsonObj["message"].ToString().ToLower() == "success")
                {
                    if (revjsonObj["data"]["status"].ToString().ToUpper() == "1" || revjsonObj["data"]["status"].ToString().ToUpper() == "2")
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
        Int32 unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
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

            sendDic.Add("pay_customer_id", SettingData.MerchantCode);//
            sendDic.Add("pay_apply_date", unixTimestamp.ToString());//
            sendDic.Add("pay_order_id", withdrawal.WithdrawSerial);//
            sendDic.Add("pay_notify_url", SettingData.WithdrawNotifyAsyncUrl);//
            sendDic.Add("pay_amount", withdrawal.Amount.ToString("#.##"));//
            sendDic.Add("pay_account_name", withdrawal.BankCardName);//
            sendDic.Add("pay_card_no", withdrawal.BankCard);//
            sendDic.Add("pay_bank_name", ProviderBankCode);//

            sendDic = CodingControl.AsciiDictionary(sendDic);

            foreach (KeyValuePair<string, string> item in sendDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + SettingData.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            sendDic.Add("pay_md5_sign", sign);


            PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(sendDic), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.WithdrawUrl, JsonConvert.SerializeObject(sendDic), withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);



                if (revjsonObj != null && revjsonObj["code"].ToString() == "0" && revjsonObj["message"].ToString().ToLower() == "success")
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