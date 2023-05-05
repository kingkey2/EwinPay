using Ext.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Xml;

public class CallBackController : ApiController
{
    #region GASH
    [HttpGet]
    [HttpPost]
    [ActionName("GASHNotify")]
    public HttpResponseMessage GASHNotify([FromBody] _GASHPayAsyncNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("GASH");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;
        string PAY_STATUS;
        string RCODE;
        //#region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        XmlDocument xmlDoc = new XmlDocument();
        CodingControl.TransactionKey _TransactionKey = new CodingControl.TransactionKey() { Key = providerSetting.MerchantKey, IV = providerSetting.OtherDatas[0], Password = providerSetting.OtherDatas[1] };
        string data = NotifyBody.data;
        if (!string.IsNullOrEmpty(NotifyBody.data))
        {
            xmlDoc.LoadXml(Encoding.UTF8.GetString(Convert.FromBase64String(data)));

            #region 取得PaymentID
            paymentSerial = xmlDoc.DocumentElement["COID"].InnerText;
            PAY_STATUS = xmlDoc.DocumentElement["PAY_STATUS"].InnerText;
            RCODE = xmlDoc.DocumentElement["RCODE"].InnerText;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + xmlDoc.OuterXml, 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail1");

            return response;
        }

        try
        {

            #region 簽名檢查

            if (!CodingControl.VerifyERPC(ref xmlDoc, xmlDoc.DocumentElement["ERPC"].InnerText, _TransactionKey))
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);

                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("sign fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼
            if (PAY_STATUS == "S" || PAY_STATUS == "0" || PAY_STATUS == "W" || RCODE == "9004" || RCODE == "9998" || RCODE == "2001" || RCODE == "9999")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else if (PAY_STATUS == "F" || PAY_STATUS == "T" || PAY_STATUS == "C")
            {
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("@RRN|@PAY_STATUS");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("Waiting Process");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            var _ApplyWithdrawalData = new Provider_GASH().ApplyWithdrawal(paymentModel, xmlDoc.DocumentElement["PAID"].InnerText);
            if (_ApplyWithdrawalData.IsPaymentSuccess)
            {
                switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, xmlDoc.OuterXml, "", decimal.Parse(xmlDoc.DocumentElement["AMOUNT"].InnerText), xmlDoc.DocumentElement["RRN"].InnerText))
                {
                    case 0:
                        //撈取該單，準備回傳資料
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("@RRN|@PAY_STATUS");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                        gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                        System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                        GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                        if (CompanyModel.IsProxyCallBack == 0)
                        {
                            companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                        }
                        else
                        {
                            companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                        }
                        break;
                    case -1:
                        //-1=交易單 不存在
                        PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("@RRN|@PAY_STATUS");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    case -2:
                        //-2=交易資料有誤 
                        PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("@RRN|@PAY_STATUS");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                        gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                        CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                        CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                        if (CompanyModel.IsProxyCallBack == 0)
                        {
                            companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                        }
                        else
                        {
                            companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                        }
                        break;
                    case -3:
                        //-3=供應商，交易失敗
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("@RRN|@PAY_STATUS");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                        gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                        CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                        CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                        if (CompanyModel.IsProxyCallBack == 0)
                        {
                            companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                        }
                        else
                        {
                            companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                        }
                        break;
                    case -4:
                        //-4=鎖定失敗
                        PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("@RRN|@PAY_STATUS");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    case -5:
                        //-5=加扣點失敗 
                        PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("@RRN|@PAY_STATUS");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    case -6:
                        //-6=通知廠商中 
                        PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("@RRN|@PAY_STATUS");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    case -7:
                        //-7=交易單非可修改之狀態 
                        PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("@RRN|@PAY_STATUS");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    default:
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("@RRN|@PAY_STATUS");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        break;
                }
            }
            else {
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("Settle Fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    #endregion

    #region Nissin
    [HttpGet]
    [HttpPost]
    [ActionName("NissinNotify")]
    public HttpResponseMessage NissinNotify([FromBody] _NissinPayAsyncNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("Nissin");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.merchantOrderNo;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();
            
            signDic.Add("merchantCode", providerSetting.MerchantCode);//
            signDic.Add("signType", NotifyBody.signType);//
            signDic.Add("code", NotifyBody.code);//
            signDic.Add("message", NotifyBody.message);//
            signDic.Add("merchantOrderNo", NotifyBody.merchantOrderNo);//
            signDic.Add("platformOrderNo", NotifyBody.platformOrderNo);//
            signDic.Add("orderAmount", NotifyBody.orderAmount);//
            signDic.Add("actualAmount", NotifyBody.actualAmount);//
            signDic.Add("actualFee", NotifyBody.actualFee);//
            signDic.Add("orderStatus", NotifyBody.orderStatus);//
            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Value;
            }

            signStr = signStr + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.code.ToUpper() == "SUCCESS"&& NotifyBody.orderStatus.ToUpper() == "COMPLETED")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.actualAmount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.actualAmount), NotifyBody.platformOrderNo))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("NissinWithdrawNotify")]
    public HttpResponseMessage NissinWithdrawNotify([FromBody] _NissinPayAsyncNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("Nissin");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.merchantOrderNo;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchantCode", providerSetting.MerchantCode);//
            signDic.Add("signType", NotifyBody.signType);//
            signDic.Add("code", NotifyBody.code);//
            signDic.Add("message", NotifyBody.message);//
            signDic.Add("merchantOrderNo", NotifyBody.merchantOrderNo);//
            signDic.Add("platformOrderNo", NotifyBody.platformOrderNo);//
            signDic.Add("orderAmount", NotifyBody.orderAmount);//
            signDic.Add("actualAmount", NotifyBody.actualAmount);//
            signDic.Add("actualFee", NotifyBody.actualFee);//
            signDic.Add("orderStatus", NotifyBody.orderStatus);//
            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Value;
            }

            signStr = signStr + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.code.ToUpper() == "SUCCESS")
            {
                if (NotifyBody.orderStatus.ToUpper() == "COMPLETED")
                {//成功
                    if (withdrawalModel.Amount == decimal.Parse(NotifyBody.actualAmount))
                    {
                        withdrawSuccess = true;
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("Amount Error");
                        return response;
                    }
                }
                else if (NotifyBody.orderStatus.ToUpper() == "CANCELED")
                {
                    withdrawSuccess = false;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("WaitingProcess");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
            }
            else {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
          
            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.platformOrderNo, decimal.Parse(NotifyBody.actualAmount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.platformOrderNo, decimal.Parse(NotifyBody.actualAmount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }


    [HttpGet]
    [HttpPost]
    [ActionName("NissinWithdrawCheck")]
    public HttpResponseMessage NissinWithdrawCheck(_NissinWithdrawCheckBody NotifyBody)
    {
        var response = Request.CreateResponse(HttpStatusCode.OK);
        JObject jobj = new JObject();
        System.Data.DataTable DT;
   
        if (string.IsNullOrEmpty(NotifyBody.orderNo))
        {
            jobj["status"] = "false";
            jobj["msg"] = "merchantNo not exist";
        }
        else
        {
            DT= PayDB.GetWithdrawalByWithdrawID(NotifyBody.orderNo);
            if (DT != null && DT.Rows.Count > 0)
            {
                if (DT.Rows[0]["Status"].ToString() == "0" || DT.Rows[0]["Status"].ToString() == "1")
                {
                    jobj["status"] = "true";
                    jobj["msg"] = "order exist";
                }
                else {
                    jobj["status"] = "false";
                    jobj["msg"] = "order not exist";
                }
            }
            else {
                jobj["status"] = "false";
                jobj["msg"] = "order not exist";
            }
        }

        HttpContext.Current.Response.Write(jobj.ToString());
        HttpContext.Current.Response.Flush();
        HttpContext.Current.Response.End();

        return response;
    }
    #endregion

    #region HeroPay
    [HttpGet]
    [HttpPost]
    [ActionName("HeroPayNotify")]
    public HttpResponseMessage HeroPayNotify(_HeroPayAsyncNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("HeroPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得PaymentID
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
            paymentSerial = NotifyBody.mchOrderNo;
            #endregion

        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();
            signDic.Add("payOrderId", NotifyBody.payOrderId);//
            signDic.Add("mchId", providerSetting.MerchantCode);//
            signDic.Add("appId", NotifyBody.appId);//
            signDic.Add("productId", NotifyBody.productId);//
            signDic.Add("mchOrderNo", NotifyBody.mchOrderNo);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("paySuccTime", NotifyBody.paySuccTime);//
            signDic.Add("backType", NotifyBody.backType);//
            if (!string.IsNullOrEmpty(NotifyBody.channelOrderNo))
            {
                signDic.Add("channelOrderNo", NotifyBody.channelOrderNo);//
            }

            if (!string.IsNullOrEmpty(NotifyBody.channelAttach))
            {
                signDic.Add("channelAttach", NotifyBody.channelAttach);//
            }

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr += "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetPaymentByPaymentID(paymentSerial);

            if (DT != null && DT.Rows.Count > 0)
            {

                if (NotifyBody.status == "2")
                {
                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.amount)/100)
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("success");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount)/100, ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("HeroPayWithdrawNotify")]
    public HttpResponseMessage HeroPayWithdrawNotify(string transOrderId,string mchId,string mchTransOrderNo,string amount,string status,string channelOrderNo,string transSuccTime,string backType,string sign)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("HeroPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;
        _HeroPayWithdrawAsyncNotifyBody NotifyBody = new _HeroPayWithdrawAsyncNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }

       
        #endregion

        if (!string.IsNullOrEmpty(mchTransOrderNo))
        {
            #region 取得withdrawSerial
        
            NotifyBody.amount = amount;
            NotifyBody.backType = backType;
            NotifyBody.channelOrderNo = channelOrderNo;
            NotifyBody.mchId = mchId;
            NotifyBody.mchTransOrderNo = mchTransOrderNo;
            NotifyBody.sign = sign;
            NotifyBody.status = status;
            NotifyBody.transOrderId = transOrderId;
            NotifyBody.transSuccTime = transSuccTime;
        
            withdrawSerial = NotifyBody.mchTransOrderNo;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("backType", NotifyBody.backType);//
            signDic.Add("mchId", providerSetting.MerchantCode);//
       
            signDic.Add("mchTransOrderNo", NotifyBody.mchTransOrderNo);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("transOrderId", NotifyBody.transOrderId);//
            signDic.Add("transSuccTime", NotifyBody.transSuccTime);//

            if (!string.IsNullOrEmpty(NotifyBody.channelOrderNo))
            {
                signDic.Add("channelOrderNo", NotifyBody.channelOrderNo);//
            }

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr += "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetWithdrawalByWithdrawID(withdrawSerial);

            if (DT != null && DT.Rows.Count > 0)
            {
                if (NotifyBody.status == "2"|| NotifyBody.status == "3")
                {
                    withdrawalModel = GatewayCommon.ToList<GatewayCommon.Withdrawal>(DT).FirstOrDefault();
                    if (withdrawalModel.Amount != decimal.Parse(NotifyBody.amount)/100)
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.WithdrawalByProvider providerRequestData = new GatewayCommon.WithdrawalByProvider();
                    providerRequestData = GatewayCommon.QueryWithdrawalByProvider(withdrawalModel);

                    if (providerRequestData.IsQuerySuccess)
                    {
                        if (providerRequestData.WithdrawalStatus == 0)
                        {
                            withdrawSuccess = true;
                        }
                        else if (providerRequestData.WithdrawalStatus == 1)
                        {
                            withdrawSuccess = false;
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                            response = Request.CreateResponse(HttpStatusCode.OK);
                            HttpContext.Current.Response.Write("WaitingProcess");
                            //HttpContext.Current.Response.Flush();
                            //HttpContext.Current.Response.End();
                            return response;
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,查詢訂單失敗", 4, withdrawSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("WaitingProcess");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("WaitingProcess");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 4, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("success");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount)/100, withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount)/100, 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("success");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    #region AsiaPlay888
    [HttpGet]
    [HttpPost]
    [ActionName("AsiaPlay888Notify")]
    public HttpResponseMessage AsiaPlay888Notify()
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("AsiaPlay888");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);


        _AsiaPlay888AsyncNotifyBody NotifyBody = JsonConvert.DeserializeObject<_AsiaPlay888AsyncNotifyBody>(strBody);
        if (NotifyBody != null)
        {
            #region 取得PaymentID
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + strBody, 3, paymentSerial, providerSetting.ProviderCode);
            paymentSerial = NotifyBody.OperatorOrderNo;
            #endregion

        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            //#region 簽名檢查

            //string signStr = "";
            //Dictionary<string, string> signDic = new Dictionary<string, string>();

            //signDic.Add("merchantCode", providerSetting.MerchantCode);//
            //signDic.Add("signType", NotifyBody.signType);//
            //signDic.Add("code", NotifyBody.code);//
            //signDic.Add("message", NotifyBody.message);//
            //signDic.Add("merchantOrderNo", NotifyBody.merchantOrderNo);//
            //signDic.Add("platformOrderNo", NotifyBody.platformOrderNo);//
            //signDic.Add("orderAmount", NotifyBody.orderAmount);//
            //signDic.Add("actualAmount", NotifyBody.actualAmount);//
            //signDic.Add("actualFee", NotifyBody.actualFee);//
            //signDic.Add("orderStatus", NotifyBody.orderStatus);//
            //signDic = CodingControl.AsciiDictionary(signDic);

            //foreach (KeyValuePair<string, string> item in signDic)
            //{
            //    signStr += item.Value;
            //}

            //signStr = signStr + providerSetting.MerchantKey;

            //sign = CodingControl.GetMD5(signStr, false);

            //if (sign != NotifyBody.sign)
            //{
            //    PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
            //    response = Request.CreateResponse(HttpStatusCode.OK);
            //    //HttpContext.Current.Response.Flush();
            //    //HttpContext.Current.Response.End();

            //    return response;
            //}

            //#endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetPaymentByPaymentID(paymentSerial);

            if (DT != null && DT.Rows.Count > 0)
            {

                if (NotifyBody.Status == "1")
                {
                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.Amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("success");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.Amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("AsiaPlay888WithdrawNotify")]
    public HttpResponseMessage AsiaPlay888WithdrawNotify()
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("AsiaPlay888");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion
        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);
        _AsiaPlay888WithdrawalAsyncNotifyBody NotifyBody = JsonConvert.DeserializeObject<_AsiaPlay888WithdrawalAsyncNotifyBody>(strBody);
        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.OperatorOrderNo;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 轉換status代碼
            
            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetWithdrawalByWithdrawID(withdrawSerial);

            if (DT != null && DT.Rows.Count > 0)
            {
                if (NotifyBody.Status == "1" || NotifyBody.Status == "2")
                {
                    withdrawalModel = GatewayCommon.ToList<GatewayCommon.Withdrawal>(DT).FirstOrDefault();
                    if (withdrawalModel.Amount != decimal.Parse(NotifyBody.Amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.WithdrawalByProvider providerRequestData = new GatewayCommon.WithdrawalByProvider();
                    providerRequestData = GatewayCommon.QueryWithdrawalByProvider(withdrawalModel);

                    if (providerRequestData.IsQuerySuccess)
                    {
                        if (providerRequestData.WithdrawalStatus == 0)
                        {
                            withdrawSuccess = true;
                        }
                        else if (providerRequestData.WithdrawalStatus == 1)
                        {
                            withdrawSuccess = false;
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                            response = Request.CreateResponse(HttpStatusCode.OK);
                            HttpContext.Current.Response.Write("WaitingProcess");
                            //HttpContext.Current.Response.Flush();
                            //HttpContext.Current.Response.End();
                            return response;
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,查詢訂單失敗", 4, withdrawSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("WaitingProcess");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("WaitingProcess");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
               
            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 4, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("success");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.Amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.Amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("success");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    #region coolemons
    [HttpGet]
    [HttpPost]
    [ActionName("coolemonsNotify")]
    public HttpResponseMessage coolemonsNotify([FromBody] _coolemonsPayAsyncNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("coolemons");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.orderNo;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, object> signDic = new Dictionary<string, object>();

            signDic.Add("status", NotifyBody.status);
            signDic.Add("tradeNo", NotifyBody.tradeNo);
            signDic.Add("orderNo", NotifyBody.orderNo);
            signDic.Add("userNo", NotifyBody.userNo);
            signDic.Add("userName", NotifyBody.userName);
            signDic.Add("channelNo",NotifyBody.channelNo);
            signDic.Add("amount", NotifyBody.amount);
            signDic.Add("discount", NotifyBody.discount);
            signDic.Add("lucky", NotifyBody.lucky);
            signDic.Add("paid", NotifyBody.paid); 
            signDic.Add("extra", NotifyBody.extra);

            signDic = CodingControl.AsciiDictionary2(signDic);

            foreach (KeyValuePair<string, object> item in signDic)
            {
                if (item.Key != "userName" && item.Key != "channelNo" && item.Key != "payeeName" && item.Key != "bankName" && item.Key != "appSecret" && item.Key != "exchangeRate")
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }
            signStr = signStr.Substring(0, signStr.Length - 1);
            signStr = signStr + providerSetting.MerchantKey;

            sign = CodingControl.GetSHA256(signStr, false);
            sign = CodingControl.GetMD5(sign, false).ToUpper();

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status == "PAID"|| NotifyBody.status == "MANUAL PAID")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion
            if (paymentModel.OrderAmount!=decimal.Parse(NotifyBody.paid))
            {
                PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("amount error");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.paid), NotifyBody.tradeNo))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("error");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("error");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("error");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("error");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("error");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("error");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("error");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("coolemonsWithdrawNotify")]
    public HttpResponseMessage coolemonsWithdrawNotify([FromBody] _coolemonsPayWithdrawAsyncNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("coolemons");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.orderNo;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, object> signDic = new Dictionary<string, object>();

            signDic.Add("status", NotifyBody.status);
            signDic.Add("tradeNo", NotifyBody.tradeNo);
            signDic.Add("orderNo", NotifyBody.orderNo);
            signDic.Add("amount", NotifyBody.amount);
            signDic.Add("exchangeRate", NotifyBody.exchangeRate);
            signDic.Add("name", NotifyBody.name);
            signDic.Add("bankName", NotifyBody.bankName);
            signDic.Add("bankAccount", NotifyBody.bankAccount);
            signDic.Add("bankBranch", NotifyBody.bankBranch);
            signDic.Add("memo", NotifyBody.memo);
            signDic.Add("mobile", NotifyBody.mobile);
            signDic.Add("fee", NotifyBody.fee);
            signDic.Add("extra", NotifyBody.extra);

            signDic = CodingControl.AsciiDictionary2(signDic);

            foreach (KeyValuePair<string, object> item in signDic)
            {
                if (item.Key != "bankBranch" && item.Key != "memo" && item.Key != "exchangerate" && item.Key != "appSecret" && item.Key != "exchangeRate")
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }
            signStr = signStr.Substring(0, signStr.Length - 1);
            signStr = signStr + providerSetting.MerchantKey;

            sign = CodingControl.GetSHA256(signStr, false);
            sign = CodingControl.GetMD5(sign, false).ToUpper();

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            if (NotifyBody.status.ToUpper() == "PAID")
            {//成功
                withdrawSuccess = true;
            }
            else if (NotifyBody.status.ToUpper() == "CANCELLED")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.tradeNo, decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.tradeNo, decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("success");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    #region TigerPay
    [HttpGet]
    [HttpPost]
    [ActionName("TigerPayNotify")]
    public HttpResponseMessage TigerPayNotify()
    {
 
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("TigerPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";

    
        //#region 回调IP检查
        //string ProxyIP = CodingControl.GetUserIP();
        //if (!providerSetting.ProviderIP.Contains(ProxyIP))
        //{
        //    PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
        //    response = Request.CreateResponse(HttpStatusCode.OK);
        //    HttpContext.Current.Response.Write("fail");
        //    //HttpContext.Current.Response.Flush();
        //    //HttpContext.Current.Response.End();
        //    return response;
        //}
        //#endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);
        if (string.IsNullOrEmpty(strBody))
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误: strBody empty", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        _TigerPayAsyncNotifyBody NotifyBody = JsonConvert.DeserializeObject<_TigerPayAsyncNotifyBody>(strBody);

   
        #region 取得PaymentID

        paymentSerial = NotifyBody.Free;
        #endregion
        PayDB.InsertPaymentTransferLog("供应商完成订单通知" + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
     

        try
        {

            #region 轉換status代碼

            if (NotifyBody.result == "0")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), NotifyBody.transaction_number))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("0");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("0");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("0");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("0");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("0");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("0");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("0");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("0");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("0");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    //[HttpGet]
    //[HttpPost]
    //[ActionName("NissinWithdrawNotify")]
    //public HttpResponseMessage NissinWithdrawNotify([FromBody] _NissinPayAsyncNotifyBody NotifyBody)
    //{

    //    GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("Nissin");
    //    bool withdrawSuccess = false;
    //    string withdrawSerial = "";
    //    string sign = "";
    //    Dictionary<string, string> sendDic = new Dictionary<string, string>();
    //    GatewayCommon.Withdrawal withdrawalModel;
    //    GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
    //    HttpResponseMessage response;

    //    #region 回调IP检查
    //    string ProxyIP = CodingControl.GetUserIP();
    //    if (!providerSetting.ProviderIP.Contains(ProxyIP))
    //    {
    //        PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
    //        response = Request.CreateResponse(HttpStatusCode.OK);
    //        HttpContext.Current.Response.Write("fail");
    //        //HttpContext.Current.Response.Flush();
    //        //HttpContext.Current.Response.End();
    //        return response;
    //    }
    //    #endregion

    //    if (NotifyBody != null)
    //    {
    //        #region 取得withdrawSerial
    //        withdrawSerial = NotifyBody.merchantOrderNo;
    //        #endregion
    //        PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
    //    }
    //    else
    //    {
    //        PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
    //    }



    //    try
    //    {

    //        #region 簽名檢查

    //        string signStr = "";
    //        Dictionary<string, string> signDic = new Dictionary<string, string>();

    //        signDic.Add("merchantCode", providerSetting.MerchantCode);//
    //        signDic.Add("signType", NotifyBody.signType);//
    //        signDic.Add("code", NotifyBody.code);//
    //        signDic.Add("message", NotifyBody.message);//
    //        signDic.Add("merchantOrderNo", NotifyBody.merchantOrderNo);//
    //        signDic.Add("platformOrderNo", NotifyBody.platformOrderNo);//
    //        signDic.Add("orderAmount", NotifyBody.orderAmount);//
    //        signDic.Add("actualAmount", NotifyBody.actualAmount);//
    //        signDic.Add("actualFee", NotifyBody.actualFee);//
    //        signDic.Add("orderStatus", NotifyBody.orderStatus);//
    //        signDic = CodingControl.AsciiDictionary(signDic);

    //        foreach (KeyValuePair<string, string> item in signDic)
    //        {
    //            signStr += item.Value;
    //        }

    //        signStr = signStr + providerSetting.MerchantKey;

    //        sign = CodingControl.GetMD5(signStr, false);

    //        if (sign != NotifyBody.sign)
    //        {
    //            PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
    //            response = Request.CreateResponse(HttpStatusCode.OK);
    //            //HttpContext.Current.Response.Flush();
    //            //HttpContext.Current.Response.End();

    //            return response;
    //        }

    //        #endregion

    //        #region 轉換status代碼
    //        if (NotifyBody.code.ToUpper() == "SUCCESS")
    //        {
    //            if (NotifyBody.orderStatus.ToUpper() == "COMPLETED")
    //            {//成功
    //                withdrawSuccess = true;
    //            }
    //            else if (NotifyBody.orderStatus.ToUpper() == "CANCELED")
    //            {
    //                withdrawSuccess = false;
    //            }
    //            else
    //            {
    //                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
    //                response = Request.CreateResponse(HttpStatusCode.OK);
    //                HttpContext.Current.Response.Write("WaitingProcess");
    //                //HttpContext.Current.Response.Flush();
    //                //HttpContext.Current.Response.End();
    //                return response;
    //            }
    //        }
    //        else
    //        {
    //            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
    //            response = Request.CreateResponse(HttpStatusCode.OK);
    //            HttpContext.Current.Response.Write("WaitingProcess");
    //            //HttpContext.Current.Response.Flush();
    //            //HttpContext.Current.Response.End();
    //            return response;
    //        }

    //        #endregion

    //        withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();

    //        if (withdrawalModel != null)
    //        {   //代付
    //            GatewayCommon.WithdrawResultStatus returnStatus;

    //            if (withdrawSuccess)
    //            {
    //                //2代表已成功且扣除額度,避免重複上分
    //                if (withdrawalModel.UpStatus != 2)
    //                {
    //                    //不修改Withdraw之狀態，預存中調整
    //                    PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.platformOrderNo, decimal.Parse(NotifyBody.actualAmount), withdrawSerial);
    //                    var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
    //                    switch (intReviewWithdrawal)
    //                    {
    //                        case 0:
    //                            PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
    //                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
    //                            break;
    //                        default:
    //                            //調整訂單為系統失敗單
    //                            PayDB.UpdateWithdrawStatus(14, withdrawSerial);
    //                            PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
    //                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
    //                            break;
    //                    }
    //                }
    //                else
    //                {

    //                    if (withdrawalModel.Status == 2)
    //                    {
    //                        returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
    //                    }
    //                    else if (withdrawalModel.Status == 3)
    //                    {
    //                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
    //                    }
    //                    else if (withdrawalModel.Status == 14)
    //                    {
    //                        returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
    //                    }
    //                    else
    //                    {
    //                        returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
    //                    }
    //                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

    //                }
    //            }
    //            else
    //            {
    //                if (withdrawalModel.UpStatus != 2)
    //                {
    //                    PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
    //                    PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.platformOrderNo, decimal.Parse(NotifyBody.actualAmount), 3, withdrawSerial);
    //                    returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
    //                }
    //                else
    //                {
    //                    if (withdrawalModel.Status == 2)
    //                    {
    //                        returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
    //                    }
    //                    else if (withdrawalModel.Status == 3)
    //                    {
    //                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
    //                    }
    //                    else if (withdrawalModel.Status == 14)
    //                    {
    //                        returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
    //                    }
    //                    else
    //                    {
    //                        returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
    //                    }
    //                    PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

    //                }
    //            }

    //            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
    //            //取得傳送資料
    //            gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
    //            //發送API 回傳商戶
    //            if (withdrawalModel.FloatType != 0)
    //            {
    //                System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
    //                GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
    //                //發送三次回調(後台手動發款後用)
    //                if (CompanyModel.IsProxyCallBack == 0)
    //                {
    //                    //發送一次回調 補單用
    //                    if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
    //                    {
    //                        //修改下游狀態
    //                        PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
    //                    }
    //                }
    //                else
    //                {
    //                    //發送一次回調 補單用
    //                    if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
    //                    {
    //                        //修改下游狀態
    //                        PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
    //                    }
    //                }
    //            }

    //        }

    //        response = Request.CreateResponse(HttpStatusCode.OK);

    //        HttpContext.Current.Response.Write("SUCCESS");
    //        //HttpContext.Current.Response.Flush();
    //        //HttpContext.Current.Response.End();
    //    }
    //    catch (Exception ex)
    //    {
    //        PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
    //        response = Request.CreateResponse(HttpStatusCode.OK);
    //        HttpContext.Current.Response.Write("fail");
    //        //HttpContext.Current.Response.Flush();
    //        //HttpContext.Current.Response.End();
    //        throw;
    //    }
    //    finally
    //    {

    //    }

    //    return response;
    //}


    //[HttpGet]
    //[HttpPost]
    //[ActionName("NissinWithdrawCheck")]
    //public HttpResponseMessage NissinWithdrawCheck(_NissinWithdrawCheckBody NotifyBody)
    //{
    //    var response = Request.CreateResponse(HttpStatusCode.OK);
    //    JObject jobj = new JObject();
    //    System.Data.DataTable DT;

    //    if (string.IsNullOrEmpty(NotifyBody.orderNo))
    //    {
    //        jobj["status"] = "false";
    //        jobj["msg"] = "merchantNo not exist";
    //    }
    //    else
    //    {
    //        DT = PayDB.GetWithdrawalByWithdrawID(NotifyBody.orderNo);
    //        if (DT != null && DT.Rows.Count > 0)
    //        {
    //            if (DT.Rows[0]["Status"].ToString() == "0" || DT.Rows[0]["Status"].ToString() == "1")
    //            {
    //                jobj["status"] = "true";
    //                jobj["msg"] = "order exist";
    //            }
    //            else
    //            {
    //                jobj["status"] = "false";
    //                jobj["msg"] = "order not exist";
    //            }
    //        }
    //        else
    //        {
    //            jobj["status"] = "false";
    //            jobj["msg"] = "order not exist";
    //        }
    //    }

    //    HttpContext.Current.Response.Write(jobj.ToString());
    //    HttpContext.Current.Response.Flush();
    //    HttpContext.Current.Response.End();

    //    return response;
    //}
    #endregion

    #region DiDiPay
    [HttpGet]
    [HttpPost]
    [ActionName("DiDiPayNotify")]
    public HttpResponseMessage DiDiPayNotify()
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("DiDiPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;
        _DiDiPayNotifyBody NotifyBody = new _DiDiPayNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);
   
        if (string.IsNullOrEmpty(strBody))
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误: strBody empty", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        var boundary = HttpContext.Current.Request.ContentType;

        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);
        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                } 
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status== "5")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("DiDiPayWithdrawNotify")]
    public HttpResponseMessage DiDiPayWithdrawNotify()
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("DiDiPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;
        _DiDiPayNotifyBody NotifyBody = new _DiDiPayNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);

        if (string.IsNullOrEmpty(strBody))
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误: strBody empty", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        var boundary = HttpContext.Current.Request.ContentType;

        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);
        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                }
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.status.ToUpper() == "5")
            {//成功
                if (withdrawalModel.Amount == decimal.Parse(NotifyBody.amount))
                {
                    withdrawSuccess = true;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("Amount Error");
                    return response;
                }
            }
            else if (NotifyBody.status.ToUpper() == "3")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
         

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }

    #endregion

    #region DiDiPay2
    [HttpGet]
    [HttpPost]
    [ActionName("DiDiPay2Notify")]
    public HttpResponseMessage DiDiPay2Notify()
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("DiDiPay2");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;
        _DiDiPayNotifyBody NotifyBody = new _DiDiPayNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);

        if (string.IsNullOrEmpty(strBody))
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误: strBody empty", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        var boundary = HttpContext.Current.Request.ContentType;

        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);
        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                }
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status == "5")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("DiDiPay2WithdrawNotify")]
    public HttpResponseMessage DiDiPay2WithdrawNotify()
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("DiDiPay2");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;
        _DiDiPayNotifyBody NotifyBody = new _DiDiPayNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);

        if (string.IsNullOrEmpty(strBody))
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误: strBody empty", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        var boundary = HttpContext.Current.Request.ContentType;

        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);
        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                }
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.status.ToUpper() == "5")
            {//成功
                if (withdrawalModel.Amount == decimal.Parse(NotifyBody.amount))
                {
                    withdrawSuccess = true;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("Amount Error");
                    return response;
                }
            }
            else if (NotifyBody.status.ToUpper() == "3")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }


            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }

    #endregion

    #region LUMIPay2
    [HttpGet]
    [HttpPost]
    [ActionName("LUMIPay2Notify")]
    public HttpResponseMessage LUMIPay2Notify()
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("LUMIPay2");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;
        _DiDiPayNotifyBody NotifyBody = new _DiDiPayNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);

        if (string.IsNullOrEmpty(strBody))
        {
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        var boundary = HttpContext.Current.Request.ContentType;

        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);
        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                }
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status == "5")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("LUMIPay2WithdrawNotify")]
    public HttpResponseMessage LUMIPay2WithdrawNotify()
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("LUMIPay2");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;
        _DiDiPayNotifyBody NotifyBody = new _DiDiPayNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);

        if (string.IsNullOrEmpty(strBody))
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误: strBody empty", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        var boundary = HttpContext.Current.Request.ContentType;

        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);
        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                }
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.status.ToUpper() == "5")
            {//成功
                if (withdrawalModel.Amount == decimal.Parse(NotifyBody.amount))
                {
                    withdrawSuccess = true;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("Amount Error");
                    return response;
                }
            }
            else if (NotifyBody.status.ToUpper() == "3")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }


            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }

    #endregion

    #region CPay
    [HttpGet]
    [HttpPost]
    [ActionName("CPayNotify")]
    public HttpResponseMessage CPayNotify()
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("CPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;
        _DiDiPayNotifyBody NotifyBody = new _DiDiPayNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);

        if (string.IsNullOrEmpty(strBody))
        {
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        var boundary = HttpContext.Current.Request.ContentType;

        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);
        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                }
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status == "5")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("CPayWithdrawNotify")]
    public HttpResponseMessage CPayWithdrawNotify()
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("CPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;
        _DiDiPayNotifyBody NotifyBody = new _DiDiPayNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);

        if (string.IsNullOrEmpty(strBody))
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误: strBody empty", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        var boundary = HttpContext.Current.Request.ContentType;

        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);
        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                }
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.status.ToUpper() == "5")
            {//成功
                if (withdrawalModel.Amount == decimal.Parse(NotifyBody.amount))
                {
                    withdrawSuccess = true;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("Amount Error");
                    return response;
                }
            }
            else if (NotifyBody.status.ToUpper() == "3")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }


            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }

    #endregion

    #region JBPay
    [HttpGet]
    [HttpPost]
    [ActionName("JBPayNotify")]
    public HttpResponseMessage JBPayNotify()
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("JBPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;
        _DiDiPayNotifyBody NotifyBody = new _DiDiPayNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);

        if (string.IsNullOrEmpty(strBody))
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误: strBody empty", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        var boundary = HttpContext.Current.Request.ContentType;

        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);
        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                }
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status == "5")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("JBPayWithdrawNotify")]
    public HttpResponseMessage JBPayWithdrawNotify()
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("JBPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;
        _DiDiPayNotifyBody NotifyBody = new _DiDiPayNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);

        if (string.IsNullOrEmpty(strBody))
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误: strBody empty", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        var boundary = HttpContext.Current.Request.ContentType;

        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);
        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                }
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.status.ToUpper() == "5")
            {//成功
                if (withdrawalModel.Amount == decimal.Parse(NotifyBody.amount))
                {
                    withdrawSuccess = true;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("Amount Error");
                    return response;
                }
            }
            else if (NotifyBody.status.ToUpper() == "3")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }


            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }

    #endregion

    #region YuHong
    [HttpGet]
    [HttpPost]
    [ActionName("YuHongNotify")]
    public HttpResponseMessage YuHongNotify([FromBody] _YuHongNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("YuHong");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.mchOrderNo;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("mchId", providerSetting.MerchantCode);//
            signDic.Add("payOrderId", NotifyBody.payOrderId);//
            signDic.Add("appId", NotifyBody.appId);//
            signDic.Add("productId", NotifyBody.productId);//
            signDic.Add("mchOrderNo", NotifyBody.mchOrderNo);//

            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("income", NotifyBody.income);//
            signDic.Add("payer", NotifyBody.payer);//
            signDic.Add("status", NotifyBody.status);//

            signDic.Add("channelOrderNo", NotifyBody.channelOrderNo);//
            signDic.Add("param1", NotifyBody.param1);//
            signDic.Add("param2", NotifyBody.param2);//
            signDic.Add("paySuccTime", NotifyBody.paySuccTime);//
            signDic.Add("backType", NotifyBody.backType);//
            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status == "2")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.amount) / 100)
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount) / 100, NotifyBody.channelOrderNo))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("YuHongWithdrawNotify")]
    public HttpResponseMessage YuHongWithdrawNotify([FromBody] _YuHongWithdrawNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("YuHong");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.mchOrderNo;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("mchId", providerSetting.MerchantCode);//
            signDic.Add("mchOrderNo", NotifyBody.mchOrderNo);//
            signDic.Add("agentpayOrderId", NotifyBody.agentpayOrderId);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("fee", NotifyBody.fee);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("transMsg", NotifyBody.transMsg);//
            signDic.Add("extra", NotifyBody.extra);//
  

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.status.ToUpper() == "2")
            {//成功
                
                if (withdrawalModel.Amount == decimal.Parse(NotifyBody.amount) / 100)
                {
                    withdrawSuccess = true;
                }
                else {
                    PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("Amount Error");
                    return response;
                }
              
            }
            else if (NotifyBody.status.ToUpper() == "3")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }


            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount) / 100, withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }

    #endregion

    #region _FeibaoPay
    [HttpGet]
    [HttpPost]
    [ActionName("FeibaoPayNotify")]
    public HttpResponseMessage FeibaoPayNotify([FromBody] _FeibaoPayNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("FeibaoPay");
        _FeibaoPayOrderData FeibaoPayOrderData;
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {   
            if (NotifyBody.code == "0")
            {
                string jsonStrData = CodingControl.DecryptStringFeibao(NotifyBody.order, providerSetting.MerchantKey, providerSetting.OtherDatas[0]);
             
                FeibaoPayOrderData = JsonConvert.DeserializeObject<_FeibaoPayOrderData>(jsonStrData);
            }
            else {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");

                return response;
            }

            #region 取得PaymentID

            paymentSerial = FeibaoPayOrderData.merchant_order_num;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(FeibaoPayOrderData), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 轉換status代碼

            if (FeibaoPayOrderData.status.ToLower() == "success"|| FeibaoPayOrderData.status.ToLower() == "success_done")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(FeibaoPayOrderData.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(FeibaoPayOrderData), "", decimal.Parse(FeibaoPayOrderData.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("FeibaoPayWithdrawNotify")]
    public HttpResponseMessage FeibaoPayWithdrawNotify([FromBody] _FeibaoPayNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("FeibaoPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;
        _FeibaoPayOrderData FeibaoPayOrderData=new _FeibaoPayOrderData();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            if (NotifyBody.code == "0")
            {
                string jsonStrData = CodingControl.DecryptStringFeibao(NotifyBody.order, providerSetting.MerchantKey, providerSetting.OtherDatas[0]);

                FeibaoPayOrderData = JsonConvert.DeserializeObject<_FeibaoPayOrderData>(jsonStrData);
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");

                return response;
            }

            #region 取得PaymentID

            withdrawSerial = FeibaoPayOrderData.merchant_order_num;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        try
        {

  
            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.code.ToUpper() == "0")
            {
                if (FeibaoPayOrderData.status.ToLower() == "success" || FeibaoPayOrderData.status.ToLower() == "success_done")
                {//成功
                    if (withdrawalModel.Amount == decimal.Parse(FeibaoPayOrderData.amount))
                    {
                        withdrawSuccess = true;
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("Amount Error");
                        return response;
                    }
                }
                else if (FeibaoPayOrderData.status.ToLower() == "fail"|| FeibaoPayOrderData.status.ToLower() == "fail_done")
                {
                    withdrawSuccess = false;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("WaitingProcess");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(FeibaoPayOrderData.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(FeibaoPayOrderData.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    #region _FeibaoPayPaymaya
    [HttpGet]
    [HttpPost]
    [ActionName("FeibaoPayPaymayaNotify")]
    public HttpResponseMessage FeibaoPayPaymayaNotify([FromBody] _FeibaoPayNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("FeibaoPayPaymaya");
        _FeibaoPayOrderData FeibaoPayOrderData;
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            if (NotifyBody.code == "0")
            {
                string jsonStrData = CodingControl.DecryptStringFeibao(NotifyBody.order, providerSetting.MerchantKey, providerSetting.OtherDatas[0]);

                FeibaoPayOrderData = JsonConvert.DeserializeObject<_FeibaoPayOrderData>(jsonStrData);
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");

                return response;
            }

            #region 取得PaymentID

            paymentSerial = FeibaoPayOrderData.merchant_order_num;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + "" + ",资料:" + JsonConvert.SerializeObject(FeibaoPayOrderData), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 轉換status代碼

            if (FeibaoPayOrderData.status.ToLower() == "success" || FeibaoPayOrderData.status.ToLower() == "success_done")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(FeibaoPayOrderData.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(FeibaoPayOrderData), "", decimal.Parse(FeibaoPayOrderData.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("FeibaoPayPaymayaWithdrawNotify")]
    public HttpResponseMessage FeibaoPayPaymayaWithdrawNotify([FromBody] _FeibaoPayNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("FeibaoPayPaymaya");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;
        _FeibaoPayOrderData FeibaoPayOrderData = new _FeibaoPayOrderData();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            if (NotifyBody.code == "0")
            {
                string jsonStrData = CodingControl.DecryptStringFeibao(NotifyBody.order, providerSetting.MerchantKey, providerSetting.OtherDatas[0]);

                FeibaoPayOrderData = JsonConvert.DeserializeObject<_FeibaoPayOrderData>(jsonStrData);
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");

                return response;
            }

            #region 取得PaymentID

            withdrawSerial = FeibaoPayOrderData.merchant_order_num;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        try
        {


            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.code.ToUpper() == "0")
            {
                if (FeibaoPayOrderData.status.ToLower() == "success" || FeibaoPayOrderData.status.ToLower() == "success_done")
                {//成功
                    if (withdrawalModel.Amount == decimal.Parse(FeibaoPayOrderData.amount))
                    {
                        withdrawSuccess = true;
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("Amount Error");
                        return response;
                    }
                }
                else if (FeibaoPayOrderData.status.ToLower() == "fail" || FeibaoPayOrderData.status.ToLower() == "fail_done")
                {
                    withdrawSuccess = false;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("WaitingProcess");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(FeibaoPayOrderData.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(FeibaoPayOrderData.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    #region FeibaoPayGrabpay
    [HttpGet]
    [HttpPost]
    [ActionName("FeibaoPayGrabpayNotify")]
    public HttpResponseMessage FeibaoPayGrabpayNotify([FromBody] _FeibaoPayNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("FeibaoPayGrabpay");
        _FeibaoPayOrderData FeibaoPayOrderData;
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            if (NotifyBody.code == "0")
            {
                string jsonStrData = CodingControl.DecryptStringFeibao(NotifyBody.order, providerSetting.MerchantKey, providerSetting.OtherDatas[0]);

                FeibaoPayOrderData = JsonConvert.DeserializeObject<_FeibaoPayOrderData>(jsonStrData);
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");

                return response;
            }

            #region 取得PaymentID

            paymentSerial = FeibaoPayOrderData.merchant_order_num;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + "" + ",资料:" + JsonConvert.SerializeObject(FeibaoPayOrderData), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 轉換status代碼

            if (FeibaoPayOrderData.status.ToLower() == "success" || FeibaoPayOrderData.status.ToLower() == "success_done")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(FeibaoPayOrderData.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(FeibaoPayOrderData), "", decimal.Parse(FeibaoPayOrderData.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("FeibaoPayGrabpayWithdrawNotify")]
    public HttpResponseMessage FeibaoPayGrabpayWithdrawNotify([FromBody] _FeibaoPayNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("FeibaoPayGrabpay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;
        _FeibaoPayOrderData FeibaoPayOrderData = new _FeibaoPayOrderData();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            if (NotifyBody.code == "0")
            {
                string jsonStrData = CodingControl.DecryptStringFeibao(NotifyBody.order, providerSetting.MerchantKey, providerSetting.OtherDatas[0]);

                FeibaoPayOrderData = JsonConvert.DeserializeObject<_FeibaoPayOrderData>(jsonStrData);
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");

                return response;
            }

            #region 取得PaymentID

            withdrawSerial = FeibaoPayOrderData.merchant_order_num;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        try
        {


            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.code.ToUpper() == "0")
            {
                if (FeibaoPayOrderData.status.ToLower() == "success" || FeibaoPayOrderData.status.ToLower() == "success_done")
                {//成功
                    if (withdrawalModel.Amount == decimal.Parse(FeibaoPayOrderData.amount))
                    {
                        withdrawSuccess = true;
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("Amount Error");
                        return response;
                    }
                }
                else if (FeibaoPayOrderData.status.ToLower() == "fail" || FeibaoPayOrderData.status.ToLower() == "fail_done")
                {
                    withdrawSuccess = false;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("WaitingProcess");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(FeibaoPayOrderData.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(FeibaoPayOrderData.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    #region FeibaoPayBank
    [HttpGet]
    [HttpPost]
    [ActionName("FeibaoPayBankNotify")]
    public HttpResponseMessage FeibaoPayBankNotify([FromBody] _FeibaoPayNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("FeibaoPayBank");
        _FeibaoPayOrderData FeibaoPayOrderData;
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            if (NotifyBody.code == "0")
            {
                string jsonStrData = CodingControl.DecryptStringFeibao(NotifyBody.order, providerSetting.MerchantKey, providerSetting.OtherDatas[0]);

                FeibaoPayOrderData = JsonConvert.DeserializeObject<_FeibaoPayOrderData>(jsonStrData);
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");

                return response;
            }

            #region 取得PaymentID

            paymentSerial = FeibaoPayOrderData.merchant_order_num;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + "" + ",资料:" + JsonConvert.SerializeObject(FeibaoPayOrderData), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 轉換status代碼

            if (FeibaoPayOrderData.status.ToLower() == "success" || FeibaoPayOrderData.status.ToLower() == "success_done")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(FeibaoPayOrderData.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(FeibaoPayOrderData), "", decimal.Parse(FeibaoPayOrderData.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("FeibaoPayBankWithdrawNotify")]
    public HttpResponseMessage FeibaoPayBankWithdrawNotify([FromBody] _FeibaoPayNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("FeibaoPayBank");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;
        _FeibaoPayOrderData FeibaoPayOrderData = new _FeibaoPayOrderData();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            if (NotifyBody.code == "0")
            {
                string jsonStrData = CodingControl.DecryptStringFeibao(NotifyBody.order, providerSetting.MerchantKey, providerSetting.OtherDatas[0]);

                FeibaoPayOrderData = JsonConvert.DeserializeObject<_FeibaoPayOrderData>(jsonStrData);
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");

                return response;
            }

            #region 取得PaymentID

            withdrawSerial = FeibaoPayOrderData.merchant_order_num;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        try
        {


            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.code.ToUpper() == "0")
            {
                if (FeibaoPayOrderData.status.ToLower() == "success" || FeibaoPayOrderData.status.ToLower() == "success_done")
                {//成功
                    if (withdrawalModel.Amount == decimal.Parse(FeibaoPayOrderData.amount))
                    {
                        withdrawSuccess = true;
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("Amount Error");
                        return response;
                    }
                }
                else if (FeibaoPayOrderData.status.ToLower() == "fail" || FeibaoPayOrderData.status.ToLower() == "fail_done")
                {
                    withdrawSuccess = false;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("WaitingProcess");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(FeibaoPayOrderData.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(FeibaoPayOrderData.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    #region FIFIPay
    [HttpGet]
    [HttpPost]
    [ActionName("FIFIPayNotify")]
    public HttpResponseMessage FIFIPayNotify([FromBody] _FIFIPayNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("FIFIPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("customer_id", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("transaction_id", NotifyBody.transaction_id);//
            signDic.Add("order_amount", NotifyBody.order_amount);//
            signDic.Add("real_amount", NotifyBody.real_amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status == "30000")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.real_amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    processStatus = 2;

                    //GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    //providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    //if (providerRequestData.IsPaymentSuccess == true)
                    //{
                    //    processStatus = 2;
                    //}
                    //else
                    //{
                    //    response = Request.CreateResponse(HttpStatusCode.OK);
                    //    HttpContext.Current.Response.Write("fail");
                    //    //HttpContext.Current.Response.Flush();
                    //    //HttpContext.Current.Response.End();
                    //    return response;
                    //}
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.real_amount), NotifyBody.transaction_id))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("FIFIPayWithdrawNotify")]
    public HttpResponseMessage FIFIPayWithdrawNotify([FromBody] _FIFIPayWithdrawNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("FIFIPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("customer_id", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("datetime", NotifyBody.datetime);//
            signDic.Add("transaction_id", NotifyBody.transaction_id);//
            signDic.Add("transaction_code", NotifyBody.transaction_code);//
            signDic.Add("transaction_msg", NotifyBody.transaction_msg);//
 
            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.transaction_code.ToUpper() == "30000")
            {//成功

                if (withdrawalModel.Amount == decimal.Parse(NotifyBody.amount))
                {
                    withdrawSuccess = true;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("Amount Error");
                    return response;
                }

            }
            else if (NotifyBody.transaction_code.ToUpper() == "40000")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }


            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.transaction_id, decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("OK");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }

    #endregion

    #region GCPay
    [HttpGet]
    [HttpPost]
    [ActionName("GCPayPayNotify")]
    public HttpResponseMessage GCPayPayNotify([FromBody] _FIFIPayNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("GCPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("customer_id", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("transaction_id", NotifyBody.transaction_id);//
            signDic.Add("order_amount", NotifyBody.order_amount);//
            signDic.Add("real_amount", NotifyBody.real_amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status == "30000")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.real_amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.real_amount), NotifyBody.transaction_id))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("GCPayWithdrawNotify")]
    public HttpResponseMessage GCPayWithdrawNotify([FromBody] _FIFIPayWithdrawNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("GCPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("customer_id", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("datetime", NotifyBody.datetime);//
            signDic.Add("transaction_id", NotifyBody.transaction_id);//
            signDic.Add("transaction_code", NotifyBody.transaction_code);//
            signDic.Add("transaction_msg", NotifyBody.transaction_msg);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.transaction_code.ToUpper() == "30000")
            {//成功

                if (withdrawalModel.Amount == decimal.Parse(NotifyBody.amount))
                {
                    withdrawSuccess = true;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("Amount Error");
                    return response;
                }

            }
            else if (NotifyBody.transaction_code.ToUpper() == "40000")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }


            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.transaction_id, decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("OK");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }

    #endregion

    #region ZINPay
    [HttpGet]
    [HttpPost]
    [ActionName("ZINPayPayNotify")]
    public HttpResponseMessage ZINPayPayNotify([FromBody] _FIFIPayNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("ZINPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("customer_id", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("transaction_id", NotifyBody.transaction_id);//
            signDic.Add("order_amount", NotifyBody.order_amount);//
            signDic.Add("real_amount", NotifyBody.real_amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status == "30000")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.real_amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.real_amount), NotifyBody.transaction_id))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("ZINPayWithdrawNotify")]
    public HttpResponseMessage ZINPayWithdrawNotify([FromBody] _FIFIPayWithdrawNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("ZINPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("customer_id", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("datetime", NotifyBody.datetime);//
            signDic.Add("transaction_id", NotifyBody.transaction_id);//
            signDic.Add("transaction_code", NotifyBody.transaction_code);//
            signDic.Add("transaction_msg", NotifyBody.transaction_msg);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.transaction_code.ToUpper() == "30000")
            {//成功

                if (withdrawalModel.Amount == decimal.Parse(NotifyBody.amount))
                {
                    withdrawSuccess = true;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("Amount Error");
                    return response;
                }

            }
            else if (NotifyBody.transaction_code.ToUpper() == "40000")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }


            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.transaction_id, decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("OK");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }

    #endregion

    #region EASYPAY
    [HttpGet]
    [HttpPost]
    [ActionName("EASYPAYNotify")]
    public HttpResponseMessage EASYPAYNotify([FromBody] _EASYPAYNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("EASYPAY");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.bill_number;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("client_id", providerSetting.MerchantCode);//
            signDic.Add("bill_number", NotifyBody.bill_number);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("timestamp", NotifyBody.timestamp);//
            signDic.Add("amount", NotifyBody.amount);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status == "已完成")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("OK");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("EASYPAYWithdrawNotify")]
    public HttpResponseMessage EASYPAYWithdrawNotify([FromBody] _EASYPAYWithdrawNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("EASYPAY");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.bill_number;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("client_id", providerSetting.MerchantCode);//
            signDic.Add("bill_number", NotifyBody.bill_number);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("fee", NotifyBody.fee);//
            signDic.Add("total_amount", NotifyBody.total_amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("timestamp", NotifyBody.timestamp);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.status == "已完成")
            {//成功

                if (withdrawalModel.Amount == decimal.Parse(NotifyBody.amount))
                {
                    withdrawSuccess = true;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("Amount Error");
                    return response;
                }

            }
            else if (NotifyBody.status == "失败")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }


            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("OK");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }

    #endregion

    #region CLOUDPAY
    [HttpGet]
    [HttpPost]
    [ActionName("CLOUDPAYNotify")]
    public HttpResponseMessage CLOUDPAYNotify()
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("CLOUDPAY");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;
        _CLOUDPAYNotifyBody NotifyBody = new _CLOUDPAYNotifyBody();
        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);
        var boundary = HttpContext.Current.Request.ContentType;
        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);

        if (string.IsNullOrEmpty(strBody))
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误: strBody empty", 3, "", providerSetting.ProviderCode);
            PayDB.InsertPaymentTransferLog("strBody:" + strBody, 3, paymentSerial, providerSetting.ProviderCode);
            PayDB.InsertPaymentTransferLog("boundary:" + boundary, 3, paymentSerial, providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }
        
        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                }
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得PaymentID

            paymentSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion
            #region 轉換status代碼

            if (NotifyBody.status == "5")
            {//成功
                #region 反查訂單
                System.Data.DataTable DT = null;
                DT = PayDB.GetPaymentByPaymentID(paymentSerial);

                if (DT != null && DT.Rows.Count > 0)
                {

                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }
                #endregion
            }
            else
            {//失敗
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("CLOUDPAYWithdrawNotify")]
    public HttpResponseMessage CLOUDPAYWithdrawNotify()
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("CLOUDPAY");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        _CLOUDPAYNotifyBody NotifyBody = new _CLOUDPAYNotifyBody();
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        string strBody = System.Text.Encoding.UTF8.GetString(Request.Content.ReadAsByteArrayAsync().Result);
        var boundary = HttpContext.Current.Request.ContentType;
        boundary = boundary.Split(';')[0].Split('/')[1];
        var datasArray = Regex.Split(strBody, boundary, RegexOptions.IgnoreCase);

        if (string.IsNullOrEmpty(strBody))
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误: strBody empty", 3, "", providerSetting.ProviderCode);
            PayDB.InsertPaymentTransferLog("strBody:" + strBody, 3, "", providerSetting.ProviderCode);
            PayDB.InsertPaymentTransferLog("boundary:" + boundary, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
        }

        foreach (var data in datasArray)
        {
            char[] crlf = new char[] { ';' };
            var dataArray = data.Split(crlf, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = { ' ', '\t', '\r', '\n' };

            dataArray = dataArray.Last().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (dataArray.Length > 2)
            {
                if (dataArray[0].Contains("merchant"))
                {
                    NotifyBody.merchant = dataArray[1];
                }
                else if (dataArray[0].Contains("order_id"))
                {
                    NotifyBody.order_id = dataArray[1];
                }
                else if (dataArray[0].Contains("amount"))
                {
                    NotifyBody.amount = dataArray[1];
                }
                else if (dataArray[0].Contains("status"))
                {
                    NotifyBody.status = dataArray[1];
                }
                else if (dataArray[0].Contains("message"))
                {
                    NotifyBody.message = dataArray[1];
                }
                else if (dataArray[0].Contains("sign"))
                {
                    NotifyBody.sign = dataArray[1];
                }
            }
        }

        if (!string.IsNullOrEmpty(NotifyBody.order_id))
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.order_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("merchant", providerSetting.MerchantCode);//
            signDic.Add("order_id", NotifyBody.order_id);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("message", NotifyBody.message);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    signStr += item.Key + "=" + item.Value + "&";
                }
            }

            signStr = signStr + "key=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false).ToUpper();

            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion

            #region 轉換status代碼
            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
            if (NotifyBody.status == "5")
            {//成功

                if (withdrawalModel.Amount == decimal.Parse(NotifyBody.amount))
                {
                    withdrawSuccess = true;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("訂單金額有誤", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("Amount Error");
                    return response;
                }

            }
            else if (NotifyBody.status == "3")
            {
                withdrawSuccess = false;
            }
            else
            {
                PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("WaitingProcess");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }


            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }

    #endregion

    #region PoPay
    [HttpGet]
    [HttpPost]
    [ActionName("PoPayNotify")]
    public HttpResponseMessage PoPayNotify([FromBody] _PoPayNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("PoPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

  
        if (NotifyBody != null)
        {
            #region 取得PaymentID
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
            paymentSerial = NotifyBody.merchantOrder;
            #endregion

        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("type", NotifyBody.type);//
            signDic.Add("orderNo", NotifyBody.orderNo);//
            signDic.Add("merchantNo", providerSetting.MerchantCode);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("realAmount", NotifyBody.realAmount);//
            signDic.Add("currency", NotifyBody.currency);//
            signDic.Add("merchantOrder", NotifyBody.merchantOrder);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("dateTime", NotifyBody.dateTime);//
            

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "payKey=" + providerSetting.MerchantKey;


            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.signature)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("sign fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetPaymentByPaymentID(paymentSerial);

            if (DT != null && DT.Rows.Count > 0)
            {

                if (NotifyBody.status == "2")
                {
                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.realAmount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.realAmount), NotifyBody.orderNo))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("PoPayWithdrawNotify")]
    public HttpResponseMessage PoPayWithdrawNotify(_PoPayWithdrawNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("PoPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.merchantOrder;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {
            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("type", NotifyBody.type);//
            signDic.Add("orderNo", NotifyBody.orderNo);//
            signDic.Add("merchantNo", providerSetting.MerchantCode);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("realAmount", NotifyBody.realAmount);//
            signDic.Add("currency", NotifyBody.currency);//
            signDic.Add("merchantOrder", NotifyBody.merchantOrder);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("dateTime", NotifyBody.dateTime);//
       
            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "payKey=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.signature)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("sign fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetWithdrawalByWithdrawID(withdrawSerial);

            if (DT != null && DT.Rows.Count > 0)
            {
                if (NotifyBody.status == "2" || NotifyBody.status == "6" || NotifyBody.status == "7")
                {
                    withdrawalModel = GatewayCommon.ToList<GatewayCommon.Withdrawal>(DT).FirstOrDefault();
                    if (withdrawalModel.Amount != decimal.Parse(NotifyBody.realAmount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.WithdrawalByProvider providerRequestData = new GatewayCommon.WithdrawalByProvider();
                    providerRequestData = GatewayCommon.QueryWithdrawalByProvider(withdrawalModel);

                    if (providerRequestData.IsQuerySuccess)
                    {
                        if (providerRequestData.WithdrawalStatus == 0)
                        {
                            withdrawSuccess = true;
                        }
                        else if (providerRequestData.WithdrawalStatus == 1)
                        {
                            withdrawSuccess = false;
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                            response = Request.CreateResponse(HttpStatusCode.OK);
                            HttpContext.Current.Response.Write("WaitingProcess");
                            //HttpContext.Current.Response.Flush();
                            //HttpContext.Current.Response.End();
                            return response;
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,查詢訂單失敗", 4, withdrawSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("WaitingProcess");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("WaitingProcess");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 4, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.orderNo, decimal.Parse(NotifyBody.realAmount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.orderNo, decimal.Parse(NotifyBody.realAmount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    #region GstarPay
    [HttpGet]
    [HttpPost]
    [ActionName("GstarPayNotify")]
    public HttpResponseMessage GstarPayNotify([FromBody] _PoPayNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("GstarPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion


        if (NotifyBody != null)
        {
            #region 取得PaymentID
            paymentSerial = NotifyBody.merchantOrder;
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
           
            #endregion

        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("type", NotifyBody.type);//
            signDic.Add("orderNo", NotifyBody.orderNo);//
            signDic.Add("merchantNo", providerSetting.MerchantCode);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("realAmount", NotifyBody.realAmount);//
            signDic.Add("currency", NotifyBody.currency);//
            signDic.Add("merchantOrder", NotifyBody.merchantOrder);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("dateTime", NotifyBody.dateTime);//


            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "payKey=" + providerSetting.MerchantKey;


            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.signature)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("sign fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetPaymentByPaymentID(paymentSerial);

            if (DT != null && DT.Rows.Count > 0)
            {

                if (NotifyBody.status == "2")
                {
                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.realAmount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.realAmount), NotifyBody.orderNo))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("SUCCESS");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("GstarPayWithdrawNotify")]
    public HttpResponseMessage GstarPayWithdrawNotify(_PoPayWithdrawNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("GstarPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.merchantOrder;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {
            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("type", NotifyBody.type);//
            signDic.Add("orderNo", NotifyBody.orderNo);//
            signDic.Add("merchantNo", providerSetting.MerchantCode);//
            signDic.Add("amount", NotifyBody.amount);//
            signDic.Add("realAmount", NotifyBody.realAmount);//
            signDic.Add("currency", NotifyBody.currency);//
            signDic.Add("merchantOrder", NotifyBody.merchantOrder);//
            signDic.Add("status", NotifyBody.status);//
            signDic.Add("dateTime", NotifyBody.dateTime);//

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "payKey=" + providerSetting.MerchantKey;

            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.signature)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("sign fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetWithdrawalByWithdrawID(withdrawSerial);

            if (DT != null && DT.Rows.Count > 0)
            {
                if (NotifyBody.status == "2" || NotifyBody.status == "6" || NotifyBody.status == "7")
                {
                    withdrawalModel = GatewayCommon.ToList<GatewayCommon.Withdrawal>(DT).FirstOrDefault();
                    if (withdrawalModel.Amount != decimal.Parse(NotifyBody.realAmount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.WithdrawalByProvider providerRequestData = new GatewayCommon.WithdrawalByProvider();
                    providerRequestData = GatewayCommon.QueryWithdrawalByProvider(withdrawalModel);

                    if (providerRequestData.IsQuerySuccess)
                    {
                        if (providerRequestData.WithdrawalStatus == 0)
                        {
                            withdrawSuccess = true;
                        }
                        else if (providerRequestData.WithdrawalStatus == 1)
                        {
                            withdrawSuccess = false;
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                            response = Request.CreateResponse(HttpStatusCode.OK);
                            HttpContext.Current.Response.Write("WaitingProcess");
                            //HttpContext.Current.Response.Flush();
                            //HttpContext.Current.Response.End();
                            return response;
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,查詢訂單失敗", 4, withdrawSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("WaitingProcess");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("WaitingProcess");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 4, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.orderNo, decimal.Parse(NotifyBody.realAmount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.orderNo, decimal.Parse(NotifyBody.realAmount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("SUCCESS");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    #region LUMIPay
    [HttpGet]
    [HttpPost]
    [ActionName("LUMIPayNotify")]
    public HttpResponseMessage LUMIPayNotify([FromBody] _LUMIPayNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("LUMIPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion


        if (NotifyBody != null)
        {
            #region 取得PaymentID
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
            paymentSerial = NotifyBody.data.order_number;
            #endregion

        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("username", providerSetting.MerchantCode);//
            signDic.Add("amount", NotifyBody.data.amount);//
            signDic.Add("order_number", NotifyBody.data.order_number);//
            signDic.Add("system_order_number", NotifyBody.data.system_order_number);//
            signDic.Add("status", NotifyBody.data.status);//

            signDic = CodingControl.AsciiDictionary(signDic);
            
            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "secret_key=" + providerSetting.MerchantKey;


            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.data.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("sign fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetPaymentByPaymentID(paymentSerial);

            if (DT != null && DT.Rows.Count > 0)
            {

                if (NotifyBody.data.status == "4"|| NotifyBody.data.status == "5")
                {
                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.data.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.data.amount), NotifyBody.data.system_order_number))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("success");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("LUMIPayWithdrawNotify")]
    public HttpResponseMessage LUMIPayWithdrawNotify(_LUMIPayNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("LUMIPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.data.order_number;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {
            #region 簽名檢查

            string signStr = "";
            Dictionary<string, string> signDic = new Dictionary<string, string>();

            signDic.Add("username", providerSetting.MerchantCode);//
            signDic.Add("amount", NotifyBody.data.amount);//
            signDic.Add("order_number", NotifyBody.data.order_number);//
            signDic.Add("system_order_number", NotifyBody.data.system_order_number);//
            signDic.Add("status", NotifyBody.data.status);//

            signDic = CodingControl.AsciiDictionary(signDic);

            foreach (KeyValuePair<string, string> item in signDic)
            {
                signStr += item.Key + "=" + item.Value + "&";
            }

            signStr = signStr + "secret_key=" + providerSetting.MerchantKey;


            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.data.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("sign fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetWithdrawalByWithdrawID(withdrawSerial);

            if (DT != null && DT.Rows.Count > 0)
            {
                if (NotifyBody.data.status == "4" || NotifyBody.data.status == "5" || NotifyBody.data.status == "6" || NotifyBody.data.status == "7" || NotifyBody.data.status == "8")
                {
                    withdrawalModel = GatewayCommon.ToList<GatewayCommon.Withdrawal>(DT).FirstOrDefault();
                    if (withdrawalModel.Amount != decimal.Parse(NotifyBody.data.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.WithdrawalByProvider providerRequestData = new GatewayCommon.WithdrawalByProvider();
                    providerRequestData = GatewayCommon.QueryWithdrawalByProvider(withdrawalModel);

                    if (providerRequestData.IsQuerySuccess)
                    {
                        if (providerRequestData.WithdrawalStatus == 0)
                        {
                            withdrawSuccess = true;
                        }
                        else if (providerRequestData.WithdrawalStatus == 1)
                        {
                            withdrawSuccess = false;
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                            response = Request.CreateResponse(HttpStatusCode.OK);
                            HttpContext.Current.Response.Write("WaitingProcess");
                            //HttpContext.Current.Response.Flush();
                            //HttpContext.Current.Response.End();
                            return response;
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,查詢訂單失敗", 4, withdrawSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("WaitingProcess");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("WaitingProcess");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 4, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.data.system_order_number, decimal.Parse(NotifyBody.data.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.data.system_order_number, decimal.Parse(NotifyBody.data.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("success");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    #region VirtualPay
    [HttpGet]
    [HttpPost]
    [ActionName("VirtualPayNotify")]
    public HttpResponseMessage VirtualPayNotify(_VirtualPayAsyncNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("VirtualPay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;
        JObject retMessage = new JObject();
        System.Data.DataTable PaymentDT = null;

        retMessage.Add("success", "false");

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("providerSetting : " + JsonConvert.SerializeObject(providerSetting), 3, "", providerSetting.ProviderCode);
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            retMessage.Add("message", "fail");
            HttpContext.Current.Response.Write(retMessage.ToString());
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得PaymentID
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
            PaymentDT= PayDB.GetPaymentByProviderOrderID(NotifyBody.transaction_id);
            if (PaymentDT!=null&& PaymentDT.Rows.Count>0)
            {
                paymentSerial = PaymentDT.Rows[0]["PaymentSerial"].ToString();
            }
            else {
                PayDB.InsertPaymentTransferLog("资料库内无此笔订单", 3, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                retMessage.Add("message", "fail");
                HttpContext.Current.Response.Write(retMessage.ToString());
                return response;
            }
         
            #endregion

        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            retMessage.Add("message", "fail");
            HttpContext.Current.Response.Write(retMessage.ToString());

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = "";
           
            signStr = NotifyBody.amount + NotifyBody.user_name + NotifyBody.transaction_id + providerSetting.MerchantKey;
            sign = CodingControl.GetMD5(signStr, false).ToUpper();
            if (sign != NotifyBody.sign.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                retMessage.Add("message", "fail");
                HttpContext.Current.Response.Write(retMessage.ToString());

                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PaymentDT;

            if (DT != null && DT.Rows.Count > 0)
            {

                if (NotifyBody.status == "deposited")
                {
                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        retMessage.Add("message", "amount error");
                        HttpContext.Current.Response.Write(retMessage.ToString());
                        return response;
                    }

                    processStatus = 2;
                    //GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    //providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    //if (providerRequestData.IsPaymentSuccess == true)
                    //{
                    //    processStatus = 2;
                    //}
                    //else
                    //{
                    //    response = Request.CreateResponse(HttpStatusCode.OK);
                    //    retMessage.Add("message", "dail");
                    //    HttpContext.Current.Response.Write(retMessage.ToString());
                    //    return response;
                    //}
                }
                else
                {
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    retMessage.Add("message", "dail");
                    HttpContext.Current.Response.Write(retMessage.ToString());
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                retMessage.Add("message", "dail");
                HttpContext.Current.Response.Write(retMessage.ToString());
                return response;
            }
            #endregion

            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), ""))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    retMessage["success"] = "true";
                    retMessage.Add("message", "");
                    HttpContext.Current.Response.Write(retMessage.ToString());
                    return response;
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    retMessage["success"] = "true";
                    retMessage.Add("message", "");
                    HttpContext.Current.Response.Write(retMessage.ToString());
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    retMessage["success"] = "true";
                    retMessage.Add("message", "");
                    HttpContext.Current.Response.Write(retMessage.ToString());
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    retMessage["success"] = "true";
                    retMessage.Add("message", "");
                    HttpContext.Current.Response.Write(retMessage.ToString());
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    retMessage["success"] = "true";
                    retMessage.Add("message", "");
                    HttpContext.Current.Response.Write(retMessage.ToString());
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    retMessage["success"] = "true";
                    retMessage.Add("message", "");
                    HttpContext.Current.Response.Write(retMessage.ToString());
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    retMessage["success"] = "true";
                    retMessage.Add("message", "");
                    HttpContext.Current.Response.Write(retMessage.ToString());
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    retMessage["success"] = "true";
                    retMessage.Add("message", "");
                    HttpContext.Current.Response.Write(retMessage.ToString());
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    retMessage["success"] = "true";
                    retMessage.Add("message", "");
                    HttpContext.Current.Response.Write(retMessage.ToString());
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("VirtualPayWithdrawNotify")]
    public HttpResponseMessage VirtualPayWithdrawNotify(_VirtualPayWithdrawAsyncNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("VirtualPay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel=null;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;
        JObject retMessage = new JObject();
        retMessage.Add("success", "false");

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            retMessage.Add("message", "fail");
            HttpContext.Current.Response.Write(retMessage.ToString());
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.transaction_id;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {
            #region 簽名檢查

            string signStr = "";
            signStr = providerSetting.OtherDatas[1]+ NotifyBody.amount + NotifyBody.transaction_id + NotifyBody.certfile_url;
            sign = CodingControl.GetSHA1(signStr, false).ToUpper();

            if (sign != NotifyBody.check_code.ToUpper())
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                retMessage.Add("message", "sign fail");
                HttpContext.Current.Response.Write(retMessage.ToString());
                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetWithdrawalByWithdrawID(withdrawSerial);

            if (DT != null && DT.Rows.Count > 0)
            {
                withdrawalModel = GatewayCommon.ToList<GatewayCommon.Withdrawal>(DT).FirstOrDefault();
                if (NotifyBody.success == "true")
                {
                    if (withdrawalModel.Amount != decimal.Parse(NotifyBody.amount))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        retMessage.Add("message", "amount error");
                        HttpContext.Current.Response.Write(retMessage.ToString());
                        return response;
                    }

                    withdrawSuccess = true;
                }
                else if (NotifyBody.success == "false") {
                    withdrawSuccess = false;
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    retMessage.Add("message", "WaitingProcess");
                    HttpContext.Current.Response.Write(retMessage.ToString());
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 4, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                retMessage.Add("message", "fail");
                HttpContext.Current.Response.Write(retMessage.ToString());
                return response;
            }
            #endregion

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.amount), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            retMessage["success"] = "true";
            retMessage.Add("message", "success");
            HttpContext.Current.Response.Write(retMessage.ToString());
            return response;
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    #region AeePay
    [HttpGet]
    [HttpPost]
    [ActionName("AeePayNotify")]
    public HttpResponseMessage AeePayNotify([FromBody] _AeePayAsyncNotifyBody NotifyBody)
    {
        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("AeePay");
        GatewayCommon.Payment paymentModel;
        int processStatus;
        HttpResponseMessage response;
        bool companyRequestResult = false;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        string paymentSerial = "";
        string sign;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion


        if (NotifyBody != null)
        {
            #region 取得PaymentID
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 3, paymentSerial, providerSetting.ProviderCode);
            paymentSerial = NotifyBody.morderno;
            #endregion

        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成订单通知有误", 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");

            return response;
        }

        try
        {

            #region 簽名檢查

            string signStr = providerSetting.MerchantCode + "|"+ NotifyBody.orderno + "|"+ NotifyBody.morderno + "|"+ NotifyBody.paycode + "|"+ NotifyBody.tjmoney + "|"+ NotifyBody.money + "|"+ NotifyBody.status + "|"+ providerSetting.MerchantKey;
       
            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, paymentSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("sign fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetPaymentByPaymentID(paymentSerial);

            if (DT != null && DT.Rows.Count > 0)
            {

                if (NotifyBody.status == "1")
                {
                    paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
                    if (paymentModel.OrderAmount != decimal.Parse(NotifyBody.tjmoney))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
                    providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);

                    if (providerRequestData.IsPaymentSuccess == true)
                    {
                        processStatus = 2;
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("fail");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("fail");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 3, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            //寫入預存
            switch (PayDB.SetPaymentProcessStatus(paymentSerial, processStatus, JsonConvert.SerializeObject(NotifyBody), "", decimal.Parse(NotifyBody.tjmoney), NotifyBody.merchantno))
            {
                case 0:
                    //撈取該單，準備回傳資料
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("ok");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -1:
                    //-1=交易單 不存在
                    PayDB.InsertPaymentTransferLog("交易单不存在", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("ok");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -2:
                    //-2=交易資料有誤 
                    PayDB.InsertPaymentTransferLog("交易资料有误", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("ok");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.ProblemPayment);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -3:
                    //-3=供應商，交易失敗
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("ok");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    paymentModel = PayDB.GetPaymentByPaymentID(paymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();
                    gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
                    CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
                    CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
                    }
                    else
                    {
                        companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, providerSetting.ProviderCode);
                    }
                    break;
                case -4:
                    //-4=鎖定失敗
                    PayDB.InsertPaymentTransferLog("锁定失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("ok");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -5:
                    //-5=加扣點失敗 
                    PayDB.InsertPaymentTransferLog("加扣点失败", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("ok");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -6:
                    //-6=通知廠商中 
                    PayDB.InsertPaymentTransferLog("通知厂商中", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("ok");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                case -7:
                    //-7=交易單非可修改之狀態 
                    PayDB.InsertPaymentTransferLog("交易单非可修改之状态", 3, paymentSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("ok");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                default:
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("ok");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new System.Diagnostics.StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            PayDB.InsertPaymentTransferLog("供应商完成订单通知,系统有误:" + ex.Message + ",Line:" + line, 3, "", providerSetting.ProviderCode);
            throw;
        }
        finally
        {

        }

        if (companyRequestResult)
        {
            PayDB.UpdatePaymentComplete(paymentSerial);
        }

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("AeePayWithdrawNotify")]
    public HttpResponseMessage AeePayWithdrawNotify(_AeePayWithdrawAsyncNotifyBody NotifyBody)
    {

        GatewayCommon.ProviderSetting providerSetting = GatewayCommon.GetProverderSettingData("AeePay");
        bool withdrawSuccess = false;
        string withdrawSerial = "";
        string sign = "";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        HttpResponseMessage response;

        #region 回调IP检查
        string ProxyIP = CodingControl.GetUserIP();
        if (!providerSetting.ProviderIP.Contains(ProxyIP))
        {
            PayDB.InsertPaymentTransferLog("该IP未在白名单内 : " + ProxyIP, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            return response;
        }
        #endregion

        if (NotifyBody != null)
        {
            #region 取得withdrawSerial
            withdrawSerial = NotifyBody.morderno;
            #endregion
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,回调IP:" + ProxyIP + ",资料:" + JsonConvert.SerializeObject(NotifyBody), 4, withdrawSerial, providerSetting.ProviderCode);
        }
        else
        {
            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知有误", 4, withdrawSerial, providerSetting.ProviderCode);
        }



        try
        {
            #region 簽名檢查

            string signStr = "";

            signStr = providerSetting.MerchantCode + "|"+ NotifyBody.orderno + "|"+ NotifyBody.bankcode + "|"+NotifyBody.morderno + "|"+ NotifyBody.cardno + "|"+ NotifyBody.tjmoney + "|"+ NotifyBody.money + "|"+ NotifyBody.status + "|"+ providerSetting.MerchantKey;


            sign = CodingControl.GetMD5(signStr, false);

            if (sign != NotifyBody.sign)
            {
                PayDB.InsertPaymentTransferLog("供应商完成订单通知,签名有误", 3, withdrawSerial, providerSetting.ProviderCode);
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("sign fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();

                return response;
            }

            #endregion


            #region 轉換status代碼

            #region 反查訂單
            System.Data.DataTable DT = null;
            DT = PayDB.GetWithdrawalByWithdrawID(withdrawSerial);

            if (DT != null && DT.Rows.Count > 0)
            {
                if (NotifyBody.status == "3" || NotifyBody.status == "4")
                {
                    withdrawalModel = GatewayCommon.ToList<GatewayCommon.Withdrawal>(DT).FirstOrDefault();
                    if (withdrawalModel.Amount != decimal.Parse(NotifyBody.tjmoney))
                    {
                        PayDB.InsertPaymentTransferLog("金額不符", 3, "", providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("amount error");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }

                    GatewayCommon.WithdrawalByProvider providerRequestData = new GatewayCommon.WithdrawalByProvider();
                    providerRequestData = GatewayCommon.QueryWithdrawalByProvider(withdrawalModel);

                    if (providerRequestData.IsQuerySuccess)
                    {
                        if (providerRequestData.WithdrawalStatus == 0)
                        {
                            withdrawSuccess = true;
                        }
                        else if (providerRequestData.WithdrawalStatus == 1)
                        {
                            withdrawSuccess = false;
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                            response = Request.CreateResponse(HttpStatusCode.OK);
                            HttpContext.Current.Response.Write("WaitingProcess");
                            //HttpContext.Current.Response.Flush();
                            //HttpContext.Current.Response.End();
                            return response;
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,查詢訂單失敗", 4, withdrawSerial, providerSetting.ProviderCode);
                        response = Request.CreateResponse(HttpStatusCode.OK);
                        HttpContext.Current.Response.Write("WaitingProcess");
                        //HttpContext.Current.Response.Flush();
                        //HttpContext.Current.Response.End();
                        return response;
                    }
                }
                else
                {
                    PayDB.InsertPaymentTransferLog("供应商完成代付订单通知,订单处理中", 4, withdrawSerial, providerSetting.ProviderCode);
                    response = Request.CreateResponse(HttpStatusCode.OK);
                    HttpContext.Current.Response.Write("WaitingProcess");
                    //HttpContext.Current.Response.Flush();
                    //HttpContext.Current.Response.End();
                    return response;
                }

            }
            else
            {
                PayDB.InsertPaymentTransferLog("反查订单失败,资料库内无此笔订单", 4, "", providerSetting.ProviderCode);

                PayDB.InsertBotSendLog(providerSetting.ProviderCode, providerSetting.ProviderCode + "反查订单失败,资料库内无此笔订单");
                response = Request.CreateResponse(HttpStatusCode.OK);
                HttpContext.Current.Response.Write("fail");
                //HttpContext.Current.Response.Flush();
                //HttpContext.Current.Response.End();
                return response;
            }
            #endregion

            #endregion

            if (withdrawalModel != null)
            {   //代付
                GatewayCommon.WithdrawResultStatus returnStatus;

                if (withdrawSuccess)
                {
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.orderno, decimal.Parse(NotifyBody.tjmoney), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, providerSetting.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                                break;
                        }
                    }
                    else
                    {

                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }
                else
                {
                    if (withdrawalModel.UpStatus != 2)
                    {
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.orderno, decimal.Parse(NotifyBody.tjmoney), 3, withdrawSerial);
                        returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else
                    {
                        if (withdrawalModel.Status == 2)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        }
                        else if (withdrawalModel.Status == 3)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.Failure;
                        }
                        else if (withdrawalModel.Status == 14)
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                        }
                        else
                        {
                            returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                        }
                        PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, providerSetting.ProviderCode);

                    }
                }

                withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                //取得傳送資料
                gpayReturn.SetByWithdraw(withdrawalModel, returnStatus);
                //發送API 回傳商戶
                if (withdrawalModel.FloatType != 0)
                {
                    System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
                    GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
                    //發送三次回調(後台手動發款後用)
                    if (CompanyModel.IsProxyCallBack == 0)
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                    else
                    {
                        //發送一次回調 補單用
                        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, providerSetting.ProviderCode))
                        {
                            //修改下游狀態
                            PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                        }
                    }
                }

            }

            response = Request.CreateResponse(HttpStatusCode.OK);

            HttpContext.Current.Response.Write("ok");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
        }
        catch (Exception ex)
        {
            PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 3, "", providerSetting.ProviderCode);
            response = Request.CreateResponse(HttpStatusCode.OK);
            HttpContext.Current.Response.Write("fail");
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();
            throw;
        }
        finally
        {

        }

        return response;
    }
    #endregion

    [HttpGet]
    [HttpPost]
    [ActionName("TestCompanyReturn")]
    public HttpResponseMessage TestCompanyReturn(JObject result)
    {
        var response = Request.CreateResponse(HttpStatusCode.OK);
        HttpContext.Current.Response.Write("SUCCESS");
        HttpContext.Current.Response.Flush();
        HttpContext.Current.Response.End();

        return response;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("TestWithdrawReturn")]
    public HttpResponseMessage TestWithdrawReturn(string code)
    {
        var data = Request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        PayDB.InsertPaymentTransferLog("data:" + data, 87, "", "");
        var response = Request.CreateResponse(HttpStatusCode.OK);
        HttpContext.Current.Response.Write("SUCCESS");
        HttpContext.Current.Response.Flush();
        HttpContext.Current.Response.End();

        return response;
    }


    #region notifyBody

    public class _AsiaPlay888AsyncNotifyBody
    {
        public string BankCode { get; set; }
        public string AccountNo { get; set; }
        public string BankTransactionNo { get; set; }
        public string CompleteDate { get; set; }
        public string Amount { get; set; }
        public string BuyerID { get; set; }
        public string SellerID { get; set; }
        public string OperatorOrderNo { get; set; }
        public string Status { get; set; }
    }

    public class _AsiaPlay888WithdrawalAsyncNotifyBody
    {
        public string BankCode { get; set; }
        public string OperatorOrderNo { get; set; }
        public string BankName { get; set; }
        public string BranchName { get; set; }
        public string BankAccount { get; set; }
        public string Name { get; set; }
        public string Amount { get; set; }
        public string CompleteDate { get; set; }

        public string Status { get; set; }
    }

    public class _TigerPayAsyncNotifyBody
    {
        public string user_account { get; set; }
        public string amount { get; set; }
        public string currency { get; set; }
        public string p_num { get; set; }
        public string transaction_number { get; set; }
        public string result { get; set; }
        public string to_fee { get; set; }
        public string Free { get; set; }

    }

    public class _NissinPayAsyncNotifyBody
    {
        public string merchantCode { get; set; }
        public string signType { get; set; }
        public string sign { get; set; }
        public string code { get; set; }
        public string message { get; set; }
        public string merchantOrderNo { get; set; }
        public string platformOrderNo { get; set; }
        public string orderAmount { get; set; }
        public string actualAmount { get; set; }
        public string actualFee { get; set; }
        public string orderStatus { get; set; }
    }
    
    public class _NissinWithdrawCheckBody
    {
        public string orderNo { get; set; }
    }

    public class _DiDiPayNotifyBody
    {
        public string merchant { get; set; }
        public string order_id { get; set; }
        public string amount { get; set; }
        public string status { get; set; }
        public string message { get; set; }
        public string sign { get; set; }

    }

    public class _FeibaoPayNotifyBody
    {
        public string code { get; set; }
        public string msg { get; set; }
        public string merchant { get; set; }
        public string merchant_order_num { get; set; }
        public string action { get; set; }
        public string order { get; set; }
    }

    public class _FeibaoPayOrderData
    {
        public string amount { get; set; }
        public string gateway { get; set; }
        public string status { get; set; }
        public string merchant_order_num { get; set; }
        public string merchant_order_remark { get; set; }
        public string sign { get; set; }
    }

    public class _EASYPAYNotifyBody
    {
        public string client_id { get; set; }
        public string bill_number { get; set; }
        public string status { get; set; }
        public string timestamp { get; set; }
        public string amount { get; set; }
        public string sign { get; set; }
    }

    public class _CLOUDPAYNotifyBody
    {
        public string merchant { get; set; }
        public string order_id { get; set; }
        public string amount { get; set; }
        public string status { get; set; }
        public string message { get; set; }
        public string sign { get; set; }
    }

    public class _EASYPAYWithdrawNotifyBody
    {
        public string client_id { get; set; }
        public string bill_number { get; set; }
        public string amount { get; set; }
        public string fee { get; set; }
        public string total_amount { get; set; }
        public string status { get; set; }
        public string timestamp { get; set; }
        public string sign { get; set; }
    }

    public class _FIFIPayNotifyBody
    {
        public string customer_id { get; set; }
        public string order_id { get; set; }
        public string transaction_id { get; set; }
        public string order_amount { get; set; }
        public string real_amount { get; set; }
        public string sign { get; set; }
        public string status { get; set; }
        public string message { get; set; }
        public _FIFIPayExtraData extra { get; set; }
    }

    public class _FIFIPayWithdrawNotifyBody
    {
        public string customer_id { get; set; }
        public string order_id { get; set; }
        public string amount { get; set; }
        public string datetime { get; set; }
        public string sign { get; set; }
        public string transaction_id { get; set; }
        public string transaction_code { get; set; }
        public string transaction_msg { get; set; }
    }

    public class _FIFIPayExtraData
    {
        public string refund_status { get; set; }
        public string bank_account_name { get; set; }
        public string bank_name { get; set; }
        public string bank_no { get; set; }
        public string bank_province { get; set; }
        public string bank_city { get; set; }
        public string bank_sub_branch { get; set; }
        public string phone { get; set; }
    }

    public class _YuHongNotifyBody
    {
        public string payOrderId { get; set; }
        public string mchId { get; set; }
        public string appId { get; set; }
        public string productId { get; set; }
        public string mchOrderNo { get; set; }
        public string amount { get; set; }
        public string income { get; set; }
        public string payer { get; set; }
        public string status { get; set; }
        public string channelOrderNo { get; set; }
        public string param1 { get; set; }
        public string param2 { get; set; }
        public string paySuccTime { get; set; }
        public string backType { get; set; }
        public string sign { get; set; }

    }

    public class _YuHongWithdrawNotifyBody
    {
        public string mchId { get; set; }
        public string mchOrderNo { get; set; }
        public string agentpayOrderId { get; set; }
        public string amount { get; set; }
        public string fee { get; set; }
        public string status { get; set; }
        public string transMsg { get; set; }
        public string extra { get; set; }
        public string sign { get; set; }

    }

    public class _HeroPayAsyncNotifyBody
    {
        public string payOrderId { get; set; }
        public string mchId { get; set; }
        public string appId { get; set; }

        public string productId { get; set; }
        public string mchOrderNo { get; set; }
       
        public string amount { get; set; }
        public string status { get; set; }

        public string channelOrderNo { get; set; }
        public string channelAttach { get; set; }
        public string paySuccTime { get; set; }
        public string backType { get; set; }
        public string sign { get; set; }
    }

    public class _HeroPayWithdrawAsyncNotifyBody
    {
        public string transOrderId { get; set; }
        public string mchId { get; set; }
        public string mchTransOrderNo { get; set; }

        public string amount { get; set; }
        public string status { get; set; }

        public string channelOrderNo { get; set; }
        public string transSuccTime { get; set; }

        public string backType { get; set; }
        public string sign { get; set; }
    }
    public class _coolemonsPayAsyncNotifyBody
    {
        public string status { get; set; }
        public string tradeNo { get; set; }
        public string orderNo { get; set; }
        public string userNo { get; set; }
        public string userName { get; set; }
        public string channelNo { get; set; }
        public string exchangeRate { get; set; }
        public string amount { get; set; }
        public string amountBeforeFixed { get; set; }
        public string discount { get; set; }
        public string lucky { get; set; }
        public string paid { get; set; }
        public string extra { get; set; }
        public string sign { get; set; }
    }

    public class _coolemonsPayWithdrawAsyncNotifyBody
    {
        public string status { get; set; }
        public string tradeNo { get; set; }
        public string orderNo { get; set; }
        public string amount { get; set; }
        public string exchangeRate { get; set; }
        public string name { get; set; }
        public string bankName { get; set; }
        public string bankAccount { get; set; }
        public string bankBranch { get; set; }
        public string memo { get; set; }
        public string mobile { get; set; }
        public string fee { get; set; }
        public string extra { get; set; }
        public string sign { get; set; }
    }

    public class _GASHPayAsyncNotifyBody
    {
        public string data { get; set; }
    }


    public class _PoPayNotifyBody
    {
        public string type { get; set; }
        public string orderNo { get; set; }
        public string merchantNo { get; set; }
        public string amount { get; set; }
        public string realAmount { get; set; }
        public string currency { get; set; }
        public string merchantOrder { get; set; }
        public string status { get; set; }
        public string dateTime { get; set; }
        public string signature { get; set; }
        public string successTime { get; set; }

    }

    public class _PoPayWithdrawNotifyBody
    {
        public string type { get; set; }
        public string orderNo { get; set; }
        public string merchantNo { get; set; }
        public string amount { get; set; }
        public string realAmount { get; set; }
        public string currency { get; set; }
        public string merchantOrder { get; set; }
        public string status { get; set; }
        public string dateTime { get; set; }
        public string signature { get; set; }
        public string successTime { get; set; }

    }

    public class _LUMIPayNotifyBody
    {
        public string http_status_code { get; set; }
        public string message { get; set; }
        public string error_code { get; set; }
        public _LUMIPayNotifyData data { get; set; }
    }

    public class _LUMIPayNotifyData
    {
        public string username { get; set; }
        public string amount { get; set; }
        public string order_number { get; set; }
        public string system_order_number { get; set; }
        public string status { get; set; }
        public string sign { get; set; }
    }

    public class _VirtualPayAsyncNotifyBody
    {
        public string status { get; set; }
        public string transaction_id { get; set; }
        public string amount { get; set; }
        public string user_name { get; set; }
        public string apikey { get; set; }
        public string sign { get; set; }
    }

    public class _VirtualPayWithdrawAsyncNotifyBody
    {
        public string api_key { get; set; }
        public string amount { get; set; }
        public string transaction_id { get; set; }
        public string certfile_url { get; set; }
        public string check_code { get; set; }
        public string success { get; set; }
    }

    public class _AeePayAsyncNotifyBody
    {
        public string orderno { get; set; }
        public string merchantno { get; set; }
        public string morderno { get; set; }
        public string paycode { get; set; }
        public string tjmoney { get; set; }
        public string money { get; set; }
        public string status { get; set; }
        public string sign { get; set; }
    }

    public class _AeePayWithdrawAsyncNotifyBody
    {
        public string orderno { get; set; }
        public string merchantno { get; set; }
        public string morderno { get; set; }
        public string bankcode { get; set; }
        public string cardno { get; set; }
        public string realname { get; set; }
        public string tjmoney { get; set; }
        public string money { get; set; }
        public string status { get; set; }
        public string sign { get; set; }

    }


    #endregion
}

