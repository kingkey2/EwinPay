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
public class Provider_AeePay : GatewayCommon.ProviderGateway, GatewayCommon.ProviderGatewayByWithdraw
{

    public Provider_AeePay()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("AeePay");
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
        var sendTime = DateTime.Now.ToString("yyyyMMddHHmmss");
        var ip = CodingControl.GetUserIP();
        Random rand = new Random();
        Random rand2 = new Random();
        var cardnumber = (long)(rand.NextDouble() * 9000000000) + 1000000000;
        var phonenumber = (long)(rand2.NextDouble() * 9000000000) + 1000000000;
        var name = randomName(4);
        signDic.Add("merchantno", SettingData.MerchantCode);//
        signDic.Add("morderno", payment.PaymentSerial);//
        signDic.Add("productname", "RUSAKAYA");//
        signDic.Add("money", payment.OrderAmount.ToString("#.##"));//
        signDic.Add("paycode", tradeTypeValue);//
        signDic.Add("sendtime", sendTime);//
        signDic.Add("notifyurl", SettingData.NotifyAsyncUrl);//
        signDic.Add("userinfo", name + "," + cardnumber + "," + phonenumber);//
        signDic.Add("buyerip", ip);//

        signStr = SettingData.MerchantCode + "|" + payment.PaymentSerial + "|" + tradeTypeValue + "|" + SettingData.NotifyAsyncUrl + "|" + payment.OrderAmount.ToString("#.##") + "|" + sendTime + "|" + ip + "|" + SettingData.MerchantKey;

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

                if (revjsonObj != null && revjsonObj["success"].ToString().ToUpper() == "TRUE")
                {

                    PayDB.InsertPaymentTransferLog("申请订单完成", 1, payment.PaymentSerial, payment.ProviderCode);
                    return revjsonObj["data"]["payurl"].ToString();

                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商回传有误:" + revjsonObj["resultMsg"].ToString(), 1, payment.PaymentSerial, payment.ProviderCode);
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
        var sendTime = DateTime.Now.ToString("yyyyMMddHHmmss");
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        Ret.IsQuerySuccess = false;
        Ret.IsPaymentSuccess = false;
        Ret.ProviderCode = SettingData.ProviderCode;

        sendDic.Add("merchantno", SettingData.MerchantCode);//
        sendDic.Add("opttype", "1");//
        sendDic.Add("morderno", payment.PaymentSerial);//
        sendDic.Add("sendtime", sendTime);//

        signStr = SettingData.MerchantCode + "|" + "1" + "|" + payment.PaymentSerial + "|" + sendTime + "|" + SettingData.MerchantKey;

        sign = CodingControl.GetMD5(signStr, false);

        sendDic.Add("sign", sign);

        var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.QueryOrderUrl, sendDic, payment.PaymentSerial, payment.ProviderCode);

        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["success"].ToString().ToUpper() == "TRUE")
                {
                    if (revjsonObj["data"]["status"].ToString().ToUpper() == "1")
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
        GatewayCommon.WithdrawalByProvider retValue = new GatewayCommon.WithdrawalByProvider() { IsQuerySuccess = false , WithdrawalStatus =2};
        string sign;
        string signStr = "";
        string GUID = Guid.NewGuid().ToString("N");
        var sendTime = DateTime.Now.ToString("yyyyMMddHHmmss");
        Dictionary<string, string> sendDic = new Dictionary<string, string>();

        try
        {

            sendDic.Add("merchantno", SettingData.MerchantCode);//
            sendDic.Add("opttype", "2");//
            sendDic.Add("morderno", withdrawal.WithdrawSerial);//
            sendDic.Add("sendtime", sendTime);//

            signStr = SettingData.MerchantCode + "|" + "2" + "|" + withdrawal.WithdrawSerial + "|" + sendTime + "|" + SettingData.MerchantKey;
            sign = CodingControl.GetMD5(signStr, false);

            sendDic.Add("sign", sign);

            var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.QueryWithdrawUrl, sendDic, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            PayDB.InsertPaymentTransferLog("查詢代付訂單:" + JsonConvert.SerializeObject(sendDic), 5, withdrawal.WithdrawSerial, SettingData.ProviderCode);

            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);
                PayDB.InsertPaymentTransferLog("查詢代付訂單結果:" + jsonStr, 5, withdrawal.WithdrawSerial, SettingData.ProviderCode);
                //return HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + jsonStr;

                if (revjsonObj != null && revjsonObj["success"].ToString().ToUpper() == "TRUE")
                {
                    if (revjsonObj["data"]["status"].ToString() == "3")
                    {
                        //已完成
                        retValue.WithdrawalStatus = 0;
                        retValue.Amount = 0;
                    }
                    else if (revjsonObj["data"]["status"].ToString() == "4")
                    {
                        //失敗
                        retValue.WithdrawalStatus = 1;
                        retValue.Amount = 0;
                    }
                    else
                    {
                        //處理中
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

            var ProviderBankCodes = SettingData.BankCodeSettings.Where(w => w.BankCode == withdrawal.BankName);

            if (ProviderBankCodes.Count() == 0)
            {
                retValue.ReturnResult = "不支援此银行";
                PayDB.InsertPaymentTransferLog("不支援此银行,银行名称:" + withdrawal.BankName, 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);
                retValue.SendStatus = 0;
                return retValue;
            }


            ProviderBankCode = ProviderBankCodes.First().ProviderBankCode;

            var sendTime = DateTime.Now.ToString("yyyyMMddHHmmss");
            var ip = CodingControl.GetUserIP();
            sendDic.Add("merchantno", SettingData.MerchantCode);//
            sendDic.Add("morderno", withdrawal.WithdrawSerial);//
            sendDic.Add("type", "0");//
            sendDic.Add("money", withdrawal.Amount.ToString("#.##"));//
            sendDic.Add("bankcode", "IDR_PERMATA");//
            sendDic.Add("realname", withdrawal.BankCardName);//
            sendDic.Add("cardno", withdrawal.BankCard);//
            sendDic.Add("sendtime", sendTime);//
            sendDic.Add("notifyurl", SettingData.WithdrawNotifyAsyncUrl);//
            sendDic.Add("buyerip", ip);//

            signStr = SettingData.MerchantCode + "|" + withdrawal.WithdrawSerial + "|" + "IDR_PERMATA" + "|" + "0" + "|" + withdrawal.BankCardName + "|" + withdrawal.BankCard + "|" + withdrawal.Amount.ToString("#.##") + "|" + sendTime + "|" + ip + "|"  + SettingData.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            sendDic.Add("sign", sign);


            PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(sendDic), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.WithdrawUrl, sendDic, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["success"].ToString().ToUpper() == "TRUE")
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

    static string randomName(int length)
    {
        using (var crypto = new RNGCryptoServiceProvider())
        {
            var bits = (length * 6);
            var byte_size = ((bits + 7) / 8);
            var bytesarray = new byte[byte_size];
            crypto.GetBytes(bytesarray);
            return Convert.ToBase64String(bytesarray);
        }
    }

    private GatewayCommon.ProviderSetting SettingData;




}