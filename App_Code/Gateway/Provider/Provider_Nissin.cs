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
public class Provider_Nissin : GatewayCommon.ProviderGateway, GatewayCommon.ProviderGatewayByWithdraw
{

    public Provider_Nissin()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("Nissin");
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
        var a = DateTime.Now.ToUniversalTime();

        signDic.Add("merchantCode", SettingData.MerchantCode);//
        signDic.Add("signType", "MD5");//
        signDic.Add("method", "C2C_DIRECT");//
        signDic.Add("timestamp", DateTime.Now.ToUniversalTime().ToString("yyyyMMddHHmmss"));
        signDic.Add("merchantOrderNo", payment.PaymentSerial);//
        signDic.Add("orderAmount", payment.OrderAmount.ToString("#"));//
        signDic.Add("accountName", payment.UserName);//
        signDic.Add("notifyUrl", SettingData.NotifyAsyncUrl);//

        signDic = CodingControl.AsciiDictionary(signDic);

        foreach (KeyValuePair<string, string> item in signDic)
        {
            signStr += item.Value;
        }

        signStr = signStr + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false);

        signDic.Add("sign", sign);

        PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(signDic), 1, payment.PaymentSerial, payment.ProviderCode);
        var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.ProviderUrl, signDic, payment.PaymentSerial, payment.ProviderCode);

        //This line executes whether or not the exception occurs.
        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "SUCCESS")
                {

                    PayDB.InsertPaymentTransferLog("申请订单完成", 1, payment.PaymentSerial, payment.ProviderCode);
                    return revjsonObj["depositUrl"].ToString();

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
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        Ret.IsQuerySuccess = false;
        Ret.IsPaymentSuccess = false;
        Ret.ProviderCode = SettingData.ProviderCode;

        sendDic.Add("merchantCode", SettingData.MerchantCode);//
        sendDic.Add("signType", "MD5");//
        sendDic.Add("orderType", "DEPOSIT");//
        sendDic.Add("merchantOrderNo", payment.PaymentSerial);//
        sendDic = CodingControl.AsciiDictionary(sendDic);

        foreach (KeyValuePair<string, string> item in sendDic)
        {
            signStr += item.Value;
        }

        signStr = signStr + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false);

        sendDic.Add("sign", sign);

        var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.QueryOrderUrl, sendDic, payment.PaymentSerial, payment.ProviderCode);

        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString().ToUpper() == "SUCCESS")
                {
                    if (revjsonObj["orderStatus"].ToString().ToUpper() == "COMPLETED")
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
        RSAUtil rsaUtil = new RSAUtil();

        try
        {
            var array = GatewayCommon.GetWithdrawBankSettingData();
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
           
            sendDic.Add("merchantCode", SettingData.MerchantCode);//
            sendDic.Add("method", "C2C_DIRECT");//
            sendDic.Add("signType", "MD5");//
            sendDic.Add("timestamp", DateTime.Now.ToUniversalTime().ToString("yyyyMMddHHmmss"));
            sendDic.Add("merchantOrderNo", withdrawal.WithdrawSerial);//
            sendDic.Add("orderAmount", withdrawal.Amount.ToString("#"));//
            sendDic.Add("accountName", withdrawal.BankCardName);//
            sendDic.Add("accountNo", withdrawal.BankCard);//
            sendDic.Add("accountBank", ProviderBankCode);//
            sendDic.Add("accountBranch", withdrawal.BankBranchName);//
            sendDic.Add("notifyUrl", SettingData.WithdrawNotifyAsyncUrl);//
            sendDic.Add("verifyChannelNo", "0");//

            sendDic = CodingControl.AsciiDictionary(sendDic);

            foreach (KeyValuePair<string, string> item in sendDic)
            {
                signStr += item.Value;
            }

            signStr = signStr + SettingData.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            sendDic.Add("sign", sign);


            PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(sendDic), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.WithdrawUrl, sendDic, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);



                if (revjsonObj != null && revjsonObj["code"].ToString() == "SUCCESS")
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