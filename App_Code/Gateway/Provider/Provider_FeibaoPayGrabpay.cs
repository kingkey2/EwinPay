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
public class Provider_FeibaoPayGrabpay : GatewayCommon.ProviderGateway, GatewayCommon.ProviderGatewayByWithdraw
{

    public Provider_FeibaoPayGrabpay()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("FeibaoPayGrabpay");
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
        Dictionary<string, object> signDic = new Dictionary<string, object>();
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
     
        signDic.Add("gateway", "grabpay");//
        signDic.Add("amount",decimal.Parse(payment.OrderAmount.ToString("#.##")));//
        signDic.Add("device", "desktop");//
        signDic.Add("callback_url", SettingData.NotifyAsyncUrl);//
        signDic.Add("merchant_slug", SettingData.MerchantCode);//
        signDic.Add("merchant_order_time", unixTimestamp);//
        signDic.Add("merchant_order_num", payment.PaymentSerial);//
        signDic.Add("merchant_order_remark", "FeibaoPay");//
        signDic.Add("uid", "uid");//
        signDic.Add("user_ip", payment.UserIP);//

        signDic = CodingControl.AsciiDictionary2(signDic);

        string[] arrKeys = signDic.Keys.ToArray();
        var jsonstr = "{";

        for (int i = 0; i < arrKeys.Length; i++)
        {
            var key2 = arrKeys[i];
            object value = signDic[key2];
            var type = value.GetType();

            if (arrKeys.Length - 1 == i)
            {
                if (type == typeof(string))
                {
                    jsonstr += "\"" + key2 + "\":" + "\"" + value + "\"";
                }
                else
                {
                    jsonstr += "\"" + key2 + "\":" + value;
                }
            }
            else
            {

                if (type == typeof(string))
                {
                    jsonstr += "\"" + key2 + "\":" + "\"" + value + "\",";
                }
                else
                {
                    jsonstr += "\"" + key2 + "\":" + value + ",";
                }
            }
        }

        jsonstr += "}";


        signStr = jsonstr;
     
        sign = CodingControl.EncryptStringFeibao(signStr, SettingData.MerchantKey, SettingData.OtherDatas[0]);
        sendDic.Add("merchant_slug", SettingData.MerchantCode);
        sendDic.Add("data", sign);

        PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(signDic), 1, payment.PaymentSerial, payment.ProviderCode);
        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.ProviderUrl, JsonConvert.SerializeObject(sendDic), payment.PaymentSerial, payment.ProviderCode);

        //This line executes whether or not the exception occurs.
        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "0")
                {
                   var jsonStrData= CodingControl.DecryptStringFeibao(revjsonObj["order"].ToString(),SettingData.MerchantKey, SettingData.OtherDatas[0]);

                    JObject jsonData = JObject.Parse(jsonStrData);

                    return jsonData["navigate_url"].ToString();
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
        Int32 unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        string sign;
        string signStr = "";
        Dictionary<string, object> signDic = new Dictionary<string, object>();
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        signDic.Add("merchant_slug", SettingData.MerchantCode);//
        signDic.Add("merchant_order_num", payment.PaymentSerial);//

        signDic = CodingControl.AsciiDictionary2(signDic);

        string[] arrKeys = signDic.Keys.ToArray();
        var jsonstr = "{";

        for (int i = 0; i < arrKeys.Length; i++)
        {
            var key2 = arrKeys[i];
            object value = signDic[key2];
            var type = value.GetType();

            if (arrKeys.Length - 1 == i)
            {
                if (type == typeof(string))
                {
                    jsonstr += "\"" + key2 + "\":" + "\"" + value + "\"";
                }
                else
                {
                    jsonstr += "\"" + key2 + "\":" + value;
                }
            }
            else
            {

                if (type == typeof(string))
                {
                    jsonstr += "\"" + key2 + "\":" + "\"" + value + "\",";
                }
                else
                {
                    jsonstr += "\"" + key2 + "\":" + value + ",";
                }
            }
        }

        jsonstr += "}";

        signStr = jsonstr;

        sign = CodingControl.EncryptStringFeibao(signStr, SettingData.MerchantKey, SettingData.OtherDatas[0]);
        sendDic.Add("merchant_slug", SettingData.MerchantCode);
        sendDic.Add("data", sign);

        var jsonStr = GatewayCommon.RequestJsonAPI(SettingData.QueryOrderUrl, JsonConvert.SerializeObject(sendDic), payment.PaymentSerial, payment.ProviderCode);

        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["code"].ToString() == "0")
                {
                    var jsonStrData = CodingControl.DecryptStringFeibao(revjsonObj["order"].ToString(), SettingData.MerchantKey, SettingData.OtherDatas[0]);

                    JObject jsonData = JObject.Parse(jsonStrData);


                    if (jsonData["status"].ToString().ToLower() == "success"|| jsonData["status"].ToString().ToLower() == "success_done")
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
        Dictionary<string, object> signDic = new Dictionary<string, object>();
        string sign;
        string signStr = "";
        string ProviderBankCode;
        Int32 unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

        var array = GatewayCommon.GetWithdrawBankSettingData("PHP");
        JObject jo = array.Children<JObject>()
    .FirstOrDefault(o => o["BankName"] != null && o["BankName"].ToString() == withdrawal.BankName && o["FeibaoPay"] != null);

        if (jo == null)
        {
            retValue.ReturnResult = "不支援此银行";
            PayDB.InsertPaymentTransferLog("不支援此银行,银行名称:" + withdrawal.BankName, 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
            retValue.SendStatus = 0;
            return retValue;
        }

        if (jo["FeibaoPay"].ToString() == "-1")
        {
            retValue.ReturnResult = "不支援此银行";
            PayDB.InsertPaymentTransferLog("不支援此银行,银行名称:" + jo["BankName"].ToString(), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
            retValue.SendStatus = 0;
            return retValue;
        }

        ProviderBankCode = jo["FeibaoPay"].ToString();

        try
        {
            signDic.Add("gateway", "grabpay");//
            signDic.Add("merchant_order_num", withdrawal.WithdrawSerial);//
            signDic.Add("amount", decimal.Parse(withdrawal.Amount.ToString("#.##")));//
            signDic.Add("callback_url", SettingData.WithdrawNotifyAsyncUrl);//
            signDic.Add("merchant_order_time", unixTimestamp);//
            signDic.Add("merchant_order_remark", "FeibaoPay");//
            signDic.Add("bank_code", ProviderBankCode);//
            if (withdrawal.BankName.ToUpper() == "GCASH")
            {
                signDic.Add("card_holder", withdrawal.BankCard);//
                signDic.Add("card_number", withdrawal.BankCard);//
            }
            else
            {
                signDic.Add("card_holder", withdrawal.BankCardName);//
                signDic.Add("card_number", withdrawal.BankCard);//
            }

            signDic = CodingControl.AsciiDictionary2(signDic);

            string[] arrKeys = signDic.Keys.ToArray();
            var jsonstr = "{";

            for (int i = 0; i < arrKeys.Length; i++)
            {
                var key2 = arrKeys[i];
                object value = signDic[key2];
                var type = value.GetType();

                if (arrKeys.Length - 1 == i)
                {
                    if (type == typeof(string))
                    {
                        jsonstr += "\"" + key2 + "\":" + "\"" + value + "\"";
                    }
                    else
                    {
                        jsonstr += "\"" + key2 + "\":" + value;
                    }
                }
                else
                {

                    if (type == typeof(string))
                    {
                        jsonstr += "\"" + key2 + "\":" + "\"" + value + "\",";
                    }
                    else
                    {
                        jsonstr += "\"" + key2 + "\":" + value + ",";
                    }
                }
            }

            jsonstr += "}";


            signStr = jsonstr;

            sign = CodingControl.EncryptStringFeibao(signStr, SettingData.MerchantKey, SettingData.OtherDatas[0]);
            sendDic.Add("merchant_slug", SettingData.MerchantCode);
            sendDic.Add("data", sign);


            PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(signDic), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

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