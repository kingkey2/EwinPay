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
public class Provider_TigerPay : GatewayCommon.ProviderGateway, GatewayCommon.ProviderGatewayByWithdraw
{

    public Provider_TigerPay()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("TigerPay");
    }

    Dictionary<string, string> GatewayCommon.ProviderGateway.GetSubmitData(GatewayCommon.Payment payment)
    {
        string sign;
        string signStr = "";
        Dictionary<string, string> signDic = new Dictionary<string, string>();
        var a = DateTime.Now.ToUniversalTime();

        signDic.Add("p_num", SettingData.MerchantCode);//
        signDic.Add("currency", "JPY");//
        signDic.Add("amount", payment.OrderAmount.ToString("#"));//
        signDic.Add("Free", payment.PaymentSerial);//
        signDic.Add("trans_id", payment.PaymentSerial);
        signDic.Add("return_url", SettingData.NotifyAsyncUrl);//
 
        signStr = SettingData.ProviderPublicKey + SettingData.MerchantKey + SettingData.MerchantCode;

        sign = CodingControl.GetSHA256(signStr, false);

        signDic.Add("signature", sign);

        PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(signDic), 1, payment.PaymentSerial, payment.ProviderCode);

        return signDic;
    }

    string GatewayCommon.ProviderGateway.GetCompleteUrl(GatewayCommon.Payment payment)
    {
        return SettingData.ProviderUrl;
    }

    GatewayCommon.ProviderRequestType GatewayCommon.ProviderGateway.GetRequestType()
    {
        return SettingData.RequestType;
    }

    public GatewayCommon.PaymentByProvider QueryPayment(GatewayCommon.Payment payment)
    {
        GatewayCommon.PaymentByProvider Ret = new GatewayCommon.PaymentByProvider();
        string sign;
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        Ret.IsQuerySuccess = false;
        Ret.IsPaymentSuccess = false;
        Ret.ProviderCode = SettingData.ProviderCode;

        sendDic.Add("p_num", SettingData.MerchantCode);//

        sendDic.Add("txs", payment.PaymentSerial);//
        sendDic = CodingControl.AsciiDictionary(sendDic);

        sign = CodingControl.HmacSha512("settlement_result"+payment.PaymentSerial, SettingData.MerchantKey);

        sendDic.Add("signature", sign);
      
        //PayDB.InsertPaymentTransferLog("訂單查詢資料:" + JsonConvert.SerializeObject(sendDic), 1, payment.PaymentSerial, payment.ProviderCode);

        var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.QueryOrderUrl, sendDic, payment.PaymentSerial, payment.ProviderCode);

        //PayDB.InsertPaymentTransferLog("訂單查詢結果:"+ jsonStr, 1, payment.PaymentSerial, payment.ProviderCode);

        try
        {
            if (!string.IsNullOrEmpty(jsonStr))
            {
                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["result"].ToString().ToUpper() == "00"&& revjsonObj["status"].ToString().ToUpper() == "OK")
                {
                    
                    if (revjsonObj.SelectToken("txs").SelectToken(payment.PaymentSerial) !=null)
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
        Dictionary<string, string> signDic = new Dictionary<string, string>();

        string sign;
        string signStr = "";

        try
        {

            signDic.Add("p_num", SettingData.MerchantCode);
            signDic.Add("from_account", SettingData.OtherDatas[0]);
            signDic.Add("to_account", withdrawal.BankCard);
            signDic.Add("currency", "JPY");//
            signDic.Add("amount", withdrawal.Amount.ToString("#"));//
            signDic.Add("debit_currency", "JPY");//
            signDic.Add("trans_id", withdrawal.WithdrawSerial);

            signStr = SettingData.OtherDatas[0] + SettingData.MerchantKey + SettingData.MerchantCode+ withdrawal.Amount.ToString("#");

            sign = CodingControl.GetSHA256(signStr, false);

            signDic.Add("signature", sign);

            PayDB.InsertPaymentTransferLog("通知供应商,传出资料:" + JsonConvert.SerializeObject(signDic), 5, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            var jsonStr = GatewayCommon.RequestFormDataConentTypeAPI(SettingData.WithdrawUrl, signDic, withdrawal.WithdrawSerial, withdrawal.ProviderCode);

            if (!string.IsNullOrEmpty(jsonStr))
            {

                JObject revjsonObj = JObject.Parse(jsonStr);

                if (revjsonObj != null && revjsonObj["result"].ToString() == "00" && revjsonObj["status"].ToString().ToUpper() == "OK")
                {
                    retValue.SendStatus = 2;
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

    public class TigerPayWithdrawData
    {
        public string result { get; set; }
        public string status { get; set; }
        public string transaction_number { get; set; }
        public string currency { get; set; }
        public string amount { get; set; }
        public string fee { get; set; }
    }

    private GatewayCommon.ProviderSetting SettingData;




}