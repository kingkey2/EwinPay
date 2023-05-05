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
public class Provider_HeroPay : GatewayCommon.ProviderGateway, GatewayCommon.ProviderGatewayByWithdraw
{

    public Provider_HeroPay()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("HeroPay");
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
        Dictionary<string, object> sendDic = new Dictionary<string, object>();


        sendDic.Add("mchId", SettingData.MerchantCode);//
        sendDic.Add("appId", "1005be33a311410597dc3d43a7078c2d");//
        sendDic.Add("productId", "8000");//
        sendDic.Add("mchOrderNo", payment.PaymentSerial);//
        sendDic.Add("currency", "jpy");
   
        sendDic.Add("amount", (payment.OrderAmount*100).ToString("#"));//
        sendDic.Add("clientIp", CodingControl.GetUserIP());//
        sendDic.Add("notifyUrl", SettingData.NotifyAsyncUrl);//
        sendDic.Add("subject", "subject");//
        sendDic.Add("body", "body");//
        

        sendDic = CodingControl.AsciiDictionary2(sendDic);

        foreach (KeyValuePair<string, object> item in sendDic)
        {
            signStr += item.Key + "=" + item.Value + "&";
        }
        signStr += "key=" + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false).ToUpper();

        sendDic.Add("sign", sign);

        PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(sendDic), 1, payment.PaymentSerial, payment.ProviderCode);
        var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.ProviderUrl, sendDic, payment.PaymentSerial, payment.ProviderCode);

        //This line executes whether or not the exception occurs.
        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["retCode"].ToString().ToUpper() == "SUCCESS")
                {
                  
                    PayDB.InsertPaymentTransferLog("申请订单完成", 1, payment.PaymentSerial, payment.ProviderCode);
                    return revjsonObj["payParams"]["payUrl"].ToString();
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
        Dictionary<string, object> sendDic = new Dictionary<string, object>();
        Dictionary<string, string> sendDic2 = new Dictionary<string, string>();
        Ret.IsQuerySuccess = false;
        Ret.IsPaymentSuccess = false;
        Ret.ProviderCode = SettingData.ProviderCode;

        sendDic.Add("mchId", SettingData.MerchantCode);//
        sendDic.Add("appId", "1005be33a311410597dc3d43a7078c2d");//
        sendDic.Add("mchOrderNo", payment.PaymentSerial);//
        sendDic.Add("payOrderId", "");//
        sendDic.Add("executeNotify", false);//
        
        sendDic = CodingControl.AsciiDictionary2(sendDic);

        foreach (KeyValuePair<string, object> item in sendDic)
        {
            if (!string.IsNullOrEmpty(item.Value.ToString()))
            {
                signStr += item.Key + "=" + item.Value + "&";
            }
           
        }
        signStr += "key=" + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false).ToUpper();

        sendDic.Add("sign", sign);

        sendDic2.Add("params", JsonConvert.SerializeObject(sendDic));

        PayDB.InsertPaymentTransferLog("查詢充值單,传出资料:" + JsonConvert.SerializeObject(sendDic), 1, payment.PaymentSerial, payment.ProviderCode);
        var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.QueryOrderUrl, sendDic2, payment.PaymentSerial, payment.ProviderCode);

        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["retCode"].ToString().ToUpper() == "SUCCESS")
                {
                    if (revjsonObj["status"].ToString() == "2"|| revjsonObj["status"].ToString() == "3")
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
        Dictionary<string, string> sendDic = new Dictionary<string, string>();

        try
        {
            signDic.Add("mchId", SettingData.MerchantCode);//
            signDic.Add("appId", "1005be33a311410597dc3d43a7078c2d");//
            signDic.Add("mchTransOrderNo", withdrawal.WithdrawSerial);//
            signDic.Add("transOrderId", "");//
            signDic.Add("executeNotify", false);//

            signDic = CodingControl.AsciiDictionary2(signDic);

            foreach (KeyValuePair<string, object> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value.ToString()))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
              
            }
            signStr += "key=" + SettingData.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            signDic.Add("sign", sign);

            sendDic.Add("params", JsonConvert.SerializeObject(signDic));

            var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.QueryWithdrawUrl, sendDic, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            PayDB.InsertPaymentTransferLog("查詢代付訂單:" + JsonConvert.SerializeObject(signDic), 5, withdrawal.WithdrawSerial, SettingData.ProviderCode);

            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);
                PayDB.InsertPaymentTransferLog("查詢代付訂單結果:" + jsonStr, 5, withdrawal.WithdrawSerial, SettingData.ProviderCode);
                //return HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + jsonStr;

                if (revjsonObj != null && revjsonObj["retCode"].ToString().ToUpper() == "SUCCESS")
                {
                    if (revjsonObj["status"].ToString() == "0"|| revjsonObj["status"].ToString() == "1")
                    {
                        //處理中

                        retValue.WithdrawalStatus = 2;
                        retValue.Amount = 0;
                    }
                    else if (revjsonObj["status"].ToString() == "2")
                    {
                        //已完成
                        retValue.WithdrawalStatus = 0;
                        retValue.Amount = 0;
                    }
                    else if (revjsonObj["status"].ToString() == "3")
                    {
                        //失敗
                        retValue.WithdrawalStatus = 1;
                        retValue.Amount = 0;
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
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        string ProviderBankCode = "";
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

            signDic.Add("mchId", SettingData.MerchantCode);//
            signDic.Add("appId", "1005be33a311410597dc3d43a7078c2d");//
            signDic.Add("mchTransOrderNo", withdrawal.WithdrawSerial);//
            signDic.Add("currency", "jpy");//
            signDic.Add("amount", int.Parse((withdrawal.Amount*100).ToString("#")));//
            signDic.Add("clientIp", CodingControl.GetUserIP());//
            signDic.Add("notifyUrl", SettingData.WithdrawNotifyAsyncUrl);//
            signDic.Add("bankCode", ProviderBankCode);//
            signDic.Add("bankName", withdrawal.BankName);//
            signDic.Add("accountType", 1);//
            signDic.Add("accountNo", withdrawal.BankCard);//
            signDic.Add("accountName", withdrawal.BankCardName);//

            signDic = CodingControl.AsciiDictionary2(signDic);

            foreach (KeyValuePair<string, object> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }
            signStr += "key=" + SettingData.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            signDic.Add("sign", sign);

            sendDic.Add("params", JsonConvert.SerializeObject(signDic));
            
            PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(sendDic), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.WithdrawUrl, sendDic, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["retCode"].ToString().ToUpper() == "SUCCESS")
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