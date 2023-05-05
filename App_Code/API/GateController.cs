using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// api 類別名稱命名一定要用 <名稱>Controller
// api 存取方式: http://xxx/api/<名稱>
public class GateController : ApiController
{

    //金流流程：
    //1.RSA檢查 => 最後開工
    //2.檢查該營運商是否可進行交易
    //3.取得可用Provider(依據上下限、每日用量)

    //----
    //4.隨機選取一家
    //5.建立單
    //6.導向對應Provider的cshtml
    //7.加密處理，FormSubmit


    [HttpGet]
    [HttpPost]
    [ActionName("HeartBeat")]
    public string HeartBeat(string EchoString)
    {
        return EchoString;
    }

    [HttpGet]
    [HttpPost]
    [ActionName("Test")]
    public string Test()
    {

        System.Threading.Thread.Sleep(30000);
        return "aaa";
    }

    #region Payment

    // 要求用戶付款
    // 由用戶端瀏覽器執行 POST
    [HttpPost]
    [ActionName("RequirePaying")]
    public HttpResponseMessage RequirePaying([FromBody] FromBodyRequirePaying frombody)
    {
        FromBodyRequirePayment body = new FromBodyRequirePayment();
        HttpResponseMessage response = null;

        if (frombody == null)
        {
            PayDB.InsertDownOrderTransferLog("未带入参数", 0, "", "", "", true);
            response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + "未带入参数", UriKind.Relative);
            return response;
        }
        else
        {
            body.CompanyCode = frombody.ManageCode;
            body.CurrencyType = frombody.Currency;
            body.ServiceType = frombody.Service;
            body.BankCode = frombody.BankCode;
            body.ClientIP = frombody.CustomerIP;
            body.OrderID = frombody.OrderID;
            body.OrderDate = frombody.OrderDate;
            body.OrderAmount = frombody.OrderAmount;
            body.ReturnURL = frombody.RevolveURL;
            body.State = frombody.State;
            body.SelCurrency = frombody.SelCurrency;
            body.AssignAmount = frombody.AllotAmount;
            body.Sign = frombody.Sign;
            body.UserName = HttpUtility.UrlDecode(frombody.UserName);
            //if (body.CompanyCode == "AC001" || body.CompanyCode == "AR001" || body.CompanyCode == "509AI001" || body.CompanyCode == "AT0011")
            //{
            //    body.UserName = HttpUtility.UrlDecode(frombody.UserName);
            //}
            //else {
            //    body.UserName = frombody.UserName;
            //}
        }

        if (string.IsNullOrEmpty(body.OrderID))
        {
            PayDB.InsertDownOrderTransferLog("未带入 OrderID", 0, "", "", "", true);
            response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + "未输入 OrderID", UriKind.Relative);
            return response;
        }

        if (string.IsNullOrEmpty(body.CompanyCode))
        {
            PayDB.InsertDownOrderTransferLog("未带入 CompanyCode", 0, "", "", "", true);
            response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + "未带入 CompanyCode", UriKind.Relative);
            return response;
        }

        //if (body.ServiceType== "OOB01")
        //{
        //    if (string.IsNullOrEmpty(body.UserName)) {
        //        PayDB.InsertDownOrderTransferLog("实名通道须带入 UserName", 0, "", "", "", true);
        //        response = Request.CreateResponse(HttpStatusCode.Moved);
        //        response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + "实名通道须带入 UserName", UriKind.Relative);
        //        return response;
        //    }
        //}

        PayDB.InsertDownOrderTransferLog("充值申请:" + JsonConvert.SerializeObject(body), 0, "", body.OrderID, body.CompanyCode, false);
        string redirectURL = string.Empty;
        System.Data.DataTable DT;
        DateTime SummaryDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

        //公司相關變數
        GatewayCommon.Company CompanyModel;
        GatewayCommon.CompanyService CompanyServiceModel;

        //供應商相關 
        System.Data.DataTable ProviderDT;
        GatewayCommon.Provider ProviderModel;
        IList<GatewayCommon.GPayRelation> GPayRelationModels;
        List<Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>> GPaySelectModels = new List<Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>>();
        int selectProviderIndex = 0;
        //交易單相關
        GatewayCommon.Payment PaymentModel;
        string PaymentSerial;
        #region 黑名單
        if (PayDB.GetBlackListCountResult(CodingControl.GetUserIP(), "", "", "Payment") > 0)
        {
            PayDB.InsertDownOrderTransferLog("错误，黑名单成员。IP：" + CodingControl.GetUserIP(), 0, body.OrderID, body.OrderID, body.CompanyCode, true);
            response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidRequire, UriKind.Relative);
            return response;
        }

        if (!string.IsNullOrEmpty(body.UserName)) {
            if (PayDB.CheckPaymentUserName(body.UserName) > 0)
            {
                PayDB.InsertDownOrderTransferLog("错误，黑名单成员。会员名称：" + body.UserName, 0, body.OrderID, body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidRequire, UriKind.Relative);
                return response;
            }
        }
        #endregion

        #region 公司檢查
            //DT = PayDB.GetCompanyByCode(body.CompanyCode, true);
            DT = RedisCache.Company.GetCompanyByCode(body.CompanyCode);
        if (!(DT != null && DT.Rows.Count > 0))
        {
            PayDB.InsertDownOrderTransferLog("公司代码不存在", 0, "", body.OrderID, body.CompanyCode, true);
            response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + ResultCode.InvalidCompanyCode, UriKind.Relative);
            return response;
        }
        #endregion

        CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(DT).FirstOrDefault();

        #region 簽名檢查
        var sign = GatewayCommon.GetGPaySign(body.OrderID, body.OrderAmount, body.OrderDate, body.ServiceType, body.CurrencyType, body.CompanyCode, CompanyModel.CompanyKey);

        if (sign.ToUpper() != body.Sign.ToUpper())
        {
            PayDB.InsertDownOrderTransferLog("签名错误", 0, "", body.OrderID, body.CompanyCode, true);
            response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + ResultCode.SignFailure, UriKind.Relative);
            return response;
        }

        #endregion

        //查看過去是否已經有建單紀錄
        DT = PayDB.GetPaymentByCompanyOrderID(CompanyModel.CompanyID, body.OrderID);

        if (DT != null && DT.Rows.Count > 0)
        {

            #region 檢查之前存在的單是否為"新建"單

            PaymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();


            PayDB.InsertDownOrderTransferLog("订单已存在", 0, "", body.OrderID, body.CompanyCode, true);
            response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.RepeatOrderID, UriKind.Relative);
            return response;

            #endregion

        }
        else
        {

            #region 營運商相關檢查

            #region 公司狀態檢查
            if (!(CompanyModel.CompanyState == 0))
            {
                PayDB.InsertDownOrderTransferLog("商户已停用", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidCompany, UriKind.Relative);
                return response;
            }
            #endregion

            #region 公司幣別檢查
            DT = RedisCache.CompanyPoint.GetCompanyPointByID(CompanyModel.CompanyID);
            if (!(DT != null && DT.Select("CurrencyType='" + body.CurrencyType + "'").Length > 0))
            {
                PayDB.InsertDownOrderTransferLog("商户币别错误", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidCurrencyType, UriKind.Relative);
                return response;
            }
            #endregion

            #region 公司可用渠道檢查
            DT = RedisCache.CompanyService.GetCompanyService(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType);



            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("没有公司可用渠道", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidCompanyService, UriKind.Relative);
                return response;
            }
            #endregion

            if (string.IsNullOrEmpty(body.ReturnURL))
            {
                if (string.IsNullOrEmpty(CompanyModel.URL))
                {
                    response = Request.CreateResponse(HttpStatusCode.Moved);
                    response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.GetRedirectUrlFail, UriKind.Relative);
                    return response;
                }
                else
                {
                    redirectURL = CompanyModel.URL;
                }
            }
            else
            {
                redirectURL = body.ReturnURL;
            }


            CompanyServiceModel = GatewayCommon.ToList<GatewayCommon.CompanyService>(DT).FirstOrDefault();

            //營運商渠道停用檢查
            if (CompanyServiceModel.State == 1)
            {

                PayDB.InsertDownOrderTransferLog("商户渠道已停用", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidCompanyService, UriKind.Relative);
                return response;
            }

            #region 公司單筆上下限制檢查

            if (body.OrderAmount > CompanyServiceModel.MaxOnceAmount)
            {
                PayDB.InsertDownOrderTransferLog("商户超过上限额度", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.OrderAmountExceededLimit, UriKind.Relative);
                return response;
            }

            if (body.OrderAmount < CompanyServiceModel.MinOnceAmount)
            {
                PayDB.InsertDownOrderTransferLog("商户低于下限额度", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.OrderAmountExceedsLowerLimit, UriKind.Relative);
                return response;
            }
            #endregion

            #region 公司每日用量檢查
            //尚未有紀錄 => 尚無用量

            DT = PayDB.GetCompanySummary(SummaryDate, CompanyModel.CompanyID, body.CurrencyType, body.ServiceType);
            decimal CompanyBeforeAmount = 0;
            if (DT != null && DT.Rows.Count > 0)
            {
                CompanyBeforeAmount = (decimal)DT.Rows[0]["SummaryAmount"];
            }
            else
            {
                CompanyBeforeAmount = 0;
            }

            //MaxDaliyAmount=0 代表無上限
            if (CompanyServiceModel.MaxDaliyAmount != 0)
            {
                if ((body.OrderAmount + CompanyBeforeAmount) > CompanyServiceModel.MaxDaliyAmount)
                {
                    PayDB.InsertDownOrderTransferLog("商户每日用量不足", 0, "", body.OrderID, body.CompanyCode, true);
                    response = Request.CreateResponse(HttpStatusCode.Moved);
                    response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.ExceedDaliyAmount, UriKind.Relative);
                    return response;
                }
            }

            #endregion

            #endregion

            #region 供應商相關檢查

            #region 設定的供應商

            //if (CompanyModel.InsideLevel != 0) {
            // DT = PayDB.GetTopParentGPayRelation(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType, CompanyModel.SortKey);
            //} else {
            DT = RedisCache.GPayRelation.GetGPayRelation(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType);
            //}

            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("尚未设定对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidProviderService + "2", UriKind.Relative);
                return response;
            }

            GPayRelationModels = GatewayCommon.ToList<GatewayCommon.GPayRelation>(DT);

            #endregion

            #region 供應商Service檢查
            bool checkProviderServiceExist = false;

            foreach (var model in GPayRelationModels)
            {
                GatewayCommon.ProviderService providerServiceModel;
                //DT = PayDB.GetProviderServiceByProviderServiceType(model.ProviderCode, body.ServiceType, body.CurrencyType, false);
                DT = RedisCache.ProviderService.GetProviderService(model.ProviderCode, body.ServiceType, body.CurrencyType);
                if (DT != null && DT.Rows.Count > 0)
                {
                    providerServiceModel = GatewayCommon.ToList<GatewayCommon.ProviderService>(DT).FirstOrDefault();
                }
                else
                {
                    providerServiceModel = null;
                }

                if (providerServiceModel != null && providerServiceModel.State == 0)
                {

                    ProviderDT = null;
                    ProviderDT = RedisCache.ProviderCode.GetProviderCode(providerServiceModel.ProviderCode);

                    if (ProviderDT != null && DT.Rows.Count > 0)
                    {
                        if (ProviderDT.Rows[0]["ProviderState"].ToString() == "0")
                        {
                            if (((int)ProviderDT.Rows[0]["ProviderAPIType"] & 1) == 1)
                            {
                                GPaySelectModels.Add(new Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>(providerServiceModel, model));
                            }


                        }
                    }
                }
            }

            if (GPaySelectModels.Count > 0)
            {
                checkProviderServiceExist = true;
            }

            if (!checkProviderServiceExist)
            {
                PayDB.InsertDownOrderTransferLog("选择不到对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidProviderService + "1", UriKind.Relative);
                return response;
            }
            #endregion

            //撈取啟用狀態的相關供應商渠道
            //DT = PayDB.GetProviderServiceByServiceType(body.ServiceType, body.CurrencyType, false);

            //考慮到未來使用Redis之可能，不在SQL中加入OrderAmount相關的條件

            #region 檢查供應商的單筆上下限制
            GPaySelectModels = GPaySelectModels.Where(x => {
                //檢查上下限制
                if (body.OrderAmount > x.Item1.MaxOnceAmount || body.OrderAmount < x.Item1.MinOnceAmount)
                {
                    return false;
                }
                return true;
            }).ToList();

            if (GPaySelectModels.Count == 0)
            {
                PayDB.InsertDownOrderTransferLog("上游限额错误", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidProviderServiceByCheck1, UriKind.Relative);
                return response;
            }
            #endregion

            #region 檢查供應商的每日用量
            GPaySelectModels = GPaySelectModels.Where(x => {
                System.Data.DataTable ProviderSummaryDT;
                //檢查上下限制
                //檢查每日用量
                //****
                ProviderSummaryDT = PayDB.GetProviderSummary(SummaryDate, x.Item1.ProviderCode, x.Item1.CurrencyType, x.Item1.ServiceType);
                decimal ProviderBeforeAmount = 0;

                if (ProviderSummaryDT != null && ProviderSummaryDT.Rows.Count > 0)
                {
                    ProviderBeforeAmount = (decimal)ProviderSummaryDT.Rows[0]["SummaryAmount"];
                }
                else
                {
                    ProviderBeforeAmount = 0;
                }

                if ((body.OrderAmount + ProviderBeforeAmount) > x.Item1.MaxDaliyAmount)
                {
                    return false;
                }
                return true;
            }).ToList();

            if (GPaySelectModels.Count == 0)
            {
                PayDB.InsertDownOrderTransferLog("上游每日用量不足", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidProviderServiceByCheck2, UriKind.Relative);
                return response;
            }
            #endregion

            selectProviderIndex = GatewayCommon.SelectProviderService(GPaySelectModels);
            #region 取得供應商相關資料
            DT = RedisCache.ProviderCode.GetProviderCode(GPaySelectModels[selectProviderIndex].Item1.ProviderCode);
            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("选择不到对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidProviderService + "2", UriKind.Relative);
                return response;
            }
            else
            {
                if ((int)DT.Rows[0]["ProviderState"] == 1)
                {
                    PayDB.InsertDownOrderTransferLog("上游已关闭", 0, "", body.OrderID, body.CompanyCode, true);
                    response = Request.CreateResponse(HttpStatusCode.Moved);
                    response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidProviderService + "2", UriKind.Relative);
                    return response;
                }

                if (((int)DT.Rows[0]["ProviderAPIType"] & 1) != 1)
                {
                    PayDB.InsertDownOrderTransferLog("上游未开启充值权限", 0, "", body.OrderID, body.CompanyCode, true);
                    response = Request.CreateResponse(HttpStatusCode.Moved);
                    response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidProviderService + "2", UriKind.Relative);
                    return response;
                }

            }

            ProviderModel = GatewayCommon.ToList<GatewayCommon.Provider>(DT).FirstOrDefault();
            #endregion

            //取得供應商相關資料

            #endregion

            #region 其他檢查(待補)
            //BankCode相關

            //if (body.ServiceType == "OB002") {
            //    DT = RedisCache.BankCode.GetBankCode();

            //    bool isBankCodeExist = false;

            //    if (string.IsNullOrEmpty(body.BankCode)) {
            //        isBankCodeExist = true;
            //    } else {
            //        for (int i = 0 ; i < DT.Rows.Count ; i++) {
            //            if (DT.Rows[i]["BankCode"].ToString() == body.BankCode && (int)DT.Rows[i]["BankState"] == 0) {
            //                isBankCodeExist = true;
            //                break;
            //            }
            //        }
            //    }

            //    if (!isBankCodeExist) {
            //        response = Request.CreateResponse(HttpStatusCode.Moved);
            //        response.Headers.Location = new Uri(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidBankCode);
            //        return response;
            //    }
            //}                     
            #endregion

            #region 建立交易單
            int PaymentID;


            //HttpContext.Current.Response.Write(body.ServiceType);
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();

            //return response;

            //產生交易單model
            PaymentModel = new GatewayCommon.Payment()
            {
                forCompanyID = CompanyModel.CompanyID,
                CurrencyType = body.CurrencyType,
                ServiceType = body.ServiceType,
                BankCode = string.IsNullOrEmpty(body.BankCode) ? "" : body.BankCode,
                ProviderCode = GPaySelectModels[selectProviderIndex].Item1.ProviderCode,
                ReturnURL = redirectURL,
                State = string.IsNullOrEmpty(body.State) ? "" : body.State,
                ClientIP = body.ClientIP,
                UserIP = CodingControl.GetUserIP(),
                OrderID = body.OrderID,
                OrderDate = body.OrderDate,
                OrderAmount = Math.Truncate(body.OrderAmount),
                CostRate = GPaySelectModels[selectProviderIndex].Item1.CostRate,
                CostCharge = GPaySelectModels[selectProviderIndex].Item1.CostCharge,
                CollectRate = CompanyServiceModel.CollectRate,
                CollectCharge = CompanyServiceModel.CollectCharge,
                UserName=body.UserName
            };

            //if (ProviderModel.CollectType == 0) {

            //} else if (ProviderModel.CollectType == 1) {
            //    PaymentModel.CollectRate = GPaySelectModels[selectProviderIndex].Item1.CostRate;
            //    PaymentModel.CollectCharge = GPaySelectModels[selectProviderIndex].Item1.CostCharge;
            //}


            PaymentID = PayDB.InsertPayment(PaymentModel);

            if (PaymentID == 0)
            {
                PayDB.InsertDownOrderTransferLog("建立订单失败", 0, "", body.OrderID, body.CompanyCode, true);
                response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidPaymentID, UriKind.Relative);
                return response;
            }

            PaymentModel.PaymentID = PaymentID;
            #endregion
        }

        #region 新建單 => 尚未提交
        PaymentSerial = "IP" + System.DateTime.Now.ToString("yyyyMMddHHmm") + (new string('0', 10 - PaymentModel.PaymentID.ToString().Length) + PaymentModel.PaymentID.ToString());
        if (PayDB.UpdatePaymentSerial(PaymentSerial, PaymentModel.PaymentID) == 0)
        {
            PayDB.InsertDownOrderTransferLog("建立订单号失败", 0, PaymentSerial, body.OrderID, body.CompanyCode, true);
            response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidOrderID, UriKind.Relative);
            return response;
        }

        PayDB.SetUpdateSummaryCount(CompanyModel.CompanyID, PaymentModel.ProviderCode, PaymentModel.CurrencyType, PaymentModel.ServiceType);

        PaymentModel.PaymentSerial = PaymentSerial;
        #endregion

        PayDB.InsertDownOrderTransferLog("充值订单完成:", 0, PaymentSerial, body.OrderID, body.CompanyCode, false);
        response = Request.CreateResponse(HttpStatusCode.Moved);

        if (PaymentModel.ProviderCode == "TestProvider")
        {
            response.Headers.Location = new Uri("/RedirectView/TestRedirectCardToCard.cshtml?Amount=" + PaymentModel.OrderAmount, UriKind.Relative);
        }

        else {
            if (body.ServiceType == "OOB01")
            {
                if (!string.IsNullOrEmpty(body.UserName))
                {
                    response.Headers.Location = new Uri("/RedirectView/GatewayRedirect.cshtml?PaymentID=" + PaymentModel.PaymentID, UriKind.Relative);
                }
                else
                {
                    response.Headers.Location = new Uri("/RedirectView/SaveRealName.cshtml?PaymentID=" + PaymentModel.PaymentID, UriKind.Relative);
                }
            }
            else {
                response.Headers.Location = new Uri("/RedirectView/GatewayRedirect.cshtml?PaymentID=" + PaymentModel.PaymentID, UriKind.Relative);
            }
        }

        return response;
    }


    [HttpPost]
    [ActionName("RequirePayingUrl")]
    public HttpResponseMessage RequirePayingUrl([FromBody] FromBodyRequirePaying frombody)
    {
        FromBodyRequirePayment body = new FromBodyRequirePayment();

        #region 回傳相關
        HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
        GatewayCommon.ReturnByRequirePayment2 paymentResult = new GatewayCommon.ReturnByRequirePayment2() { Status = GatewayCommon.ResultStatus.ERR };

        //response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        #endregion

        if (frombody == null)
        {
            PayDB.InsertDownOrderTransferLog("未带入参数", 0, "", "", "", true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "未带入参数";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));

            return response;
        }
        else
        {
            body.CompanyCode = frombody.ManageCode;
            body.CurrencyType = frombody.Currency;
            body.ServiceType = frombody.Service;
            body.BankCode = frombody.BankCode;
            body.ClientIP = frombody.CustomerIP;
            body.OrderID = frombody.OrderID;
            body.OrderDate = frombody.OrderDate;
            body.OrderAmount = frombody.OrderAmount;
            body.ReturnURL = frombody.RevolveURL;
            body.State = frombody.State;
            body.SelCurrency = frombody.SelCurrency;
            body.AssignAmount = frombody.AllotAmount;
            body.Sign = frombody.Sign;
            body.UserName = HttpUtility.UrlDecode(frombody.UserName);
        }

        if (string.IsNullOrEmpty(body.OrderID))
        {
            PayDB.InsertDownOrderTransferLog("未带入 OrderID", 0, "", "", "", true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "未带入 OrderID";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        if (string.IsNullOrEmpty(body.CompanyCode))
        {
            PayDB.InsertDownOrderTransferLog("未带入商戶代碼", 0, "", "", "", true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "未带入商戶代碼";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        PayDB.InsertDownOrderTransferLog("充值申请:" + JsonConvert.SerializeObject(body), 0, "", body.OrderID, body.CompanyCode, false);
        string redirectURL = string.Empty;
        System.Data.DataTable DT;
        DateTime SummaryDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

        //公司相關變數
        GatewayCommon.Company CompanyModel;
        GatewayCommon.CompanyService CompanyServiceModel;

        //供應商相關 
        System.Data.DataTable ProviderDT;
        GatewayCommon.Provider ProviderModel;
        IList<GatewayCommon.GPayRelation> GPayRelationModels;
        List<Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>> GPaySelectModels = new List<Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>>();
        int selectProviderIndex = 0;
        //交易單相關
        GatewayCommon.Payment PaymentModel;
        string PaymentSerial;
        #region 黑名單
        if (PayDB.GetBlackListCountResult(CodingControl.GetUserIP(), "", "", "Payment") > 0)
        {
            PayDB.InsertDownOrderTransferLog("错误，黑名单成员。IP：" + CodingControl.GetUserIP(), 0, body.OrderID, body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "错误，黑名单成员。IP：" + CodingControl.GetUserIP();
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        if (!string.IsNullOrEmpty(body.UserName))
        {
            if (PayDB.CheckPaymentUserName(body.UserName) > 0)
            {
                PayDB.InsertDownOrderTransferLog("错误，黑名单成员。会员名称：" + body.UserName, 0, body.OrderID, body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "错误，黑名单成员。会员名称：" + body.UserName;
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
        }
        #endregion

        #region 公司檢查
        //DT = PayDB.GetCompanyByCode(body.CompanyCode, true);
        DT = RedisCache.Company.GetCompanyByCode(body.CompanyCode);
        if (!(DT != null && DT.Rows.Count > 0))
        {
            PayDB.InsertDownOrderTransferLog("公司代码不存在", 0, "", body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "公司代码不存在" + body.UserName;
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }
        #endregion

        CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(DT).FirstOrDefault();

        #region 簽名檢查
        var sign = GatewayCommon.GetGPaySign(body.OrderID, body.OrderAmount, body.OrderDate, body.ServiceType, body.CurrencyType, body.CompanyCode, CompanyModel.CompanyKey);

        if (sign.ToUpper() != body.Sign.ToUpper())
        {
            PayDB.InsertDownOrderTransferLog("签名错误", 0, "", body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "签名错误";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        #endregion

        //查看過去是否已經有建單紀錄
        DT = PayDB.GetPaymentByCompanyOrderID(CompanyModel.CompanyID, body.OrderID);

        if (DT != null && DT.Rows.Count > 0)
        {

            #region 檢查之前存在的單是否為"新建"單

            PaymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();


            PayDB.InsertDownOrderTransferLog("订单已存在", 0, "", body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "订单已存在";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;

            #endregion

        }
        else
        {

            #region 營運商相關檢查

            #region 公司狀態檢查
            if (!(CompanyModel.CompanyState == 0))
            {
                PayDB.InsertDownOrderTransferLog("商户已停用", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "商户已停用";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            #region 公司幣別檢查
            DT = RedisCache.CompanyPoint.GetCompanyPointByID(CompanyModel.CompanyID);
            if (!(DT != null && DT.Select("CurrencyType='" + body.CurrencyType + "'").Length > 0))
            {
                PayDB.InsertDownOrderTransferLog("商户币别错误", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "商户币别错误";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            #region 公司可用渠道檢查
            DT = RedisCache.CompanyService.GetCompanyService(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType);

            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("没有公司可用渠道", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "没有公司可用渠道";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            if (string.IsNullOrEmpty(body.ReturnURL))
            {
                if (string.IsNullOrEmpty(CompanyModel.URL))
                {

                    PayDB.InsertDownOrderTransferLog("RevolveURL为空", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "RevolveURL为空";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }
                else
                {
                    redirectURL = CompanyModel.URL;
                }
            }
            else
            {
                redirectURL = body.ReturnURL;
            }


            CompanyServiceModel = GatewayCommon.ToList<GatewayCommon.CompanyService>(DT).FirstOrDefault();

            //營運商渠道停用檢查
            if (CompanyServiceModel.State == 1)
            {

                PayDB.InsertDownOrderTransferLog("商户渠道已停用", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "商户渠道已停用";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }

            #region 公司單筆上下限制檢查

            if (CompanyModel.CompanyType != 4)
            {
                if (body.OrderAmount > CompanyServiceModel.MaxOnceAmount)
                {
                    PayDB.InsertDownOrderTransferLog("商户超过上限额度", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "商户超过上限额度";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }

                if (body.OrderAmount < CompanyServiceModel.MinOnceAmount)
                {
                    PayDB.InsertDownOrderTransferLog("商户低于下限额度", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "商户低于下限额度";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }
            }

            #endregion

            #region 公司每日用量檢查
            //尚未有紀錄 => 尚無用量

            DT = PayDB.GetCompanySummary(SummaryDate, CompanyModel.CompanyID, body.CurrencyType, body.ServiceType);
            decimal CompanyBeforeAmount = 0;
            if (DT != null && DT.Rows.Count > 0)
            {
                CompanyBeforeAmount = (decimal)DT.Rows[0]["SummaryAmount"];
            }
            else
            {
                CompanyBeforeAmount = 0;
            }

            //MaxDaliyAmount=0 代表無上限
            if (CompanyServiceModel.MaxDaliyAmount != 0)
            {
                if ((body.OrderAmount + CompanyBeforeAmount) > CompanyServiceModel.MaxDaliyAmount)
                {
                    PayDB.InsertDownOrderTransferLog("商户每日用量不足", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "商户每日用量不足";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }
            }

            #endregion

            #endregion

            #region 供應商相關檢查

            #region 設定的供應商

            //if (CompanyModel.InsideLevel != 0) {
            // DT = PayDB.GetTopParentGPayRelation(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType, CompanyModel.SortKey);
            //} else {
            DT = RedisCache.GPayRelation.GetGPayRelation(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType);
            //}

            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("尚未设定对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "尚未设定对应上游";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }

            GPayRelationModels = GatewayCommon.ToList<GatewayCommon.GPayRelation>(DT);

            #endregion

            #region 供應商Service檢查
            bool checkProviderServiceExist = false;

            foreach (var model in GPayRelationModels)
            {
                GatewayCommon.ProviderService providerServiceModel;
                //DT = PayDB.GetProviderServiceByProviderServiceType(model.ProviderCode, body.ServiceType, body.CurrencyType, false);
                DT = RedisCache.ProviderService.GetProviderService(model.ProviderCode, body.ServiceType, body.CurrencyType);
                if (DT != null && DT.Rows.Count > 0)
                {
                    providerServiceModel = GatewayCommon.ToList<GatewayCommon.ProviderService>(DT).FirstOrDefault();
                }
                else
                {
                    providerServiceModel = null;
                }

                if (providerServiceModel != null && providerServiceModel.State == 0)
                {

                    ProviderDT = null;
                    ProviderDT = RedisCache.ProviderCode.GetProviderCode(providerServiceModel.ProviderCode);

                    if (ProviderDT != null && DT.Rows.Count > 0)
                    {
                        if (ProviderDT.Rows[0]["ProviderState"].ToString() == "0")
                        {
                            if (((int)ProviderDT.Rows[0]["ProviderAPIType"] & 1) == 1)
                            {
                                GPaySelectModels.Add(new Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>(providerServiceModel, model));
                            }


                        }
                    }
                }
            }

            if (GPaySelectModels.Count > 0)
            {
                checkProviderServiceExist = true;
            }

            if (!checkProviderServiceExist)
            {
                PayDB.InsertDownOrderTransferLog("选择不到对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "选择不到对应上游";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            //撈取啟用狀態的相關供應商渠道
            //DT = PayDB.GetProviderServiceByServiceType(body.ServiceType, body.CurrencyType, false);

            //考慮到未來使用Redis之可能，不在SQL中加入OrderAmount相關的條件

            #region 檢查供應商的單筆上下限制
            GPaySelectModels = GPaySelectModels.Where(x =>
            {
                //檢查上下限制
                if (body.OrderAmount > x.Item1.MaxOnceAmount || body.OrderAmount < x.Item1.MinOnceAmount)
                {
                    return false;
                }
                return true;
            }).ToList();

            if (GPaySelectModels.Count == 0)
            {
                PayDB.InsertDownOrderTransferLog("上游限额错误", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "上游限额错误";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            #region 檢查供應商的每日用量
            GPaySelectModels = GPaySelectModels.Where(x =>
            {
                System.Data.DataTable ProviderSummaryDT;
                //檢查上下限制
                //檢查每日用量
                //****
                if (x.Item1.MaxDaliyAmount == 0)
                {
                    return true;
                }

                ProviderSummaryDT = PayDB.GetProviderSummary(SummaryDate, x.Item1.ProviderCode, x.Item1.CurrencyType, x.Item1.ServiceType);
                decimal ProviderBeforeAmount = 0;


                if (ProviderSummaryDT != null && ProviderSummaryDT.Rows.Count > 0)
                {
                    ProviderBeforeAmount = (decimal)ProviderSummaryDT.Rows[0]["SummaryAmount"];
                }
                else
                {
                    ProviderBeforeAmount = 0;
                }

                if ((body.OrderAmount + ProviderBeforeAmount) > x.Item1.MaxDaliyAmount)
                {
                    return false;
                }
                return true;
            }).ToList();

            if (GPaySelectModels.Count == 0)
            {
                PayDB.InsertDownOrderTransferLog("上游每日用量不足", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "上游每日用量不足";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            selectProviderIndex = GatewayCommon.SelectProviderService(GPaySelectModels);
            #region 取得供應商相關資料
            DT = RedisCache.ProviderCode.GetProviderCode(GPaySelectModels[selectProviderIndex].Item1.ProviderCode);
            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("选择不到对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "选择不到对应上游";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            else
            {
                if ((int)DT.Rows[0]["ProviderState"] == 1)
                {
                    PayDB.InsertDownOrderTransferLog("上游已关闭", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "上游已关闭";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }

                if (((int)DT.Rows[0]["ProviderAPIType"] & 1) != 1)
                {
                    PayDB.InsertDownOrderTransferLog("上游未开启充值权限", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "上游未开启充值权限";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }

            }

            ProviderModel = GatewayCommon.ToList<GatewayCommon.Provider>(DT).FirstOrDefault();
            #endregion

            //取得供應商相關資料

            #endregion

            #region 其他檢查(待補)
            //BankCode相關

            //if (body.ServiceType == "OB002") {
            //    DT = RedisCache.BankCode.GetBankCode();

            //    bool isBankCodeExist = false;

            //    if (string.IsNullOrEmpty(body.BankCode)) {
            //        isBankCodeExist = true;
            //    } else {
            //        for (int i = 0 ; i < DT.Rows.Count ; i++) {
            //            if (DT.Rows[i]["BankCode"].ToString() == body.BankCode && (int)DT.Rows[i]["BankState"] == 0) {
            //                isBankCodeExist = true;
            //                break;
            //            }
            //        }
            //    }

            //    if (!isBankCodeExist) {
            //        response = Request.CreateResponse(HttpStatusCode.Moved);
            //        response.Headers.Location = new Uri(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidBankCode);
            //        return response;
            //    }
            //}                     
            #endregion

            #region 建立交易單
            int PaymentID;


            //HttpContext.Current.Response.Write(body.ServiceType);
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();

            //return response;

            //產生交易單model
            PaymentModel = new GatewayCommon.Payment()
            {
                forCompanyID = CompanyModel.CompanyID,
                CurrencyType = body.CurrencyType,
                ServiceType = body.ServiceType,
                BankCode = string.IsNullOrEmpty(body.BankCode) ? "" : body.BankCode,
                ProviderCode = GPaySelectModels[selectProviderIndex].Item1.ProviderCode,
                ReturnURL = redirectURL,
                State = string.IsNullOrEmpty(body.State) ? "" : body.State,
                ClientIP = body.ClientIP,
                UserIP = CodingControl.GetUserIP(),
                OrderID = body.OrderID,
                OrderDate = body.OrderDate,
                OrderAmount = Math.Truncate(body.OrderAmount),
                CostRate = GPaySelectModels[selectProviderIndex].Item1.CostRate,
                CostCharge = GPaySelectModels[selectProviderIndex].Item1.CostCharge,
                CollectRate = CompanyModel.CompanyType == 4 ? GPaySelectModels[selectProviderIndex].Item1.CostRate : CompanyServiceModel.CollectRate,
                CollectCharge = CompanyModel.CompanyType == 4 ? GPaySelectModels[selectProviderIndex].Item1.CostCharge : CompanyServiceModel.CollectCharge,
                UserName = body.UserName
            };

            //if (ProviderModel.CollectType == 0) {

            //} else if (ProviderModel.CollectType == 1) {
            //    PaymentModel.CollectRate = GPaySelectModels[selectProviderIndex].Item1.CostRate;
            //    PaymentModel.CollectCharge = GPaySelectModels[selectProviderIndex].Item1.CostCharge;
            //}


            PaymentID = PayDB.InsertPayment(PaymentModel);

            if (PaymentID == 0)
            {
                PayDB.InsertDownOrderTransferLog("建立订单失败", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "建立订单失败";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }

            PaymentModel.PaymentID = PaymentID;
            #endregion
        }

        #region 新建單 => 尚未提交
        if (CompanyModel.CompanyType != 4)
        {
            PaymentSerial = "IP" + System.DateTime.Now.ToString("yyyyMMddHHmm") + (new string('0', 10 - PaymentModel.PaymentID.ToString().Length) + PaymentModel.PaymentID.ToString());
        }
        else
        {
            PaymentSerial = PaymentModel.OrderID;
        }

        if (PayDB.UpdatePaymentSerial(PaymentSerial, PaymentModel.PaymentID) == 0)
        {
            PayDB.InsertDownOrderTransferLog("建立订单号失败", 0, PaymentSerial, body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "建立订单号失败";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        PayDB.SetUpdateSummaryCount(CompanyModel.CompanyID, PaymentModel.ProviderCode, PaymentModel.CurrencyType, PaymentModel.ServiceType);

        PaymentModel.PaymentSerial = PaymentSerial;
        #endregion


        var providerRequestData = GatewayCommon.GetProviderRequestData2(PaymentModel);
        if (string.IsNullOrEmpty(providerRequestData.ProviderUrl))
        {
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "取得充值網址失敗";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));

            return response;
        }

        PayDB.InsertDownOrderTransferLog("充值订单完成:", 0, PaymentSerial, body.OrderID, body.CompanyCode, false);

        paymentResult.Status = GatewayCommon.ResultStatus.OK;
        paymentResult.Url = providerRequestData.ProviderUrl;
        paymentResult.PayingSerial = PaymentSerial;
        paymentResult.OrderAmount = PaymentModel.OrderAmount;
        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));

        return response;
    }


    [HttpPost]
    [ActionName("RequirePayingReturnUrl")]
    public HttpResponseMessage RequirePayingReturnUrl([FromBody] FromBodyRequirePaying frombody)
    {
        FromBodyRequirePayment body = new FromBodyRequirePayment();

        #region 回傳相關
        HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
        GatewayCommon.ReturnByRequirePayment paymentResult = new GatewayCommon.ReturnByRequirePayment() { Status = GatewayCommon.ResultStatus.ERR };

        //response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        #endregion

        if (frombody == null)
        {
            PayDB.InsertDownOrderTransferLog("未带入参数", 0, "", "", "", true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "未带入参数";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));

            return response;
        }
        else
        {
            body.CompanyCode = frombody.ManageCode;
            body.CurrencyType = frombody.Currency;
            body.ServiceType = frombody.Service;
            body.BankCode = frombody.BankCode;
            body.ClientIP = frombody.CustomerIP;
            body.OrderID = frombody.OrderID;
            body.OrderDate = frombody.OrderDate;
            body.OrderAmount = frombody.OrderAmount;
            body.ReturnURL = frombody.RevolveURL;
            body.State = frombody.State;
            body.SelCurrency = frombody.SelCurrency;
            body.AssignAmount = frombody.AllotAmount;
            body.Sign = frombody.Sign;
            body.UserName = HttpUtility.UrlDecode(frombody.UserName);
        }

        if (string.IsNullOrEmpty(body.OrderID))
        {
            PayDB.InsertDownOrderTransferLog("未带入 OrderID", 0, "", "", "", true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "未带入 OrderID";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        if (string.IsNullOrEmpty(body.CompanyCode))
        {
            PayDB.InsertDownOrderTransferLog("未带入商戶代碼", 0, "", "", "", true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "未带入商戶代碼";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        PayDB.InsertDownOrderTransferLog("充值申请:" + JsonConvert.SerializeObject(body), 0, "", body.OrderID, body.CompanyCode, false);
        string redirectURL = string.Empty;
        System.Data.DataTable DT;
        DateTime SummaryDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

        //公司相關變數
        GatewayCommon.Company CompanyModel;
        GatewayCommon.CompanyService CompanyServiceModel;

        //供應商相關 
        System.Data.DataTable ProviderDT;
        GatewayCommon.Provider ProviderModel;
        IList<GatewayCommon.GPayRelation> GPayRelationModels;
        List<Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>> GPaySelectModels = new List<Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>>();
        int selectProviderIndex = 0;
        //交易單相關
        GatewayCommon.Payment PaymentModel;
        string PaymentSerial;
        #region 黑名單
        if (PayDB.GetBlackListCountResult(CodingControl.GetUserIP(), "", "", "Payment") > 0)
        {
            PayDB.InsertDownOrderTransferLog("错误，黑名单成员。IP：" + CodingControl.GetUserIP(), 0, body.OrderID, body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "错误，黑名单成员。IP：" + CodingControl.GetUserIP();
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        if (!string.IsNullOrEmpty(body.UserName))
        {
            if (PayDB.CheckPaymentUserName(body.UserName) > 0)
            {
                PayDB.InsertDownOrderTransferLog("错误，黑名单成员。会员名称：" + body.UserName, 0, body.OrderID, body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "错误，黑名单成员。会员名称：" + body.UserName;
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
        }
        #endregion

        #region 公司檢查
        //DT = PayDB.GetCompanyByCode(body.CompanyCode, true);
        DT = RedisCache.Company.GetCompanyByCode(body.CompanyCode);
        if (!(DT != null && DT.Rows.Count > 0))
        {
            PayDB.InsertDownOrderTransferLog("公司代码不存在", 0, "", body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "公司代码不存在" + body.UserName;
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }
        #endregion

        CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(DT).FirstOrDefault();

        #region 簽名檢查
        var sign = GatewayCommon.GetGPaySign(body.OrderID, body.OrderAmount, body.OrderDate, body.ServiceType, body.CurrencyType, body.CompanyCode, CompanyModel.CompanyKey);

        if (sign.ToUpper() != body.Sign.ToUpper())
        {
            PayDB.InsertDownOrderTransferLog("签名错误", 0, "", body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "签名错误";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        #endregion

        //查看過去是否已經有建單紀錄
        DT = PayDB.GetPaymentByCompanyOrderID(CompanyModel.CompanyID, body.OrderID);

        if (DT != null && DT.Rows.Count > 0)
        {

            #region 檢查之前存在的單是否為"新建"單

            PaymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();


            PayDB.InsertDownOrderTransferLog("订单已存在", 0, "", body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "订单已存在";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;

            #endregion

        }
        else
        {

            #region 營運商相關檢查

            #region 公司狀態檢查
            if (!(CompanyModel.CompanyState == 0))
            {
                PayDB.InsertDownOrderTransferLog("商户已停用", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "商户已停用";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            #region 公司幣別檢查
            DT = RedisCache.CompanyPoint.GetCompanyPointByID(CompanyModel.CompanyID);
            if (!(DT != null && DT.Select("CurrencyType='" + body.CurrencyType + "'").Length > 0))
            {
                PayDB.InsertDownOrderTransferLog("商户币别错误", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "商户币别错误";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            #region 公司可用渠道檢查
            DT = RedisCache.CompanyService.GetCompanyService(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType);

            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("没有公司可用渠道", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "没有公司可用渠道";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            if (string.IsNullOrEmpty(body.ReturnURL))
            {
                if (string.IsNullOrEmpty(CompanyModel.URL))
                {
                    response = Request.CreateResponse(HttpStatusCode.Moved);
                    response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.GetRedirectUrlFail, UriKind.Relative);
                    return response;
                }
                else
                {
                    redirectURL = CompanyModel.URL;
                }
            }
            else
            {
                redirectURL = body.ReturnURL;
            }


            CompanyServiceModel = GatewayCommon.ToList<GatewayCommon.CompanyService>(DT).FirstOrDefault();

            //營運商渠道停用檢查
            if (CompanyServiceModel.State == 1)
            {

                PayDB.InsertDownOrderTransferLog("商户渠道已停用", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "商户渠道已停用";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }

            #region 公司單筆上下限制檢查

            if (CompanyModel.CompanyType != 4)
            {
                if (body.OrderAmount > CompanyServiceModel.MaxOnceAmount)
                {
                    PayDB.InsertDownOrderTransferLog("商户超过上限额度", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "商户超过上限额度";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }

                if (body.OrderAmount < CompanyServiceModel.MinOnceAmount)
                {
                    PayDB.InsertDownOrderTransferLog("商户低于下限额度", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "商户低于下限额度";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }
            }

            #endregion

            #region 公司每日用量檢查
            //尚未有紀錄 => 尚無用量
            if (CompanyModel.CompanyType != 4)
            {
                DT = PayDB.GetCompanySummary(SummaryDate, CompanyModel.CompanyID, body.CurrencyType, body.ServiceType);
                decimal CompanyBeforeAmount = 0;
                if (DT != null && DT.Rows.Count > 0)
                {
                    CompanyBeforeAmount = (decimal)DT.Rows[0]["SummaryAmount"];
                }
                else
                {
                    CompanyBeforeAmount = 0;
                }

                //MaxDaliyAmount = 0 代表無上限
                if (CompanyServiceModel.MaxDaliyAmount != 0)
                {
                    if ((body.OrderAmount + CompanyBeforeAmount) > CompanyServiceModel.MaxDaliyAmount)
                    {
                        PayDB.InsertDownOrderTransferLog("商户每日用量不足", 0, "", body.OrderID, body.CompanyCode, true);
                        paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                        paymentResult.Message = "商户每日用量不足";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                        return response;
                    }
                }
            }

            #endregion

            #endregion

            #region 供應商相關檢查

            #region 設定的供應商

            //if (CompanyModel.InsideLevel != 0) {
            // DT = PayDB.GetTopParentGPayRelation(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType, CompanyModel.SortKey);
            //} else {
            DT = RedisCache.GPayRelation.GetGPayRelation(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType);
            //}

            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("尚未设定对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "尚未设定对应上游";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }

            GPayRelationModels = GatewayCommon.ToList<GatewayCommon.GPayRelation>(DT);

            #endregion

            #region 供應商Service檢查
            bool checkProviderServiceExist = false;

            foreach (var model in GPayRelationModels)
            {
                GatewayCommon.ProviderService providerServiceModel;
                //DT = PayDB.GetProviderServiceByProviderServiceType(model.ProviderCode, body.ServiceType, body.CurrencyType, false);
                DT = RedisCache.ProviderService.GetProviderService(model.ProviderCode, body.ServiceType, body.CurrencyType);
                if (DT != null && DT.Rows.Count > 0)
                {
                    providerServiceModel = GatewayCommon.ToList<GatewayCommon.ProviderService>(DT).FirstOrDefault();
                }
                else
                {
                    providerServiceModel = null;
                }

                if (providerServiceModel != null && providerServiceModel.State == 0)
                {

                    ProviderDT = null;
                    ProviderDT = RedisCache.ProviderCode.GetProviderCode(providerServiceModel.ProviderCode);

                    if (ProviderDT != null && DT.Rows.Count > 0)
                    {
                        if (ProviderDT.Rows[0]["ProviderState"].ToString() == "0")
                        {
                            if (((int)ProviderDT.Rows[0]["ProviderAPIType"] & 1) == 1)
                            {
                                GPaySelectModels.Add(new Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>(providerServiceModel, model));
                            }


                        }
                    }
                }
            }

            if (GPaySelectModels.Count > 0)
            {
                checkProviderServiceExist = true;
            }

            if (!checkProviderServiceExist)
            {
                PayDB.InsertDownOrderTransferLog("选择不到对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "选择不到对应上游";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            //撈取啟用狀態的相關供應商渠道
            //DT = PayDB.GetProviderServiceByServiceType(body.ServiceType, body.CurrencyType, false);

            //考慮到未來使用Redis之可能，不在SQL中加入OrderAmount相關的條件

            #region 檢查供應商的單筆上下限制
            if (CompanyModel.CompanyType != 4) {

                GPaySelectModels = GPaySelectModels.Where(x =>
                {
                    //檢查上下限制
                    if (body.OrderAmount > x.Item1.MaxOnceAmount || body.OrderAmount < x.Item1.MinOnceAmount)
                    {
                        return false;
                    }
                    return true;
                }).ToList();

                if (GPaySelectModels.Count == 0)
                {
                    PayDB.InsertDownOrderTransferLog("上游限额错误", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "上游限额错误";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }
            }

            #endregion

            #region 檢查供應商的每日用量
            if (CompanyModel.CompanyType != 4)
            {
                GPaySelectModels = GPaySelectModels.Where(x =>
                {
                    System.Data.DataTable ProviderSummaryDT;
                    //檢查上下限制
                    //檢查每日用量
                    //****
                    if (x.Item1.MaxDaliyAmount == 0)
                    {
                        return true;
                    }

                    ProviderSummaryDT = PayDB.GetProviderSummary(SummaryDate, x.Item1.ProviderCode, x.Item1.CurrencyType, x.Item1.ServiceType);
                    decimal ProviderBeforeAmount = 0;


                    if (ProviderSummaryDT != null && ProviderSummaryDT.Rows.Count > 0)
                    {
                        ProviderBeforeAmount = (decimal)ProviderSummaryDT.Rows[0]["SummaryAmount"];
                    }
                    else
                    {
                        ProviderBeforeAmount = 0;
                    }

                    if ((body.OrderAmount + ProviderBeforeAmount) > x.Item1.MaxDaliyAmount)
                    {
                        return false;
                    }
                    return true;
                }).ToList();

                if (GPaySelectModels.Count == 0)
                {
                    PayDB.InsertDownOrderTransferLog("上游每日用量不足", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "上游每日用量不足";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }
            }

            #endregion

            selectProviderIndex = GatewayCommon.SelectProviderService(GPaySelectModels);
            #region 取得供應商相關資料
            DT = RedisCache.ProviderCode.GetProviderCode(GPaySelectModels[selectProviderIndex].Item1.ProviderCode);
            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("选择不到对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "选择不到对应上游";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            else
            {
                if ((int)DT.Rows[0]["ProviderState"] == 1)
                {
                    PayDB.InsertDownOrderTransferLog("上游已关闭", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "上游已关闭";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }

                if (((int)DT.Rows[0]["ProviderAPIType"] & 1) != 1)
                {
                    PayDB.InsertDownOrderTransferLog("上游未开启充值权限", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "上游未开启充值权限";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }

            }

            ProviderModel = GatewayCommon.ToList<GatewayCommon.Provider>(DT).FirstOrDefault();
            #endregion

            //取得供應商相關資料

            #endregion

            #region 其他檢查(待補)
            //BankCode相關

            //if (body.ServiceType == "OB002") {
            //    DT = RedisCache.BankCode.GetBankCode();

            //    bool isBankCodeExist = false;

            //    if (string.IsNullOrEmpty(body.BankCode)) {
            //        isBankCodeExist = true;
            //    } else {
            //        for (int i = 0 ; i < DT.Rows.Count ; i++) {
            //            if (DT.Rows[i]["BankCode"].ToString() == body.BankCode && (int)DT.Rows[i]["BankState"] == 0) {
            //                isBankCodeExist = true;
            //                break;
            //            }
            //        }
            //    }

            //    if (!isBankCodeExist) {
            //        response = Request.CreateResponse(HttpStatusCode.Moved);
            //        response.Headers.Location = new Uri(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidBankCode);
            //        return response;
            //    }
            //}                     
            #endregion

            #region 建立交易單
            int PaymentID;


            //HttpContext.Current.Response.Write(body.ServiceType);
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();

            //return response;

            //產生交易單model
            PaymentModel = new GatewayCommon.Payment()
            {
                forCompanyID = CompanyModel.CompanyID,
                CurrencyType = body.CurrencyType,
                ServiceType = body.ServiceType,
                BankCode = string.IsNullOrEmpty(body.BankCode) ? "" : body.BankCode,
                ProviderCode = GPaySelectModels[selectProviderIndex].Item1.ProviderCode,
                ReturnURL = redirectURL,
                State = string.IsNullOrEmpty(body.State) ? "" : body.State,
                ClientIP = body.ClientIP,
                UserIP = CodingControl.GetUserIP(),
                OrderID = body.OrderID,
                OrderDate = body.OrderDate,
                OrderAmount = Math.Truncate(body.OrderAmount),
                CostRate = GPaySelectModels[selectProviderIndex].Item1.CostRate,
                CostCharge = GPaySelectModels[selectProviderIndex].Item1.CostCharge,
                CollectRate =CompanyModel.CompanyType==4? GPaySelectModels[selectProviderIndex].Item1.CostRate : CompanyServiceModel.CollectRate,
                CollectCharge =CompanyModel.CompanyType==4? GPaySelectModels[selectProviderIndex].Item1.CostCharge : CompanyServiceModel.CollectCharge,
                UserName = body.UserName
            };

            //if (ProviderModel.CollectType == 0) {

            //} else if (ProviderModel.CollectType == 1) {
            //    PaymentModel.CollectRate = GPaySelectModels[selectProviderIndex].Item1.CostRate;
            //    PaymentModel.CollectCharge = GPaySelectModels[selectProviderIndex].Item1.CostCharge;
            //}


            PaymentID = PayDB.InsertPayment(PaymentModel);

            if (PaymentID == 0)
            {
                PayDB.InsertDownOrderTransferLog("建立订单失败", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "建立订单失败";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }

            PaymentModel.PaymentID = PaymentID;
            #endregion
        }

        #region 新建單 => 尚未提交
        if (CompanyModel.CompanyType != 4)
        {
            PaymentSerial = "IP" + System.DateTime.Now.ToString("yyyyMMddHHmm") + (new string('0', 10 - PaymentModel.PaymentID.ToString().Length) + PaymentModel.PaymentID.ToString());
        }
        else {
            PaymentSerial = PaymentModel.OrderID;
        }
    
        if (PayDB.UpdatePaymentSerial(PaymentSerial, PaymentModel.PaymentID) == 0)
        {
            PayDB.InsertDownOrderTransferLog("建立订单号失败", 0, PaymentSerial, body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "建立订单号失败";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        PayDB.SetUpdateSummaryCount(CompanyModel.CompanyID, PaymentModel.ProviderCode, PaymentModel.CurrencyType, PaymentModel.ServiceType);

        PaymentModel.PaymentSerial = PaymentSerial;
        #endregion

       
        var providerRequestData = GatewayCommon.GetProviderRequestData2(PaymentModel);
        if (string.IsNullOrEmpty(providerRequestData.ProviderUrl))
        {
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "取得充值網址失敗";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));

            return response;
        }

        PayDB.InsertDownOrderTransferLog("充值订单完成:", 0, PaymentSerial, body.OrderID, body.CompanyCode, false);

        paymentResult.Status = GatewayCommon.ResultStatus.OK;
        paymentResult.Message = providerRequestData.ProviderUrl;
        paymentResult.Code = PaymentModel.ProviderCode;
        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));

        return response;
    }


    [HttpPost]
    [ActionName("RequirePayingReturnUrl2")]
    public HttpResponseMessage RequirePayingReturnUrl2([FromBody] FromBodyRequirePaying frombody)
    {
        FromBodyRequirePayment body = new FromBodyRequirePayment();

        #region 回傳相關
        HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
        GatewayCommon.ReturnByRequirePayment paymentResult = new GatewayCommon.ReturnByRequirePayment() { Status = GatewayCommon.ResultStatus.ERR };

        //response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        #endregion

        if (frombody == null)
        {
            PayDB.InsertDownOrderTransferLog("未带入参数", 0, "", "", "", true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "未带入参数";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));

            return response;
        }
        else
        {
            body.CompanyCode = frombody.ManageCode;
            body.CurrencyType = frombody.Currency;
            body.ServiceType = frombody.Service;
            body.BankCode = frombody.BankCode;
            body.ClientIP = frombody.CustomerIP;
            body.OrderID = frombody.OrderID;
            body.OrderDate = frombody.OrderDate;
            body.OrderAmount = frombody.OrderAmount;
            body.ReturnURL = frombody.RevolveURL;
            body.State = frombody.State;
            body.SelCurrency = frombody.SelCurrency;
            body.AssignAmount = frombody.AllotAmount;
            body.Sign = frombody.Sign;
            body.ProviderCode = frombody.ProviderCode;
            body.UserName = HttpUtility.UrlDecode(frombody.UserName);
        }

        if (string.IsNullOrEmpty(body.OrderID))
        {
            PayDB.InsertDownOrderTransferLog("未带入 OrderID", 0, "", "", "", true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "未带入 OrderID";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        if (string.IsNullOrEmpty(body.CompanyCode))
        {
            PayDB.InsertDownOrderTransferLog("未带入商戶代碼", 0, "", "", "", true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "未带入商戶代碼";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        if (string.IsNullOrEmpty(body.ProviderCode))
        {
            PayDB.InsertDownOrderTransferLog("未带入供應商代碼", 0, "", "", "", true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "未带入供應商代碼";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        PayDB.InsertDownOrderTransferLog("充值申请:" + JsonConvert.SerializeObject(body), 0, "", body.OrderID, body.CompanyCode, false);
        string redirectURL = string.Empty;
        System.Data.DataTable DT;
        DateTime SummaryDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

        //公司相關變數
        GatewayCommon.Company CompanyModel;
        GatewayCommon.CompanyService CompanyServiceModel;

        //供應商相關 
        System.Data.DataTable ProviderDT;
        GatewayCommon.Provider ProviderModel;
        IList<GatewayCommon.GPayRelation> GPayRelationModels;
        List<Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>> GPaySelectModels = new List<Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>>();
        int selectProviderIndex = 0;
        //交易單相關
        GatewayCommon.Payment PaymentModel;
        string PaymentSerial;
        #region 黑名單
        if (PayDB.GetBlackListCountResult(CodingControl.GetUserIP(), "", "", "Payment") > 0)
        {
            PayDB.InsertDownOrderTransferLog("错误，黑名单成员。IP：" + CodingControl.GetUserIP(), 0, body.OrderID, body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "错误，黑名单成员。IP：" + CodingControl.GetUserIP();
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        if (!string.IsNullOrEmpty(body.UserName))
        {
            if (PayDB.CheckPaymentUserName(body.UserName) > 0)
            {
                PayDB.InsertDownOrderTransferLog("错误，黑名单成员。会员名称：" + body.UserName, 0, body.OrderID, body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "错误，黑名单成员。会员名称：" + body.UserName;
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
        }
        #endregion

        #region 公司檢查
        //DT = PayDB.GetCompanyByCode(body.CompanyCode, true);
        DT = RedisCache.Company.GetCompanyByCode(body.CompanyCode);
        if (!(DT != null && DT.Rows.Count > 0))
        {
            PayDB.InsertDownOrderTransferLog("公司代码不存在", 0, "", body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "公司代码不存在" + body.UserName;
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }
        #endregion

        CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(DT).FirstOrDefault();

        #region 簽名檢查
        var sign = GatewayCommon.GetGPaySign(body.OrderID, body.OrderAmount, body.OrderDate, body.ServiceType, body.CurrencyType, body.CompanyCode, CompanyModel.CompanyKey);

        if (sign.ToUpper() != body.Sign.ToUpper())
        {
            PayDB.InsertDownOrderTransferLog("签名错误", 0, "", body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "签名错误";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        #endregion

        //查看過去是否已經有建單紀錄
        DT = PayDB.GetPaymentByCompanyOrderID(CompanyModel.CompanyID, body.OrderID);

        if (DT != null && DT.Rows.Count > 0)
        {

            #region 檢查之前存在的單是否為"新建"單

            PaymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();


            PayDB.InsertDownOrderTransferLog("订单已存在", 0, "", body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "订单已存在";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;

            #endregion

        }
        else
        {

            #region 營運商相關檢查

            #region 公司狀態檢查
            if (!(CompanyModel.CompanyState == 0))
            {
                PayDB.InsertDownOrderTransferLog("商户已停用", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "商户已停用";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            #region 公司幣別檢查
            DT = RedisCache.CompanyPoint.GetCompanyPointByID(CompanyModel.CompanyID);
            if (!(DT != null && DT.Select("CurrencyType='" + body.CurrencyType + "'").Length > 0))
            {
                PayDB.InsertDownOrderTransferLog("商户币别错误", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "商户币别错误";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            #region 公司可用渠道檢查
            DT = RedisCache.CompanyService.GetCompanyService(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType);

            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("没有公司可用渠道", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "没有公司可用渠道";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            if (string.IsNullOrEmpty(body.ReturnURL))
            {
                if (string.IsNullOrEmpty(CompanyModel.URL))
                {
                    response = Request.CreateResponse(HttpStatusCode.Moved);
                    response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.GetRedirectUrlFail, UriKind.Relative);
                    return response;
                }
                else
                {
                    redirectURL = CompanyModel.URL;
                }
            }
            else
            {
                redirectURL = body.ReturnURL;
            }


            CompanyServiceModel = GatewayCommon.ToList<GatewayCommon.CompanyService>(DT).FirstOrDefault();

            //營運商渠道停用檢查
            if (CompanyServiceModel.State == 1)
            {

                PayDB.InsertDownOrderTransferLog("商户渠道已停用", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "商户渠道已停用";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }

            #region 公司單筆上下限制檢查

            if (CompanyModel.CompanyType != 4)
            {
                if (body.OrderAmount > CompanyServiceModel.MaxOnceAmount)
                {
                    PayDB.InsertDownOrderTransferLog("商户超过上限额度", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "商户超过上限额度";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }

                if (body.OrderAmount < CompanyServiceModel.MinOnceAmount)
                {
                    PayDB.InsertDownOrderTransferLog("商户低于下限额度", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "商户低于下限额度";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }
            }

            #endregion

            #region 公司每日用量檢查
            //尚未有紀錄 => 尚無用量

            DT = PayDB.GetCompanySummary(SummaryDate, CompanyModel.CompanyID, body.CurrencyType, body.ServiceType);
            decimal CompanyBeforeAmount = 0;
            if (DT != null && DT.Rows.Count > 0)
            {
                CompanyBeforeAmount = (decimal)DT.Rows[0]["SummaryAmount"];
            }
            else
            {
                CompanyBeforeAmount = 0;
            }

            //MaxDaliyAmount=0 代表無上限
            if (CompanyServiceModel.MaxDaliyAmount != 0)
            {
                if ((body.OrderAmount + CompanyBeforeAmount) > CompanyServiceModel.MaxDaliyAmount)
                {
                    PayDB.InsertDownOrderTransferLog("商户每日用量不足", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "商户每日用量不足";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }
            }

            #endregion

            #endregion

            #region 供應商相關檢查

            #region 設定的供應商

            //if (CompanyModel.InsideLevel != 0) {
            // DT = PayDB.GetTopParentGPayRelation(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType, CompanyModel.SortKey);
            //} else {
            DT = RedisCache.GPayRelation.GetGPayRelation(CompanyModel.CompanyID, body.ServiceType, body.CurrencyType);
            //}

            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("尚未设定对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "尚未设定对应上游";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }

            GPayRelationModels = GatewayCommon.ToList<GatewayCommon.GPayRelation>(DT);

            #endregion

            var model = GPayRelationModels.Where(w => w.ProviderCode == body.ProviderCode).FirstOrDefault();

            if (model==null)
            {
                PayDB.InsertDownOrderTransferLog("尚未设定对应上游2:"+JsonConvert.SerializeObject(GPayRelationModels), 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "尚未设定对应上游";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }

            #region 供應商Service檢查
            bool checkProviderServiceExist = false;

            GatewayCommon.ProviderService providerServiceModel;
            DT = RedisCache.ProviderService.GetProviderService(body.ProviderCode, body.ServiceType, body.CurrencyType);
            if (DT != null && DT.Rows.Count > 0)
            {
                providerServiceModel = GatewayCommon.ToList<GatewayCommon.ProviderService>(DT).FirstOrDefault();
            }
            else
            {
                providerServiceModel = null;
            }

            if (providerServiceModel != null && providerServiceModel.State == 0)
            {

                ProviderDT = null;
                ProviderDT = RedisCache.ProviderCode.GetProviderCode(providerServiceModel.ProviderCode);

                if (ProviderDT != null && DT.Rows.Count > 0)
                {
                    if (ProviderDT.Rows[0]["ProviderState"].ToString() == "0")
                    {
                        if (((int)ProviderDT.Rows[0]["ProviderAPIType"] & 1) == 1)
                        {
                            GPaySelectModels.Add(new Tuple<GatewayCommon.ProviderService, GatewayCommon.GPayRelation>(providerServiceModel, model));
                        }
                    }
                }
            }
       
            if (GPaySelectModels.Count > 0)
            {
                checkProviderServiceExist = true;
            }

            if (!checkProviderServiceExist)
            {
                PayDB.InsertDownOrderTransferLog("选择不到对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "选择不到对应上游";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            //撈取啟用狀態的相關供應商渠道
            //DT = PayDB.GetProviderServiceByServiceType(body.ServiceType, body.CurrencyType, false);

            //考慮到未來使用Redis之可能，不在SQL中加入OrderAmount相關的條件

            #region 檢查供應商的單筆上下限制
            GPaySelectModels = GPaySelectModels.Where(x =>
            {
                //檢查上下限制
                if (body.OrderAmount > x.Item1.MaxOnceAmount || body.OrderAmount < x.Item1.MinOnceAmount)
                {
                    return false;
                }
                return true;
            }).ToList();

            if (GPaySelectModels.Count == 0)
            {
                PayDB.InsertDownOrderTransferLog("上游限额错误", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "上游限额错误";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            #region 檢查供應商的每日用量
            GPaySelectModels = GPaySelectModels.Where(x =>
            {
                System.Data.DataTable ProviderSummaryDT;
                //檢查上下限制
                //檢查每日用量
                //****
                if (x.Item1.MaxDaliyAmount == 0)
                {
                    return true;
                }

                ProviderSummaryDT = PayDB.GetProviderSummary(SummaryDate, x.Item1.ProviderCode, x.Item1.CurrencyType, x.Item1.ServiceType);
                decimal ProviderBeforeAmount = 0;


                if (ProviderSummaryDT != null && ProviderSummaryDT.Rows.Count > 0)
                {
                    ProviderBeforeAmount = (decimal)ProviderSummaryDT.Rows[0]["SummaryAmount"];
                }
                else
                {
                    ProviderBeforeAmount = 0;
                }

                if ((body.OrderAmount + ProviderBeforeAmount) > x.Item1.MaxDaliyAmount)
                {
                    return false;
                }
                return true;
            }).ToList();

            if (GPaySelectModels.Count == 0)
            {
                PayDB.InsertDownOrderTransferLog("上游每日用量不足", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "上游每日用量不足";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            #endregion

            selectProviderIndex = GatewayCommon.SelectProviderService(GPaySelectModels);
            #region 取得供應商相關資料
            DT = RedisCache.ProviderCode.GetProviderCode(GPaySelectModels[selectProviderIndex].Item1.ProviderCode);
            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("选择不到对应上游", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "选择不到对应上游";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }
            else
            {
                if ((int)DT.Rows[0]["ProviderState"] == 1)
                {
                    PayDB.InsertDownOrderTransferLog("上游已关闭", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "上游已关闭";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }

                if (((int)DT.Rows[0]["ProviderAPIType"] & 1) != 1)
                {
                    PayDB.InsertDownOrderTransferLog("上游未开启充值权限", 0, "", body.OrderID, body.CompanyCode, true);
                    paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                    paymentResult.Message = "上游未开启充值权限";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                    return response;
                }

            }

            ProviderModel = GatewayCommon.ToList<GatewayCommon.Provider>(DT).FirstOrDefault();
            #endregion

            //取得供應商相關資料

            #endregion

            #region 其他檢查(待補)
            //BankCode相關

            //if (body.ServiceType == "OB002") {
            //    DT = RedisCache.BankCode.GetBankCode();

            //    bool isBankCodeExist = false;

            //    if (string.IsNullOrEmpty(body.BankCode)) {
            //        isBankCodeExist = true;
            //    } else {
            //        for (int i = 0 ; i < DT.Rows.Count ; i++) {
            //            if (DT.Rows[i]["BankCode"].ToString() == body.BankCode && (int)DT.Rows[i]["BankState"] == 0) {
            //                isBankCodeExist = true;
            //                break;
            //            }
            //        }
            //    }

            //    if (!isBankCodeExist) {
            //        response = Request.CreateResponse(HttpStatusCode.Moved);
            //        response.Headers.Location = new Uri(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Result.cshtml?ResultCode=" + (int)ResultCode.InvalidBankCode);
            //        return response;
            //    }
            //}                     
            #endregion

            #region 建立交易單
            int PaymentID;


            //HttpContext.Current.Response.Write(body.ServiceType);
            //HttpContext.Current.Response.Flush();
            //HttpContext.Current.Response.End();

            //return response;

            //產生交易單model
            PaymentModel = new GatewayCommon.Payment()
            {
                forCompanyID = CompanyModel.CompanyID,
                CurrencyType = body.CurrencyType,
                ServiceType = body.ServiceType,
                BankCode = string.IsNullOrEmpty(body.BankCode) ? "" : body.BankCode,
                ProviderCode = body.ProviderCode,
                ReturnURL = redirectURL,
                State = string.IsNullOrEmpty(body.State) ? "" : body.State,
                ClientIP = body.ClientIP,
                UserIP = CodingControl.GetUserIP(),
                OrderID = body.OrderID,
                OrderDate = body.OrderDate,
                OrderAmount = Math.Truncate(body.OrderAmount),
                CostRate = GPaySelectModels[selectProviderIndex].Item1.CostRate,
                CostCharge = GPaySelectModels[selectProviderIndex].Item1.CostCharge,
                CollectRate = CompanyModel.CompanyType == 4 ? GPaySelectModels[selectProviderIndex].Item1.CostRate : CompanyServiceModel.CollectRate,
                CollectCharge = CompanyModel.CompanyType == 4 ? GPaySelectModels[selectProviderIndex].Item1.CostCharge : CompanyServiceModel.CollectCharge,
                UserName = body.UserName
            };

            //if (ProviderModel.CollectType == 0) {

            //} else if (ProviderModel.CollectType == 1) {
            //    PaymentModel.CollectRate = GPaySelectModels[selectProviderIndex].Item1.CostRate;
            //    PaymentModel.CollectCharge = GPaySelectModels[selectProviderIndex].Item1.CostCharge;
            //}


            PaymentID = PayDB.InsertPayment(PaymentModel);

            if (PaymentID == 0)
            {
                PayDB.InsertDownOrderTransferLog("建立订单失败", 0, "", body.OrderID, body.CompanyCode, true);
                paymentResult.Status = GatewayCommon.ResultStatus.ERR;
                paymentResult.Message = "建立订单失败";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
                return response;
            }

            PaymentModel.PaymentID = PaymentID;
            #endregion
        }

        #region 新建單 => 尚未提交
        if (CompanyModel.CompanyType != 4)
        {
            PaymentSerial = "IP" + System.DateTime.Now.ToString("yyyyMMddHHmm") + (new string('0', 10 - PaymentModel.PaymentID.ToString().Length) + PaymentModel.PaymentID.ToString());
        }
        else
        {
            PaymentSerial = PaymentModel.OrderID;
        }

        if (PayDB.UpdatePaymentSerial(PaymentSerial, PaymentModel.PaymentID) == 0)
        {
            PayDB.InsertDownOrderTransferLog("建立订单号失败", 0, PaymentSerial, body.OrderID, body.CompanyCode, true);
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "建立订单号失败";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));
            return response;
        }

        PayDB.SetUpdateSummaryCount(CompanyModel.CompanyID, PaymentModel.ProviderCode, PaymentModel.CurrencyType, PaymentModel.ServiceType);

        PaymentModel.PaymentSerial = PaymentSerial;
        #endregion


        var providerRequestData = GatewayCommon.GetProviderRequestData2(PaymentModel);
        if (string.IsNullOrEmpty(providerRequestData.ProviderUrl))
        {
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = "取得充值網址失敗";
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));

            return response;
        }

        if (providerRequestData.ProviderUrl.Length >= 6&& providerRequestData.ProviderUrl.Substring(0, 6)== "error:")
        {
            paymentResult.Status = GatewayCommon.ResultStatus.ERR;
            paymentResult.Message = providerRequestData.ProviderUrl.Substring(6);
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));

            return response;
        }

        PayDB.InsertDownOrderTransferLog("充值订单完成:", 0, PaymentSerial, body.OrderID, body.CompanyCode, false);

        paymentResult.Status = GatewayCommon.ResultStatus.OK;
        paymentResult.Message = providerRequestData.ProviderUrl;
        paymentResult.Code = PaymentModel.ProviderCode;
        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(paymentResult));

        return response;
    }

    [HttpPost]
    [ActionName("SavePayingUserName")]
    public HttpResponseMessage SavePayingUserName([FromBody] FromBodySavePayingUserName frombody) {

        HttpResponseMessage response = null;
        
        System.Data.DataTable DT = PayDB.GetPaymentByPaymentID(frombody.PaymentID);
        GatewayCommon.Payment paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();

        if (PayDB.CheckPaymentUserName(frombody.UserName.Trim()) > 0)
        {
            PayDB.InsertDownOrderTransferLog("错误，黑名单成员。会员名称：" + frombody.UserName.Trim(), 0, paymentModel.PaymentSerial, paymentModel.OrderID, "", true);
            response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InsertUserNameError, UriKind.Relative);
            return response;
        }

        if (PayDB.UpdatePaymentUserName(frombody.PaymentID, frombody.UserName) == 0)
        {
            PayDB.InsertDownOrderTransferLog("新增用户名失败", 0, paymentModel.PaymentSerial, paymentModel.OrderID, "", true);
            response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/Result.cshtml?ResultCode=" + (int)ResultCode.InsertUserNameError, UriKind.Relative);
            return response;
        }
        else {
            response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/RedirectView/GatewayRedirect.cshtml?PaymentID=" + paymentModel.PaymentID, UriKind.Relative);
            return response;
        }
    }

    [HttpPost]
    [ActionName("QueryPaying")]
    public PayingResult QueryPaying([FromBody] FromBodyQueryPaying frombody)
    {
        PayingResult Ret = new PayingResult(); ;
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable CompanyDT;
        System.Data.DataTable DT;
        string signStr;
        string sign;
        string companyKey;
        GatewayCommon.Payment payment;

        FromBodyQueryPayment body = new FromBodyQueryPayment();
        body.CompanyCode = frombody.ManageCode;
        body.PaymentSerial = frombody.PayingSerial;
        body.Sign = frombody.Sign;

        CompanyDT = RedisCache.Company.GetCompanyByCode(body.CompanyCode);
        if (CompanyDT.Rows.Count > 0)
        {
            companyKey = CompanyDT.Rows[0]["CompanyKey"].ToString();
            #region 簽名檢查
            signStr = string.Format("ManageCode={0}&PayingSerial={1}&CompanyKey={2}", body.CompanyCode, body.PaymentSerial, companyKey);
            sign = CodingControl.GetSHA256(signStr, false);
            #endregion

            if (sign.ToUpper() == body.Sign.ToUpper())
            {
                SS = "SELECT * FROM PaymentTable WITH (NOLOCK) WHERE forCompanyID=@CompanyID AND PaymentSerial=@PaymentSerial";
                DBCmd = new System.Data.SqlClient.SqlCommand();
                DBCmd.CommandText = SS;
                DBCmd.CommandType = System.Data.CommandType.Text;
                DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = (int)CompanyDT.Rows[0]["CompanyID"];
                DBCmd.Parameters.Add("@PaymentSerial", System.Data.SqlDbType.VarChar).Value = body.PaymentSerial;
                DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);


                if (DT.Rows.Count > 0)
                {

                    payment = DT.ToList<GatewayCommon.Payment>().FirstOrDefault();
                    GatewayCommon.PaymentResultStatus paymentResultStatus;

                    if (payment.ProcessStatus == 2 || payment.ProcessStatus == 4)
                    {
                        paymentResultStatus = GatewayCommon.PaymentResultStatus.Successs;
                    }
                    else if (payment.ProcessStatus == 0 || payment.ProcessStatus == 1)
                    {
                        paymentResultStatus = GatewayCommon.PaymentResultStatus.PaymentProgress;
                    }
                    else if (payment.ProcessStatus == 3)
                    {
                        paymentResultStatus = GatewayCommon.PaymentResultStatus.Failure;
                    }
                    else
                    {
                        paymentResultStatus = GatewayCommon.PaymentResultStatus.ProblemPayment;
                    }

                    Ret.SetByPayment(payment, paymentResultStatus, body.CompanyCode, companyKey);

                }
                else
                {

                    Ret.Status = ResultStatus.ERR;
                    Ret = new PayingResult() { Status = ResultStatus.ERR, Message = "Invalid PayingSerial" };
                    Ret.PayingStatus = 1;
                }
            }
            else
            {


                Ret.Status = ResultStatus.ERR;
                Ret = new PayingResult() { Status = ResultStatus.ERR, Message = "SignError" };
                Ret.PayingStatus = 1;
            }
        }
        else
        {
            Ret = new PayingResult() { Status = ResultStatus.ERR, Message = "Invalid ManageCode" };
            Ret.PayingStatus = 1;
        }
        return Ret;
    }

    [HttpPost]
    [ActionName("QueryPayingByOrderID")]
    public PayingResult QueryPayingByOrderID([FromBody] FromBodyQueryPayingByOrderID frombody)
    {
        PayingResult Ret = new PayingResult();
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable CompanyDT;
        System.Data.DataTable DT;
        string signStr;
        string sign;
        string companyKey;
        GatewayCommon.Payment payment;

        CompanyDT = RedisCache.Company.GetCompanyByCode(frombody.ManageCode);
        if (CompanyDT.Rows.Count > 0)
        {
            companyKey = CompanyDT.Rows[0]["CompanyKey"].ToString();
            #region 簽名檢查
            signStr = string.Format("ManageCode={0}&OrderID={1}&CompanyKey={2}", frombody.ManageCode, frombody.OrderID, companyKey);
            sign = CodingControl.GetSHA256(signStr, false);
            #endregion

            if (sign.ToUpper() == frombody.Sign.ToUpper())
            {
                SS = "SELECT * FROM PaymentTable WITH (NOLOCK) WHERE forCompanyID=@CompanyID AND OrderID=@OrderID";
                DBCmd = new System.Data.SqlClient.SqlCommand();
                DBCmd.CommandText = SS;
                DBCmd.CommandType = System.Data.CommandType.Text;
                DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = (int)CompanyDT.Rows[0]["CompanyID"];
                DBCmd.Parameters.Add("@OrderID", System.Data.SqlDbType.VarChar).Value = frombody.OrderID;
                DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);


                if (DT.Rows.Count > 0)
                {

                    payment = DT.ToList<GatewayCommon.Payment>().FirstOrDefault();
                    GatewayCommon.PaymentResultStatus paymentResultStatus;

                    if (payment.ProcessStatus == 2 || payment.ProcessStatus == 4)
                    {
                        paymentResultStatus = GatewayCommon.PaymentResultStatus.Successs;
                    }
                    else if (payment.ProcessStatus == 0 || payment.ProcessStatus == 1)
                    {
                        paymentResultStatus = GatewayCommon.PaymentResultStatus.PaymentProgress;
                    }
                    else if (payment.ProcessStatus == 3)
                    {
                        paymentResultStatus = GatewayCommon.PaymentResultStatus.Failure;
                    }
                    else
                    {
                        paymentResultStatus = GatewayCommon.PaymentResultStatus.ProblemPayment;
                    }

                    Ret.SetByPayment(payment, paymentResultStatus, frombody.ManageCode, companyKey);

                }
                else
                {

                    Ret.Status = ResultStatus.ERR;
                    Ret = new PayingResult() { Status = ResultStatus.ERR, Message = "Invalid OrderID" };
                    Ret.PayingStatus = 1;
                }
            }
            else
            {


                Ret.Status = ResultStatus.ERR;
                Ret = new PayingResult() { Status = ResultStatus.ERR, Message = "SignError" };
                Ret.PayingStatus = 1;
            }
        }
        else
        {
            Ret = new PayingResult() { Status = ResultStatus.ERR, Message = "Invalid ManageCode" };
            Ret.PayingStatus = 1;
        }
        return Ret;
    }

    #endregion

    #region Withdraw

    [HttpPost]
    [ActionName("RequireWithdraw")]
    public HttpResponseMessage RequireWithdraw([FromBody] FromBodyWithdrawRequire frombody)
    {
        //发生 Exception时 将订单改为失败单
        int ExceptionWithdrawID = -1;
        FromBodyRequireWithdraw body = new FromBodyRequireWithdraw();
        if (frombody != null)
        {
            body.CompanyCode = frombody.ManageCode;
            body.CurrencyType = frombody.Currency;
            body.OrderAmount = frombody.OrderAmount;
            body.BankCard = frombody.BankCard;
            body.BankCardName = frombody.BankCardName;
            body.BankName = frombody.BankName;
            body.BankBranchName = frombody.BankComponentName;
            body.OwnProvince = frombody.OwnProvince;
            body.OwnCity = frombody.OwnCity;
            body.OrderID = frombody.OrderID;
            body.OrderDate = frombody.OrderDate;
            body.ReturnUrl = frombody.RevolveUrl;
            body.ClientIP = frombody.ClientIP;
            body.Sign = frombody.Sign;
            body.State = frombody.State;
            
        }

        try
        {
            #region 回傳相關
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            GatewayCommon.ReturnByRequireWithdraw withdrawResult = new GatewayCommon.ReturnByRequireWithdraw() { Status = GatewayCommon.ResultStatus.ERR };

            //response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            #endregion

            string redirectURL = string.Empty;
            System.Data.DataTable DT;
            DateTime SummaryDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

            IList<GatewayCommon.GPayWithdrawRelation> GPayWithdrawRelationModels;
            GatewayCommon.GPayWithdrawRelation GPayWithdrawRelationModel;
            //公司相關變數
            GatewayCommon.Company CompanyModel;
            GatewayCommon.ProxyProvider ProxyProviderModel = null;
            GatewayCommon.CompanyPoint CompanyPointModel;
            GatewayCommon.CompanyServicePoint CompanyServicePointModel;
            GatewayCommon.WithdrawLimit CompanyWithdrawLimitModel;
            List<GatewayCommon.WithdrawLimit> LstCompanyWithdrawLimitModel;
            GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
            //交易單相關
            GatewayCommon.Withdrawal WithdrawModel;
            string WithdrawSerial;
            int GroupID = 0;
            GatewayCommon.ReturnWithdrawByProvider withdrawReturn;

            List<Tuple<GatewayCommon.WithdrawLimit, GatewayCommon.GPayRelation, GatewayCommon.Provider>> GPaySelectModels = new List<Tuple<GatewayCommon.WithdrawLimit, GatewayCommon.GPayRelation, GatewayCommon.Provider>>();
            //供应商相关
            GatewayCommon.WithdrawLimit withdrawLimitModel = null;
            GatewayCommon.Provider provider = null;



            //API送出相關
            if (string.IsNullOrEmpty(body.CompanyCode))
            {
                PayDB.InsertDownOrderTransferLog("商户代码不得为空", 2, "", "", "", true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户代码不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.OrderID))
            {
                PayDB.InsertDownOrderTransferLog("商户订单号不得为空", 2, "", "", body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户订单号不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            #region 渠道总开关检查
            DT = RedisCache.WebSetting.GetWebSetting("WithdrawOption");
            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("未开启代付功能", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "未开启代付功能";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (DT.Rows[0]["SettingValue"].ToString() == "1")
            {
                PayDB.InsertDownOrderTransferLog("代付功能关闭中", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "代付功能关闭中";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion



            PayDB.InsertDownOrderTransferLog("代付申请:" + JsonConvert.SerializeObject(body) + ",IP:" + CodingControl.GetUserIP(), 2, "", body.OrderID, body.CompanyCode, false);

            if (string.IsNullOrEmpty(body.ClientIP))
            {
                PayDB.InsertDownOrderTransferLog("提单会员IP不得为空", 2, "", "", body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "提单会员IP不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }





            #region 传入参数检查

            if (string.IsNullOrEmpty(body.BankCard))
            {
                PayDB.InsertDownOrderTransferLog("银行卡号不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "银行卡号不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.BankName))
            {
                PayDB.InsertDownOrderTransferLog("银行名称不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "银行名称不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.BankCardName))
            {
                PayDB.InsertDownOrderTransferLog("开户名不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "开户名不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.BankBranchName))
            {
                PayDB.InsertDownOrderTransferLog("支行名称不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "支行名称不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.OwnProvince))
            {
                PayDB.InsertDownOrderTransferLog("省份不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "省份不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.OwnCity))
            {
                PayDB.InsertDownOrderTransferLog("城市名称不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "城市名称不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion

            if (PayDB.CheckWitdrawal(body.BankCard.Trim(), body.BankCardName.Trim(), body.BankName.Trim()) > 0)
            {
                PayDB.InsertDownOrderTransferLog("5分钟内只能提交一张相同银行卡资讯订单", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "5分钟内只能提交一张相同银行卡资讯订单";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                PayDB.InsertRiskControlWithdrawal(body.CompanyCode, body.BankCard, body.BankCardName, body.BankName);
                return response;
            }

            //#region 黑名單
            if (PayDB.GetBlackListCountResult(CodingControl.GetUserIP(), body.BankCard, body.BankCardName, "Withdraw") > 0)
            {
                PayDB.InsertDownOrderTransferLog("错误，黑名单成员。卡号：" + body.BankCard + ",开户名：" + body.BankCardName, 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "错误，黑名单成员。卡号：" + body.BankCard + ",开户名：" + body.BankCardName;
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            //#endregion


            #region 公司檢查
            //DT = PayDB.GetCompanyByCode(body.CompanyCode, true);
            DT = RedisCache.Company.GetCompanyByCode(body.CompanyCode);
            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("商户代码有误", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户代码有误";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion

            CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(DT).FirstOrDefault();


            #region API提单权限检查
            if (!((CompanyModel.WithdrawAPIType & 2) == 2))
            {
                PayDB.InsertDownOrderTransferLog("没有API代付权限", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "没有API代付权限";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion

            #region 白名單
            if (!Pay.IsTestSite)
            {
                var CheckXForwardedResult = CodingControl.CheckXForwardedFor2();

                if (CheckXForwardedResult.Item1)
                {
                    foreach (var checkIpResult in CheckXForwardedResult.Item2)
                    {
                        if (!checkIpResult.Item2)
                        {
                            //沒有在返代ip內 檢查是否在白名單內
                            if (PayDB.GetWithdrawalIP(checkIpResult.Item1, body.CompanyCode) <= 0)
                            {
                                PayDB.InsertDownOrderTransferLog(
                                 "XForwarder:" + (string.IsNullOrEmpty(HttpContext.Current.Request.Headers["X-Forwarded-For"]) ? "空值" : HttpContext.Current.Request.Headers["X-Forwarded-For"])
                                 + ";UserHostAddress:" + (string.IsNullOrEmpty(HttpContext.Current.Request.UserHostAddress) ? "空值" : HttpContext.Current.Request.UserHostAddress)
                                 , 2, "", "", "55688", true);
                                PayDB.InsertDownOrderTransferLog("该IP未在白名单内。 IP：" + checkIpResult.Item1 + ",公司代码：" + body.CompanyCode, 2, "", body.OrderID, body.CompanyCode, true);
                                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                                withdrawResult.Message = "该IP未在白名单内。 IP：" + checkIpResult.Item1;
                                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                                return response;
                            }
                        }
                    }
                }
                else
                {

                    PayDB.InsertDownOrderTransferLog(
                        "XForwarder:" + (string.IsNullOrEmpty(HttpContext.Current.Request.Headers["X-Forwarded-For"]) ? "空值" : HttpContext.Current.Request.Headers["X-Forwarded-For"])
                        + ";UserHostAddress:" + (string.IsNullOrEmpty(HttpContext.Current.Request.UserHostAddress) ? "空值" : HttpContext.Current.Request.UserHostAddress)
                        , 2, "", "", "55688", true);
                    PayDB.InsertDownOrderTransferLog("非法IP，公司代码：" + body.CompanyCode, 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "非法IP，公司代码：" + body.CompanyCode;
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                    return response;
                }
            }
            #endregion

            #region 簽名檢查


            var sign = GatewayCommon.GetGPayWithdrawSign(body.OrderID, body.OrderAmount, body.OrderDate, body.CurrencyType, body.CompanyCode, CompanyModel.CompanyKey);

            if (sign != body.Sign.ToUpper())
            {
                PayDB.InsertDownOrderTransferLog("签名错误", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "签名错误";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                return response;
            }

            #endregion

            //查看過去是否已經有建單紀錄
            DT = PayDB.GetWithdrawalByWithdrawID(CompanyModel.CompanyID, body.OrderID);

            if (DT != null && DT.Rows.Count > 0)
            {

                #region 檢查之前存在的單是否為"新建"單
                PayDB.InsertDownOrderTransferLog("商户单号已经存在", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户单号已经存在";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                return response;

                #endregion

            }
            else
            {

                #region 營運商相關檢查

                #region 公司狀態檢查
                if (!(CompanyModel.CompanyState == 0))
                {
                    PayDB.InsertDownOrderTransferLog("商户停用中", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "商户停用中";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }
                #endregion

                #region 公司幣別檢查
                DT = RedisCache.CompanyPoint.GetCompanyPointByID(CompanyModel.CompanyID);
                if (!(DT != null && DT.Select("CurrencyType='" + body.CurrencyType + "'").Length > 0))
                {
                    PayDB.InsertDownOrderTransferLog("尚无申请的币别", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "尚无申请的币别";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }
                #endregion

                #region 公司限額檢查
                if (CompanyModel.WithdrawType == 1)
                {//自动代付
                    DT = RedisCache.CompanyWithdrawLimit.GetCompanyAPIWithdrawLimit(CompanyModel.CompanyID, body.CurrencyType);

                    if (!(DT != null && DT.Rows.Count > 0))
                    {
                        PayDB.InsertDownOrderTransferLog("尚未设定自动代付限额", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "尚未开启代付功能";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    CompanyWithdrawLimitModel = GatewayCommon.ToList<GatewayCommon.WithdrawLimit>(DT).FirstOrDefault();
                }
                else
                {//后台审核
                    DT = RedisCache.CompanyWithdrawLimit.GetCompanyBackendtWithdrawLimit(CompanyModel.CompanyID, body.CurrencyType);

                    if (!(DT != null && DT.Rows.Count > 0))
                    {
                        PayDB.InsertDownOrderTransferLog("尚未设定后台审核代付限额", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "尚未开启代付功能";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    LstCompanyWithdrawLimitModel = GatewayCommon.ToList<GatewayCommon.WithdrawLimit>(DT).ToList();
                    decimal tmpMaxLimit = LstCompanyWithdrawLimitModel.First().MaxLimit;
                    decimal tmpMinLimit = LstCompanyWithdrawLimitModel.First().MinLimit;
                    decimal tmpCharge = LstCompanyWithdrawLimitModel.First().Charge;
                    for (int i = 0; i < LstCompanyWithdrawLimitModel.Count; i++)
                    {
                        if (LstCompanyWithdrawLimitModel[i].MaxLimit > tmpMaxLimit)
                        {
                            tmpMaxLimit = LstCompanyWithdrawLimitModel[i].MaxLimit;
                        }

                        if (LstCompanyWithdrawLimitModel[i].MinLimit < tmpMinLimit)
                        {
                            tmpMinLimit = LstCompanyWithdrawLimitModel[i].MinLimit;
                        }


                        if (LstCompanyWithdrawLimitModel[i].Charge > tmpCharge)
                        {
                            tmpCharge = LstCompanyWithdrawLimitModel[i].Charge;
                        }
                    }

                    CompanyWithdrawLimitModel = new GatewayCommon.WithdrawLimit()
                    {
                        MaxLimit = tmpMaxLimit,
                        MinLimit = tmpMinLimit,
                        Charge = tmpCharge
                    };
                    #endregion
                }

                //營運商渠道停用檢查


                #region 公司單筆上下限制檢查

                if (body.OrderAmount > CompanyWithdrawLimitModel.MaxLimit)
                {
                    PayDB.InsertDownOrderTransferLog("单笔上限超过额度", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "单笔上限超过额度";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }

                if (body.OrderAmount < CompanyWithdrawLimitModel.MinLimit)
                {
                    PayDB.InsertDownOrderTransferLog("单笔下限超过额度", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "单笔下限超过额度";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }

                #endregion

                #region 公司可用餘額檢查
                //尚未有紀錄 => 尚無用量

                DT = PayDB.GetCanUseCompanyPoint(CompanyModel.CompanyID, body.CurrencyType);

                if (!(DT != null && DT.Rows.Count > 0))
                {
                    PayDB.InsertDownOrderTransferLog("商户可用余额不足", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "商户可用余额不足";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }

                CompanyPointModel = GatewayCommon.ToList<GatewayCommon.CompanyPoint>(DT).FirstOrDefault();

                //檢查公司可用餘額，專用渠道才有用，正常公司下發值必定大於供應商
                if ((body.OrderAmount + CompanyWithdrawLimitModel.Charge) > CompanyPointModel.CanUsePoint - CompanyPointModel.FrozenPoint)
                {
                    PayDB.InsertDownOrderTransferLog("商户可用余额不足", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "商户可用余额不足";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }

                #endregion

                #region 公司支付通道餘額檢查
                //尚未有紀錄 => 尚無用量

                if (CompanyModel.WithdrawType == 1)
                {
                    DT = PayDB.GetCompanyServicePoint(CompanyModel.CompanyID, body.CurrencyType, CompanyModel.AutoWithdrawalServiceType);


                    if (!(DT != null && DT.Rows.Count > 0))
                    {
                        PayDB.InsertDownOrderTransferLog("商户支付通道余额不足", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "商户支付通道余额不足";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    CompanyServicePointModel = GatewayCommon.ToList<GatewayCommon.CompanyServicePoint>(DT).FirstOrDefault();

                    //檢查公司可用餘額，專用渠道才有用，正常公司下發值必定大於供應商
                    if ((body.OrderAmount + CompanyWithdrawLimitModel.Charge) > CompanyServicePointModel.CanUsePoint - CompanyServicePointModel.FrozenPoint)
                    {
                        PayDB.InsertDownOrderTransferLog("商户支付通道余额不足", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "商户支付通道余额不足";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }
                }


                #endregion

                #endregion

                //当为自动代付时检查供应商相关资料
                if (CompanyModel.WithdrawType == 1)
                {
                    #region 供應商相關檢查

                    #region 設定的供應商

                    DT = RedisCache.GPayWithdrawRelation.GetGPayWithdrawRelation(CompanyModel.CompanyID, body.CurrencyType);


                    if (!(DT != null && DT.Rows.Count > 0))
                    {
                        PayDB.InsertDownOrderTransferLog("渠道设定错误", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "渠道设定错误";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    GPayWithdrawRelationModels = GatewayCommon.ToList<GatewayCommon.GPayWithdrawRelation>(DT);

                    #endregion

                    #region 供應商Limit檢查
                    bool checkProviderServiceExist = false;
                    GPayWithdrawRelationModel = GPayWithdrawRelationModels.First();

                    //DT = PayDB.GetProviderServiceByProviderServiceType(model.ProviderCode, body.ServiceType, body.CurrencyType, false);
                    DT = RedisCache.ProviderWithdrawLimit.GetProviderAPIWithdrawLimit(GPayWithdrawRelationModel.ProviderCode, body.CurrencyType);
                    if (DT != null && DT.Rows.Count > 0)
                    {
                        withdrawLimitModel = GatewayCommon.ToList<GatewayCommon.WithdrawLimit>(DT).FirstOrDefault();
                    }
                    else
                    {
                        withdrawLimitModel = null;
                    }

                    DT = RedisCache.ProviderCode.GetProviderCode(GPayWithdrawRelationModel.ProviderCode);

                    if (DT != null && DT.Rows.Count > 0)
                    {
                        provider = GatewayCommon.ToList<GatewayCommon.Provider>(DT).FirstOrDefault();
                    }
                    else
                    {
                        provider = null;
                    }



                    if (withdrawLimitModel != null && provider != null)
                    {
                        if (provider.ProviderState == 0)
                        {
                            checkProviderServiceExist = true;
                        }

                    }

                    if (!checkProviderServiceExist)
                    {
                        PayDB.InsertDownOrderTransferLog("渠道设定错误", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "渠道设定错误";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }
                    #endregion

                    //考慮到未來使用Redis之可能，不在SQL中加入OrderAmount相關的條件

                    #region 檢查供應商的是否启用
                    if (provider.ProviderState == 1)
                    {
                        PayDB.InsertDownOrderTransferLog("渠道未启用", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "渠道未启用";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    #endregion

                    #region 檢查供應商的是否開啟代付功能
                    if (!(((GatewayCommon.ProviderAPIType)provider.ProviderAPIType & GatewayCommon.ProviderAPIType.Withdraw) == GatewayCommon.ProviderAPIType.Withdraw))
                    {
                        PayDB.InsertDownOrderTransferLog("渠道代付功能未开启", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "渠道代付功能未开启";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    #endregion

                    #region 檢查供應商的單筆上下限制

                    if (body.OrderAmount > withdrawLimitModel.MaxLimit || body.OrderAmount < withdrawLimitModel.MinLimit)
                    {
                        PayDB.InsertDownOrderTransferLog("渠道单笔上下限制错误", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "渠道单笔上下限制错误";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    #endregion



                    #endregion
                }
                #region 呼叫商户API 确认此笔单为商户提交(必中使用)
                if (CompanyModel.CheckCompanyWithdrawType == 0)
                {
                    GatewayCommon.APIResult CheckCompanyWithdrawResult = GatewayCommon.CheckCompanyWithdrawCallBack(body.CompanyCode, CompanyModel.CheckCompanyWithdrawUrl, body.OrderID);
                    //回传为 null 代表不使用此功能

                    if (CheckCompanyWithdrawResult.Status != GatewayCommon.ResultStatus.OK)
                    {

                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = CheckCompanyWithdrawResult.Message;
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }
                }
                else
                {
                    if (CompanyModel.CheckCompanyWithdrawUrl != "")
                    {
                        GatewayCommon.APIResult CheckCompanyWithdrawResult = GatewayCommon.CheckCompanyWithdrawCallBack(body.CompanyCode, CompanyModel.CheckCompanyWithdrawUrl, body.OrderID);

                        if (CheckCompanyWithdrawResult.Status == GatewayCommon.ResultStatus.OK)
                        {
                            PayDB.InsertDownOrderTransferLog("测试确认订单完成", 2, "", body.OrderID, body.CompanyCode, true);
                        }
                    }

                }


                #endregion

                #region 建立交易單
                int WithdrawID;


                if (CompanyModel.WithdrawType == 1)//自动代付
                {
                    #region 确认是否为专属供应商
                    DT = PayDB.GetProxyProviderResult(provider.ProviderCode);
                    if (DT != null && DT.Rows.Count > 0)
                    {
                        ProxyProviderModel = GatewayCommon.ToList<GatewayCommon.ProxyProvider>(DT).FirstOrDefault();
                    }
                    #endregion

                    if (ProxyProviderModel != null && body.OrderAmount > ProxyProviderModel.MaxWithdrawalAmount)
                    {
                        WithdrawModel = new GatewayCommon.Withdrawal()
                        {
                            WithdrawType = 0, //0=人工/1=API代付
                            forCompanyID = CompanyModel.CompanyID,
                            ProviderCode = "",
                            CurrencyType = body.CurrencyType,
                            Amount = body.OrderAmount,
                            CollectCharge = CompanyWithdrawLimitModel.Charge,
                            CostCharge = 0,
                            Status = 0,
                            BankCard = body.BankCard.Trim(),
                            BankCardName = body.BankCardName.Trim(),
                            BankName = body.BankName.Trim(),
                            BankBranchName = body.BankBranchName,
                            OwnProvince = body.OwnProvince,
                            OwnCity = body.OwnCity,
                            DownStatus = 1,
                            DownUrl = body.ReturnUrl,
                            DownOrderID = body.OrderID,
                            DownOrderDate = body.OrderDate,
                            DownClientIP = body.ClientIP,
                            ServiceType = CompanyModel.AutoWithdrawalServiceType,
                            State= string.IsNullOrEmpty(body.State)?"": body.State,
                            FloatType = 1 //0=後台申請提現單=>後台審核/1=API申請代付=>後台審核/2=API申請代付=>不經後台審核
                        };
                    }
                    else
                    {
                        //產生交易單model
                        WithdrawModel = new GatewayCommon.Withdrawal()
                        {
                            WithdrawType = 1, //0=人工/1=API代付
                            forCompanyID = CompanyModel.CompanyID,
                            ProviderCode = provider.ProviderCode,
                            CurrencyType = body.CurrencyType,
                            Amount = body.OrderAmount,
                            CollectCharge = CompanyWithdrawLimitModel.Charge,
                            CostCharge = withdrawLimitModel.Charge,
                            Status = 1,
                            BankCard = body.BankCard.Trim(),
                            BankCardName = body.BankCardName.Trim(),
                            BankName = body.BankName.Trim(),
                            BankBranchName = body.BankBranchName,
                            OwnProvince = body.OwnProvince,
                            OwnCity = body.OwnCity,
                            DownStatus = 1,
                            DownUrl = body.ReturnUrl,
                            DownOrderID = body.OrderID,
                            DownOrderDate = body.OrderDate,
                            DownClientIP = body.ClientIP,
                            ServiceType = CompanyModel.AutoWithdrawalServiceType,
                            State = string.IsNullOrEmpty(body.State) ? "" : body.State,
                            FloatType = 2 //0=後台申請提現單=>後台審核/1=API申請代付=>後台審核/2=API申請代付=>不經後台審核
                        };
                    }

                }
                else
                {//后台审核
                 //產生交易單model
                    WithdrawModel = new GatewayCommon.Withdrawal()
                    {
                        WithdrawType = 0, //0=人工/1=API代付
                        forCompanyID = CompanyModel.CompanyID,
                        ProviderCode = "",
                        CurrencyType = body.CurrencyType,
                        Amount = body.OrderAmount,
                        CollectCharge = CompanyWithdrawLimitModel.Charge,
                        CostCharge = 0,
                        Status = 0,
                        BankCard = body.BankCard.Trim(),
                        BankCardName = body.BankCardName.Trim(),
                        BankName = body.BankName.Trim(),
                        BankBranchName = body.BankBranchName,
                        OwnProvince = body.OwnProvince,
                        OwnCity = body.OwnCity,
                        DownStatus = 1,
                        DownUrl = body.ReturnUrl,
                        DownOrderID = body.OrderID,
                        DownOrderDate = body.OrderDate,
                        DownClientIP = body.ClientIP,
                        ServiceType = "",
                        State = string.IsNullOrEmpty(body.State) ? "" : body.State,
                        FloatType = 1 //0=後台申請提現單=>後台審核/1=API申請代付=>後台審核/2=API申請代付=>不經後台審核
                    };
                }

                //建單
                WithdrawID = PayDB.InsertWithdrawalByDownData(WithdrawModel);

                if (WithdrawID < 0)
                {
                    PayDB.InsertDownOrderTransferLog("建立订单失败,Return:" + WithdrawID, 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    switch (WithdrawID)
                    {
                        case -3:
                            withdrawResult.Message = "建立订单失败:重复的订单编号";
                            break;
                        case -4:
                            withdrawResult.Message = "建立订单失败:余额不足";
                            break;
                        default:
                            withdrawResult.Message = "建立订单失败:系统错误";
                            break;
                    }
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }
                ExceptionWithdrawID = WithdrawID;
                WithdrawModel.WithdrawID = WithdrawID;
                #endregion
            }

            #region 新建單 => 尚未提交
            if (CompanyModel.CompanyType != 4)
            {
                WithdrawSerial = "OP" + System.DateTime.Now.ToString("yyyyMMddHHmm") + (new string('0', 10 - WithdrawModel.WithdrawID.ToString().Length) + WithdrawModel.WithdrawID.ToString());
            }
            else
            {
                WithdrawSerial = WithdrawModel.DownOrderID;
            }

            WithdrawSerial = "OP" + System.DateTime.Now.ToString("yyyyMMddHHmm") + (new string('0', 10 - WithdrawModel.WithdrawID.ToString().Length) + WithdrawModel.WithdrawID.ToString());
            if (PayDB.UpdateWithdrawSerialByUpData(1, string.Empty, string.Empty, 0, WithdrawSerial, WithdrawModel.WithdrawID) == 0)
            {
                PayDB.InsertDownOrderTransferLog("修改订单状态失败", 2, WithdrawSerial, body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "修改订单状态失败";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                return response;
            }

            //自动代付
            if (CompanyModel.WithdrawType == 1)
            {   //专属供应商特殊处理
                if (provider.CollectType == 1)
                {
                    if (ProxyProviderModel != null && body.OrderAmount <= ProxyProviderModel.MaxWithdrawalAmount)
                    {
                        DT = PayDB.GetProxyProviderOrderByOrderSerial(WithdrawSerial);

                        if (!(DT != null && DT.Rows.Count > 0))
                        {
                            if (CompanyModel.ProviderGroups != "0")
                            {
                                GroupID = GatewayCommon.SelectProxyProviderGroupByCompanySelected(provider.ProviderCode, body.OrderAmount, CompanyModel.ProviderGroups);
                            }
                            else
                            {
                                GroupID = GatewayCommon.SelectProxyProviderGroup(provider.ProviderCode, body.OrderAmount);
                            }
                            if (PayDB.InsertProxyProviderOrder(WithdrawSerial, GroupID) == 0)
                            {
                                PayDB.InsertDownOrderTransferLog("修改订单状态失败", 2, WithdrawSerial, body.OrderID, body.CompanyCode, true);
                                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                                withdrawResult.Message = "修改订单状态失败";
                                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                                return response;
                            }
                        }
                    }

                }
            }

            WithdrawModel.UpStatus = 0;
            WithdrawModel.WithdrawSerial = WithdrawSerial;
            #endregion

            withdrawResult.WithdrawSerial = WithdrawModel.WithdrawSerial;
            withdrawResult.OrderAmount = WithdrawModel.Amount;
            if (CompanyModel.WithdrawType == 0)
            {
                PayDB.InsertDownOrderTransferLog("申请成功,后台审核", 2, WithdrawSerial, body.OrderID, body.CompanyCode, false);
                withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                withdrawResult.Message = "申请成功,审核中";

            }
            else
            {
                if (ProxyProviderModel != null)
                {
                    if (WithdrawModel.Amount <= ProxyProviderModel.MaxWithdrawalAmount)
                    {
                        PayDB.InsertDownOrderTransferLog("申请成功,后台审核(专属供应商)", 2, WithdrawSerial, body.OrderID, body.CompanyCode, false);
                        withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                        PayDB.UpdateWithdrawStatusAndFloatTypeForProxyProvider(1, WithdrawModel.WithdrawID);
                        withdrawResult.Message = "申请成功,审核中";
                    }
                    else
                    {
                        PayDB.InsertDownOrderTransferLog("申请成功,后台审核", 2, WithdrawSerial, body.OrderID, body.CompanyCode, false);
                        withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                        withdrawResult.Message = "申请成功,审核中";
                    }
                }
                else
                {
                    withdrawReturn = GatewayCommon.SendWithdraw(WithdrawModel);
                    if (withdrawReturn != null)
                    {
                        PayDB.InsertDownOrderTransferLog("(人工审核确认)API代付结果:" + JsonConvert.SerializeObject(withdrawReturn), 2, "", body.OrderID, body.CompanyCode, false);
                        //SendStatus; 0=申請失敗/1=申請成功/2=交易已完成
                        if (withdrawReturn.SendStatus == 1)
                        {   //修改状态为上游审核中
                            PayDB.UpdateWithdrawUpStatus(1, WithdrawModel.WithdrawSerial);
                            withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                            withdrawResult.Message = "申请成功,审核中";
                        }
                        //else if (withdrawReturn.SendStatus == 2)
                        //{
                        //    PayDB.UpdateWithdrawUpStatus(1, WithdrawModel.WithdrawSerial);
                        //    withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                        //    withdrawResult.Message = "申请成功,审核中";
                        //    //System.Threading.Tasks.Task.Run(() =>
                        //    //{
                        //    //    GatewayCommon.WithdrawResultStatus returnStatus;
                        //    //    WithdrawModel = PayDB.GetWithdrawalByWithdrawID(WithdrawModel.WithdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                        //    //    //2代表已成功且扣除額度,避免重複上分
                        //    //    if (WithdrawModel.UpStatus != 2)
                        //    //    {
                        //    //        //不修改Withdraw之狀態，預存中調整
                        //    //        PayDB.UpdateWithdrawSerialByUpData(2, Newtonsoft.Json.JsonConvert.SerializeObject(withdrawReturn.ReturnResult), "", withdrawReturn.DidAmount, WithdrawModel.WithdrawSerial);
                        //    //        var intReviewWithdrawal = PayDB.ReviewWithdrawal(WithdrawModel.WithdrawSerial);
                        //    //        switch (intReviewWithdrawal)
                        //    //        {
                        //    //            case 0:
                        //    //                PayDB.InsertPaymentTransferLog("订单完成", 4, WithdrawModel.WithdrawSerial, WithdrawModel.ProviderCode);
                        //    //                //PayDB.AdjustProviderPoint(withdrawSerial, withdrawalModel.ProviderCode, "CNY", withdrawReturn.DidAmount);
                        //    //                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;

                        //    //                break;
                        //    //            default:
                        //    //                //調整訂單為系統失敗單
                        //    //                PayDB.UpdateWithdrawStatus(14, WithdrawModel.WithdrawSerial);
                        //    //                PayDB.InsertPaymentTransferLog("问题单:" + intReviewWithdrawal, 4, WithdrawModel.WithdrawSerial, WithdrawModel.ProviderCode);
                        //    //                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;

                        //    //                break;
                        //    //        }
                        //    //    }
                        //    //    else
                        //    //    {
                        //    //        PayDB.InsertPaymentTransferLog("订单完成", 4, WithdrawModel.WithdrawSerial, WithdrawModel.ProviderCode);
                        //    //        returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        //    //    }

                        //    //    WithdrawModel = PayDB.GetWithdrawalByWithdrawID(WithdrawModel.WithdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                        //    //    //取得傳送資料
                        //    //    gpayReturn.SetByWithdraw(WithdrawModel, returnStatus);
                        //    //    //發送API 回傳商戶

                        //    //    if (CompanyModel.IsProxyCallBack == 0)
                        //    //    {
                        //    //        //發送一次回調 補單用
                        //    //        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, WithdrawModel.ProviderCode))
                        //    //        {
                        //    //            //修改下游狀態
                        //    //            PayDB.UpdateWithdrawSerialByStatus(2, WithdrawModel.WithdrawID);
                        //    //        }
                        //    //    }
                        //    //    else
                        //    //    {
                        //    //        //發送一次回調 補單用
                        //    //        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, WithdrawModel.ProviderCode))
                        //    //        {
                        //    //            //修改下游狀態
                        //    //            PayDB.UpdateWithdrawSerialByStatus(2, WithdrawModel.WithdrawID);
                        //    //        }
                        //    //    }
                        //    //});

                        //}
                        else
                        {
                            PayDB.UpdateWithdrawStatusAndFloatType(0, WithdrawModel.WithdrawSerial);
                            withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                            withdrawResult.Message = "申请成功,审核中";
                        }
                    }
                    else
                    {
                        PayDB.InsertDownOrderTransferLog("上游回传空值", 2, "", body.OrderID, body.CompanyCode, true);

                        PayDB.UpdateWithdrawStatusAndFloatType(0, WithdrawModel.WithdrawSerial);
                        withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                        withdrawResult.Message = "申请成功,审核中";
                    }
                }
            }
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
            return response;
        }
        catch (Exception ex)
        {

            var WithdrawDT = PayDB.GetWithdrawalByWithdrawID(ExceptionWithdrawID);
            if (WithdrawDT != null && WithdrawDT.Rows.Count > 0)
            {

                var WithdrawModel = WithdrawDT.ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                #region 檢查之前存在的單是否為"新建"單
                PayDB.UpdateWithdrawStatus(3, WithdrawModel.WithdrawID);
                PayDB.InsertDownOrderTransferLog("Exception 错误,修改为失败单,单号:" + WithdrawModel.WithdrawID, 2, "", body.OrderID, body.CompanyCode, true);
                #endregion

            }
            int errorLine = new System.Diagnostics.StackTrace(ex, true).GetFrame(0).GetFileLineNumber();
            PayDB.InsertDownOrderTransferLog("行号:" + errorLine.ToString() + "," + ex.Message, 2, "", "", "", true);
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK); ;
            GatewayCommon.ReturnByRequireWithdraw withdrawResult = new GatewayCommon.ReturnByRequireWithdraw() { Status = GatewayCommon.ResultStatus.ERR };
            withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
            withdrawResult.Message = ex.Message;
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
            return response;
            throw;
        }

    }

    [HttpPost]
    [ActionName("RequireWithdraw2")]
    public HttpResponseMessage RequireWithdraw2([FromBody] FromBodyWithdrawRequire frombody)
    {
        //发生 Exception时 将订单改为失败单
        int ExceptionWithdrawID = -1;
        FromBodyRequireWithdraw body = new FromBodyRequireWithdraw();
        
        if (frombody != null)
        {
            body.CompanyCode = frombody.ManageCode;
            body.CurrencyType = frombody.Currency;
            body.OrderAmount = frombody.OrderAmount;
            body.BankCard = frombody.BankCard;
            body.BankCardName = frombody.BankCardName;
            body.BankName = frombody.BankName;
            body.BankBranchName = frombody.BankComponentName;
            body.OwnProvince = frombody.OwnProvince;
            body.OwnCity = frombody.OwnCity;
            body.OrderID = frombody.OrderID;
            body.OrderDate = frombody.OrderDate;
            body.ReturnUrl = frombody.RevolveUrl;
            body.ClientIP = frombody.ClientIP;
            body.Sign = frombody.Sign;
            body.State = frombody.State;

        }
     
        try
        {
            #region 回傳相關
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            GatewayCommon.ReturnByRequireWithdraw withdrawResult = new GatewayCommon.ReturnByRequireWithdraw() { Status = GatewayCommon.ResultStatus.ERR };

            //response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            #endregion

            string redirectURL = string.Empty;
            System.Data.DataTable DT;
            DateTime SummaryDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

            IList<GatewayCommon.GPayWithdrawRelation> GPayWithdrawRelationModels;
            GatewayCommon.GPayWithdrawRelation GPayWithdrawRelationModel;
            //公司相關變數
            GatewayCommon.Company CompanyModel;
            GatewayCommon.ProxyProvider ProxyProviderModel = null;
            GatewayCommon.CompanyPoint CompanyPointModel;
            GatewayCommon.CompanyServicePoint CompanyServicePointModel;
            GatewayCommon.WithdrawLimit CompanyWithdrawLimitModel;
            List<GatewayCommon.WithdrawLimit> LstCompanyWithdrawLimitModel;
            GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
            //交易單相關
            GatewayCommon.Withdrawal WithdrawModel;
            string WithdrawSerial;
            int GroupID = 0;
            GatewayCommon.ReturnWithdrawByProvider withdrawReturn;

            List<Tuple<GatewayCommon.WithdrawLimit, GatewayCommon.GPayRelation, GatewayCommon.Provider>> GPaySelectModels = new List<Tuple<GatewayCommon.WithdrawLimit, GatewayCommon.GPayRelation, GatewayCommon.Provider>>();
            //供应商相关
            GatewayCommon.WithdrawLimit withdrawLimitModel = null;
            GatewayCommon.Provider provider = null;
            IEnumerable<string> token;
            if (Request.Headers.TryGetValues("token", out token))
            {
                if (token.First() != Pay.Token)
                {
                    PayDB.InsertDownOrderTransferLog("通訊代碼有誤", 2, "", "", "", true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "通訊代碼有誤";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                    return response;
                }
            }
            else {
                PayDB.InsertDownOrderTransferLog("通訊代碼有誤", 2, "", "", "", true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "通訊代碼有誤";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            //API送出相關
            if (string.IsNullOrEmpty(body.CompanyCode))
            {
                PayDB.InsertDownOrderTransferLog("商户代码不得为空", 2, "", "", "", true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户代码不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.OrderID))
            {
                PayDB.InsertDownOrderTransferLog("商户订单号不得为空", 2, "", "", body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户订单号不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            #region 渠道总开关检查
            DT = RedisCache.WebSetting.GetWebSetting("WithdrawOption");
            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("未开启代付功能", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "未开启代付功能";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (DT.Rows[0]["SettingValue"].ToString() == "1")
            {
                PayDB.InsertDownOrderTransferLog("代付功能关闭中", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "代付功能关闭中";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion

            PayDB.InsertDownOrderTransferLog("代付申请:" + JsonConvert.SerializeObject(body) + ",IP:" + CodingControl.GetUserIP(), 2, "", body.OrderID, body.CompanyCode, false);

            if (string.IsNullOrEmpty(body.ClientIP))
            {
                PayDB.InsertDownOrderTransferLog("提单会员IP不得为空", 2, "", "", body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "提单会员IP不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            #region 传入参数检查

            if (string.IsNullOrEmpty(body.BankCard))
            {
                PayDB.InsertDownOrderTransferLog("银行卡号不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "银行卡号不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.BankName))
            {
                PayDB.InsertDownOrderTransferLog("银行名称不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "银行名称不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.BankCardName))
            {
                PayDB.InsertDownOrderTransferLog("开户名不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "开户名不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.BankBranchName))
            {
                PayDB.InsertDownOrderTransferLog("支行名称不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "支行名称不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.OwnProvince))
            {
                PayDB.InsertDownOrderTransferLog("省份不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "省份不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.OwnCity))
            {
                PayDB.InsertDownOrderTransferLog("城市名称不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "城市名称不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion

            //if (PayDB.CheckWitdrawal(body.BankCard.Trim(), body.BankCardName.Trim(), body.BankName.Trim()) > 0)
            //{
            //    PayDB.InsertDownOrderTransferLog("5分钟内只能提交一张相同银行卡资讯订单", 2, "", body.OrderID, body.CompanyCode, true);
            //    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
            //    withdrawResult.Message = "5分钟内只能提交一张相同银行卡资讯订单";
            //    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
            //    PayDB.InsertRiskControlWithdrawal(body.CompanyCode, body.BankCard, body.BankCardName, body.BankName);
            //    return response;
            //}

            //#region 黑名單
            //if (PayDB.GetBlackListCountResult(CodingControl.GetUserIP(), body.BankCard, body.BankCardName, "Withdraw") > 0)
            //{
            //    PayDB.InsertDownOrderTransferLog("错误，黑名单成员。卡号：" + body.BankCard + ",开户名：" + body.BankCardName, 2, "", body.OrderID, body.CompanyCode, true);
            //    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
            //    withdrawResult.Message = "错误，黑名单成员。卡号：" + body.BankCard + ",开户名：" + body.BankCardName;
            //    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

            //    return response;
            //}
            //#endregion


            #region 公司檢查
            //DT = PayDB.GetCompanyByCode(body.CompanyCode, true);
            DT = RedisCache.Company.GetCompanyByCode(body.CompanyCode);
            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("商户代码有误", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户代码有误";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion

            CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(DT).FirstOrDefault();

            #region API提单权限检查
            if (!((CompanyModel.WithdrawAPIType & 2) == 2))
            {
                PayDB.InsertDownOrderTransferLog("没有API代付权限", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "没有API代付权限";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion

            #region 白名單
            if (!Pay.IsTestSite)
            {
                var InIP = HttpContext.Current.Request.Headers["X-Forwarded-For"];

                if (string.IsNullOrEmpty(InIP))
                {
                    InIP = "127.0.0.1";
                }

                if (PayDB.GetWithdrawalIP(InIP, body.CompanyCode) <= 0)
                {
             
                    PayDB.InsertDownOrderTransferLog("该IP未在白名单内。 IP：" + InIP + ",公司代码：" + body.CompanyCode, 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "该IP未在白名单内。 IP：" + InIP;
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                    return response;
                }
            }
            #endregion

            #region 簽名檢查
            var sign = GatewayCommon.GetGPayWithdrawSign(body.OrderID, body.OrderAmount, body.OrderDate, body.CurrencyType, body.CompanyCode, CompanyModel.CompanyKey);

            if (sign != body.Sign.ToUpper())
            {
                PayDB.InsertDownOrderTransferLog("签名错误", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "签名错误";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                return response;
            }

            #endregion

            //查看過去是否已經有建單紀錄
            DT = PayDB.GetWithdrawalByWithdrawID(CompanyModel.CompanyID, body.OrderID);

            if (DT != null && DT.Rows.Count > 0)
            {

                #region 檢查之前存在的單是否為"新建"單
                PayDB.InsertDownOrderTransferLog("商户单号已经存在", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户单号已经存在";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                return response;

                #endregion

            }
            else
            {

                #region 營運商相關檢查

                #region 公司狀態檢查
                if (!(CompanyModel.CompanyState == 0))
                {
                    PayDB.InsertDownOrderTransferLog("商户停用中", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "商户停用中";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }
                #endregion

                #region 公司幣別檢查
                DT = RedisCache.CompanyPoint.GetCompanyPointByID(CompanyModel.CompanyID);
                if (!(DT != null && DT.Select("CurrencyType='" + body.CurrencyType + "'").Length > 0))
                {
                    PayDB.InsertDownOrderTransferLog("尚无申请的币别", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "尚无申请的币别";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }
                #endregion

                #region 公司限額檢查
                if (CompanyModel.CompanyType != 4)
                {
                    if (CompanyModel.WithdrawType == 1)
                    {//自动代付
                        DT = RedisCache.CompanyWithdrawLimit.GetCompanyAPIWithdrawLimit(CompanyModel.CompanyID, body.CurrencyType);

                        if (!(DT != null && DT.Rows.Count > 0))
                        {
                            PayDB.InsertDownOrderTransferLog("尚未设定自动代付限额", 2, "", body.OrderID, body.CompanyCode, true);
                            withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                            withdrawResult.Message = "尚未开启代付功能";
                            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                            return response;
                        }

                        CompanyWithdrawLimitModel = GatewayCommon.ToList<GatewayCommon.WithdrawLimit>(DT).FirstOrDefault();
                    }
                    else
                    {//后台审核
                        DT = RedisCache.CompanyWithdrawLimit.GetCompanyBackendtWithdrawLimit(CompanyModel.CompanyID, body.CurrencyType);

                        if (!(DT != null && DT.Rows.Count > 0))
                        {
                            PayDB.InsertDownOrderTransferLog("尚未设定后台审核代付限额", 2, "", body.OrderID, body.CompanyCode, true);
                            withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                            withdrawResult.Message = "尚未开启代付功能";
                            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                            return response;
                        }

                        LstCompanyWithdrawLimitModel = GatewayCommon.ToList<GatewayCommon.WithdrawLimit>(DT).ToList();
                        decimal tmpMaxLimit = LstCompanyWithdrawLimitModel.First().MaxLimit;
                        decimal tmpMinLimit = LstCompanyWithdrawLimitModel.First().MinLimit;
                        decimal tmpCharge = LstCompanyWithdrawLimitModel.First().Charge;
                        for (int i = 0; i < LstCompanyWithdrawLimitModel.Count; i++)
                        {
                            if (LstCompanyWithdrawLimitModel[i].MaxLimit > tmpMaxLimit)
                            {
                                tmpMaxLimit = LstCompanyWithdrawLimitModel[i].MaxLimit;
                            }

                            if (LstCompanyWithdrawLimitModel[i].MinLimit < tmpMinLimit)
                            {
                                tmpMinLimit = LstCompanyWithdrawLimitModel[i].MinLimit;
                            }


                            if (LstCompanyWithdrawLimitModel[i].Charge > tmpCharge)
                            {
                                tmpCharge = LstCompanyWithdrawLimitModel[i].Charge;
                            }
                        }

                        CompanyWithdrawLimitModel = new GatewayCommon.WithdrawLimit()
                        {
                            MaxLimit = tmpMaxLimit,
                            MinLimit = tmpMinLimit,
                            Charge = tmpCharge
                        };
                        #endregion
                    }

                    #region 公司單筆上下限制檢查

                    if (body.OrderAmount > CompanyWithdrawLimitModel.MaxLimit)
                    {
                        PayDB.InsertDownOrderTransferLog("单笔上限超过额度", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "单笔上限超过额度";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    if (body.OrderAmount < CompanyWithdrawLimitModel.MinLimit)
                    {
                        PayDB.InsertDownOrderTransferLog("单笔下限超过额度", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "单笔下限超过额度";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    #endregion
                }
                else {
                    CompanyWithdrawLimitModel = new GatewayCommon.WithdrawLimit()
                    {
                        MaxLimit = 0,
                        MinLimit = 0,
                        Charge = 0
                    };
                }

                //營運商渠道停用檢查

                #region 公司可用餘額檢查
                //尚未有紀錄 => 尚無用量

                //DT = PayDB.GetCanUseCompanyPoint(CompanyModel.CompanyID, body.CurrencyType);

                //if (!(DT != null && DT.Rows.Count > 0))
                //{
                //    PayDB.InsertDownOrderTransferLog("商户可用余额不足", 2, "", body.OrderID, body.CompanyCode, true);
                //    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                //    withdrawResult.Message = "商户可用余额不足";
                //    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                //    return response;
                //}

                //CompanyPointModel = GatewayCommon.ToList<GatewayCommon.CompanyPoint>(DT).FirstOrDefault();

                ////檢查公司可用餘額，專用渠道才有用，正常公司下發值必定大於供應商
                //if ((body.OrderAmount + CompanyWithdrawLimitModel.Charge) > CompanyPointModel.CanUsePoint - CompanyPointModel.FrozenPoint)
                //{
                //    PayDB.InsertDownOrderTransferLog("商户可用余额不足", 2, "", body.OrderID, body.CompanyCode, true);
                //    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                //    withdrawResult.Message = "商户可用余额不足";
                //    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                //    return response;
                //}

                #endregion

                #region 公司支付通道餘額檢查
                //尚未有紀錄 => 尚無用量

                //if (CompanyModel.WithdrawType == 1)
                //{
                //    DT = PayDB.GetCompanyServicePoint(CompanyModel.CompanyID, body.CurrencyType, CompanyModel.AutoWithdrawalServiceType);


                //    if (!(DT != null && DT.Rows.Count > 0))
                //    {
                //        PayDB.InsertDownOrderTransferLog("商户支付通道余额不足", 2, "", body.OrderID, body.CompanyCode, true);
                //        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                //        withdrawResult.Message = "商户支付通道余额不足";
                //        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                //        return response;
                //    }

                //    CompanyServicePointModel = GatewayCommon.ToList<GatewayCommon.CompanyServicePoint>(DT).FirstOrDefault();

                //    //檢查公司可用餘額，專用渠道才有用，正常公司下發值必定大於供應商
                //    if ((body.OrderAmount + CompanyWithdrawLimitModel.Charge) > CompanyServicePointModel.CanUsePoint - CompanyServicePointModel.FrozenPoint)
                //    {
                //        PayDB.InsertDownOrderTransferLog("商户支付通道余额不足", 2, "", body.OrderID, body.CompanyCode, true);
                //        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                //        withdrawResult.Message = "商户支付通道余额不足";
                //        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                //        return response;
                //    }
                //}


                #endregion

                #endregion

                //当为自动代付时检查供应商相关资料
                if (CompanyModel.WithdrawType == 1)
                {
                    #region 供應商相關檢查

                    #region 設定的供應商

                    DT = RedisCache.GPayWithdrawRelation.GetGPayWithdrawRelation(CompanyModel.CompanyID, body.CurrencyType);


                    if (!(DT != null && DT.Rows.Count > 0))
                    {
                        PayDB.InsertDownOrderTransferLog("渠道设定错误", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "渠道设定错误";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    GPayWithdrawRelationModels = GatewayCommon.ToList<GatewayCommon.GPayWithdrawRelation>(DT);

                    #endregion

                    #region 供應商Limit檢查
                    bool checkProviderServiceExist = false;
                    GPayWithdrawRelationModel = GPayWithdrawRelationModels.First();

                    //DT = PayDB.GetProviderServiceByProviderServiceType(model.ProviderCode, body.ServiceType, body.CurrencyType, false);
                    DT = RedisCache.ProviderWithdrawLimit.GetProviderAPIWithdrawLimit(GPayWithdrawRelationModel.ProviderCode, body.CurrencyType);
                    if (DT != null && DT.Rows.Count > 0)
                    {
                        withdrawLimitModel = GatewayCommon.ToList<GatewayCommon.WithdrawLimit>(DT).FirstOrDefault();
                    }
                    else
                    {
                        withdrawLimitModel = null;
                    }

                    DT = RedisCache.ProviderCode.GetProviderCode(GPayWithdrawRelationModel.ProviderCode);

                    if (DT != null && DT.Rows.Count > 0)
                    {
                        provider = GatewayCommon.ToList<GatewayCommon.Provider>(DT).FirstOrDefault();
                    }
                    else
                    {
                        provider = null;
                    }



                    if (withdrawLimitModel != null && provider != null)
                    {
                        if (provider.ProviderState == 0)
                        {
                            checkProviderServiceExist = true;
                        }

                    }

                    if (!checkProviderServiceExist)
                    {
                        PayDB.InsertDownOrderTransferLog("渠道设定错误", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "渠道设定错误";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }
                    #endregion

                    //考慮到未來使用Redis之可能，不在SQL中加入OrderAmount相關的條件

                    #region 檢查供應商的是否启用
                    if (provider.ProviderState == 1)
                    {
                        PayDB.InsertDownOrderTransferLog("渠道未启用", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "渠道未启用";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    #endregion

                    #region 檢查供應商的是否開啟代付功能
                    if (!(((GatewayCommon.ProviderAPIType)provider.ProviderAPIType & GatewayCommon.ProviderAPIType.Withdraw) == GatewayCommon.ProviderAPIType.Withdraw))
                    {
                        PayDB.InsertDownOrderTransferLog("渠道代付功能未开启", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "渠道代付功能未开启";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    #endregion

                    #region 檢查供應商的單筆上下限制

                    if (body.OrderAmount > withdrawLimitModel.MaxLimit || body.OrderAmount < withdrawLimitModel.MinLimit)
                    {
                        PayDB.InsertDownOrderTransferLog("渠道单笔上下限制错误", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "渠道单笔上下限制错误";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    #endregion

                    #endregion
                }
                #region 呼叫商户API 确认此笔单为商户提交(必中使用)
                //if (CompanyModel.CheckCompanyWithdrawType == 0)
                //{
                //    GatewayCommon.APIResult CheckCompanyWithdrawResult = GatewayCommon.CheckCompanyWithdrawCallBack(body.CompanyCode, CompanyModel.CheckCompanyWithdrawUrl, body.OrderID);
                //    //回传为 null 代表不使用此功能

                //    if (CheckCompanyWithdrawResult.Status != GatewayCommon.ResultStatus.OK)
                //    {

                //        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                //        withdrawResult.Message = CheckCompanyWithdrawResult.Message;
                //        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                //        return response;
                //    }
                //}
                //else
                //{
                //    if (CompanyModel.CheckCompanyWithdrawUrl != "")
                //    {
                //        GatewayCommon.APIResult CheckCompanyWithdrawResult = GatewayCommon.CheckCompanyWithdrawCallBack(body.CompanyCode, CompanyModel.CheckCompanyWithdrawUrl, body.OrderID);

                //        if (CheckCompanyWithdrawResult.Status == GatewayCommon.ResultStatus.OK)
                //        {
                //            PayDB.InsertDownOrderTransferLog("测试确认订单完成", 2, "", body.OrderID, body.CompanyCode, true);
                //        }
                //    }

                //}

                #endregion

                #region 建立交易單
                int WithdrawID;


                if (CompanyModel.WithdrawType == 1)//自动代付
                {
                    #region 确认是否为专属供应商
                    DT = PayDB.GetProxyProviderResult(provider.ProviderCode);
                    if (DT != null && DT.Rows.Count > 0)
                    {
                        ProxyProviderModel = GatewayCommon.ToList<GatewayCommon.ProxyProvider>(DT).FirstOrDefault();
                    }
                    #endregion

                    if (ProxyProviderModel != null && body.OrderAmount > ProxyProviderModel.MaxWithdrawalAmount)
                    {
                        WithdrawModel = new GatewayCommon.Withdrawal()
                        {
                            WithdrawType = 0, //0=人工/1=API代付
                            forCompanyID = CompanyModel.CompanyID,
                            ProviderCode = "",
                            CurrencyType = body.CurrencyType,
                            Amount = body.OrderAmount,
                            CollectCharge = CompanyWithdrawLimitModel.Charge,
                            CostCharge = 0,
                            Status = 0,
                            BankCard = body.BankCard.Trim(),
                            BankCardName = body.BankCardName.Trim(),
                            BankName = body.BankName.Trim(),
                            BankBranchName = body.BankBranchName,
                            OwnProvince = body.OwnProvince,
                            OwnCity = body.OwnCity,
                            DownStatus = 1,
                            DownUrl = body.ReturnUrl,
                            DownOrderID = body.OrderID,
                            DownOrderDate = body.OrderDate,
                            DownClientIP = body.ClientIP,
                            ServiceType = CompanyModel.AutoWithdrawalServiceType,
                            State = string.IsNullOrEmpty(body.State) ? "" : body.State,
                            FloatType = 1 //0=後台申請提現單=>後台審核/1=API申請代付=>後台審核/2=API申請代付=>不經後台審核
                        };
                    }
                    else
                    {
                        //產生交易單model
                        WithdrawModel = new GatewayCommon.Withdrawal()
                        {
                            WithdrawType = 1, //0=人工/1=API代付
                            forCompanyID = CompanyModel.CompanyID,
                            ProviderCode = provider.ProviderCode,
                            CurrencyType = body.CurrencyType,
                            Amount = body.OrderAmount,
                            CollectCharge = CompanyWithdrawLimitModel.Charge,
                            CostCharge = withdrawLimitModel.Charge,
                            Status = 1,
                            BankCard = body.BankCard.Trim(),
                            BankCardName = body.BankCardName.Trim(),
                            BankName = body.BankName.Trim(),
                            BankBranchName = body.BankBranchName,
                            OwnProvince = body.OwnProvince,
                            OwnCity = body.OwnCity,
                            DownStatus = 1,
                            DownUrl = body.ReturnUrl,
                            DownOrderID = body.OrderID,
                            DownOrderDate = body.OrderDate,
                            DownClientIP = body.ClientIP,
                            ServiceType = CompanyModel.AutoWithdrawalServiceType,
                            State = string.IsNullOrEmpty(body.State) ? "" : body.State,
                            FloatType = 2 //0=後台申請提現單=>後台審核/1=API申請代付=>後台審核/2=API申請代付=>不經後台審核
                        };
                    }

                }
                else
                {//后台审核
                 //產生交易單model
                    WithdrawModel = new GatewayCommon.Withdrawal()
                    {
                        WithdrawType = 0, //0=人工/1=API代付
                        forCompanyID = CompanyModel.CompanyID,
                        ProviderCode = "",
                        CurrencyType = body.CurrencyType,
                        Amount = body.OrderAmount,
                        CollectCharge = CompanyWithdrawLimitModel.Charge,
                        CostCharge = 0,
                        Status = 0,
                        BankCard = body.BankCard.Trim(),
                        BankCardName = body.BankCardName.Trim(),
                        BankName = body.BankName.Trim(),
                        BankBranchName = body.BankBranchName,
                        OwnProvince = body.OwnProvince,
                        OwnCity = body.OwnCity,
                        DownStatus = 1,
                        DownUrl = body.ReturnUrl,
                        DownOrderID = body.OrderID,
                        DownOrderDate = body.OrderDate,
                        DownClientIP = body.ClientIP,
                        ServiceType = "",
                        State = string.IsNullOrEmpty(body.State) ? "" : body.State,
                        FloatType = 1 //0=後台申請提現單=>後台審核/1=API申請代付=>後台審核/2=API申請代付=>不經後台審核
                    };
                }

                //建單
                WithdrawID = PayDB.InsertWithdrawalByDownData(WithdrawModel);

                if (WithdrawID < 0)
                {
                    PayDB.InsertDownOrderTransferLog("建立订单失败,Return:" + WithdrawID, 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    switch (WithdrawID)
                    {
                        case -3:
                            withdrawResult.Message = "建立订单失败:重复的订单编号";
                            break;
                        case -4:
                            withdrawResult.Message = "建立订单失败:余额不足";
                            break;
                        default:
                            withdrawResult.Message = "建立订单失败:系统错误";
                            break;
                    }
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }
                ExceptionWithdrawID = WithdrawID;
                WithdrawModel.WithdrawID = WithdrawID;
                #endregion
            }

            #region 新建單 => 尚未提交
            if (CompanyModel.CompanyType != 4)
            {
                WithdrawSerial = "OP" + System.DateTime.Now.ToString("yyyyMMddHHmm") + (new string('0', 10 - WithdrawModel.WithdrawID.ToString().Length) + WithdrawModel.WithdrawID.ToString());
            }
            else
            {
                WithdrawSerial = WithdrawModel.DownOrderID;
            }
            if (PayDB.UpdateWithdrawSerialByUpData(1, string.Empty, string.Empty, 0, WithdrawSerial, WithdrawModel.WithdrawID) == 0)
            {
                PayDB.InsertDownOrderTransferLog("修改订单状态失败", 2, WithdrawSerial, body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "修改订单状态失败";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                return response;
            }

            //自动代付
            if (CompanyModel.WithdrawType == 1)
            {   //专属供应商特殊处理
                if (provider.CollectType == 1)
                {
                    if (ProxyProviderModel != null && body.OrderAmount <= ProxyProviderModel.MaxWithdrawalAmount)
                    {
                        DT = PayDB.GetProxyProviderOrderByOrderSerial(WithdrawSerial);

                        if (!(DT != null && DT.Rows.Count > 0))
                        {
                            if (CompanyModel.ProviderGroups != "0")
                            {
                                GroupID = GatewayCommon.SelectProxyProviderGroupByCompanySelected(provider.ProviderCode, body.OrderAmount, CompanyModel.ProviderGroups);
                            }
                            else
                            {
                                GroupID = GatewayCommon.SelectProxyProviderGroup(provider.ProviderCode, body.OrderAmount);
                            }
                            if (PayDB.InsertProxyProviderOrder(WithdrawSerial, GroupID) == 0)
                            {
                                PayDB.InsertDownOrderTransferLog("修改订单状态失败", 2, WithdrawSerial, body.OrderID, body.CompanyCode, true);
                                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                                withdrawResult.Message = "修改订单状态失败";
                                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                                return response;
                            }
                        }
                    }

                }
            }

            WithdrawModel.UpStatus = 0;
            WithdrawModel.WithdrawSerial = WithdrawSerial;
            #endregion

            withdrawResult.WithdrawSerial = WithdrawModel.WithdrawSerial;
            withdrawResult.OrderAmount = WithdrawModel.Amount;
            if (CompanyModel.WithdrawType == 0)
            {
                PayDB.InsertDownOrderTransferLog("申请成功,后台审核", 2, WithdrawSerial, body.OrderID, body.CompanyCode, false);
                withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                withdrawResult.Message = "申请成功,审核中";

            }
            else
            {
                if (ProxyProviderModel != null)
                {
                    if (WithdrawModel.Amount <= ProxyProviderModel.MaxWithdrawalAmount)
                    {
                        PayDB.InsertDownOrderTransferLog("申请成功,后台审核(专属供应商)", 2, WithdrawSerial, body.OrderID, body.CompanyCode, false);
                        withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                        PayDB.UpdateWithdrawStatusAndFloatTypeForProxyProvider(1, WithdrawModel.WithdrawID);
                        withdrawResult.Message = "申请成功,审核中";
                    }
                    else
                    {
                        PayDB.InsertDownOrderTransferLog("申请成功,后台审核", 2, WithdrawSerial, body.OrderID, body.CompanyCode, false);
                        withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                        withdrawResult.Message = "申请成功,审核中";
                    }
                }
                else
                {
                    withdrawReturn = GatewayCommon.SendWithdraw(WithdrawModel);
                    if (withdrawReturn != null)
                    {
                        PayDB.InsertDownOrderTransferLog("(人工审核确认)API代付结果:" + JsonConvert.SerializeObject(withdrawReturn), 2, "", body.OrderID, body.CompanyCode, false);
                        //SendStatus; 0=申請失敗/1=申請成功/2=交易已完成
                        if (withdrawReturn.SendStatus == 1)
                        {   //修改状态为上游审核中
                            PayDB.UpdateWithdrawUpStatus(1, WithdrawModel.WithdrawSerial);
                            withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                            withdrawResult.Message = "申请成功,审核中";
                        }
                        //else if (withdrawReturn.SendStatus == 2)
                        //{
                        //    PayDB.UpdateWithdrawUpStatus(1, WithdrawModel.WithdrawSerial);
                        //    withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                        //    withdrawResult.Message = "申请成功,审核中";
                        //    //System.Threading.Tasks.Task.Run(() =>
                        //    //{
                        //    //    GatewayCommon.WithdrawResultStatus returnStatus;
                        //    //    WithdrawModel = PayDB.GetWithdrawalByWithdrawID(WithdrawModel.WithdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                        //    //    //2代表已成功且扣除額度,避免重複上分
                        //    //    if (WithdrawModel.UpStatus != 2)
                        //    //    {
                        //    //        //不修改Withdraw之狀態，預存中調整
                        //    //        PayDB.UpdateWithdrawSerialByUpData(2, Newtonsoft.Json.JsonConvert.SerializeObject(withdrawReturn.ReturnResult), "", withdrawReturn.DidAmount, WithdrawModel.WithdrawSerial);
                        //    //        var intReviewWithdrawal = PayDB.ReviewWithdrawal(WithdrawModel.WithdrawSerial);
                        //    //        switch (intReviewWithdrawal)
                        //    //        {
                        //    //            case 0:
                        //    //                PayDB.InsertPaymentTransferLog("订单完成", 4, WithdrawModel.WithdrawSerial, WithdrawModel.ProviderCode);
                        //    //                //PayDB.AdjustProviderPoint(withdrawSerial, withdrawalModel.ProviderCode, "CNY", withdrawReturn.DidAmount);
                        //    //                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;

                        //    //                break;
                        //    //            default:
                        //    //                //調整訂單為系統失敗單
                        //    //                PayDB.UpdateWithdrawStatus(14, WithdrawModel.WithdrawSerial);
                        //    //                PayDB.InsertPaymentTransferLog("问题单:" + intReviewWithdrawal, 4, WithdrawModel.WithdrawSerial, WithdrawModel.ProviderCode);
                        //    //                returnStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;

                        //    //                break;
                        //    //        }
                        //    //    }
                        //    //    else
                        //    //    {
                        //    //        PayDB.InsertPaymentTransferLog("订单完成", 4, WithdrawModel.WithdrawSerial, WithdrawModel.ProviderCode);
                        //    //        returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                        //    //    }

                        //    //    WithdrawModel = PayDB.GetWithdrawalByWithdrawID(WithdrawModel.WithdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                        //    //    //取得傳送資料
                        //    //    gpayReturn.SetByWithdraw(WithdrawModel, returnStatus);
                        //    //    //發送API 回傳商戶

                        //    //    if (CompanyModel.IsProxyCallBack == 0)
                        //    //    {
                        //    //        //發送一次回調 補單用
                        //    //        if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, WithdrawModel.ProviderCode))
                        //    //        {
                        //    //            //修改下游狀態
                        //    //            PayDB.UpdateWithdrawSerialByStatus(2, WithdrawModel.WithdrawID);
                        //    //        }
                        //    //    }
                        //    //    else
                        //    //    {
                        //    //        //發送一次回調 補單用
                        //    //        if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, WithdrawModel.ProviderCode))
                        //    //        {
                        //    //            //修改下游狀態
                        //    //            PayDB.UpdateWithdrawSerialByStatus(2, WithdrawModel.WithdrawID);
                        //    //        }
                        //    //    }
                        //    //});

                        //}
                        else
                        {
                            PayDB.UpdateWithdrawStatusAndFloatType(0, WithdrawModel.WithdrawSerial);
                            withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                            withdrawResult.Message = "申请成功,审核中";
                        }
                    }
                    else
                    {
                        PayDB.InsertDownOrderTransferLog("上游回传空值", 2, "", body.OrderID, body.CompanyCode, true);

                        PayDB.UpdateWithdrawStatusAndFloatType(0, WithdrawModel.WithdrawSerial);
                        withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                        withdrawResult.Message = "申请成功,审核中";
                    }
                }
            }
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
            return response;
        }
        catch (Exception ex)
        {

            var WithdrawDT = PayDB.GetWithdrawalByWithdrawID(ExceptionWithdrawID);
            if (WithdrawDT != null && WithdrawDT.Rows.Count > 0)
            {

                var WithdrawModel = WithdrawDT.ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                #region 檢查之前存在的單是否為"新建"單
                PayDB.UpdateWithdrawStatus(3, WithdrawModel.WithdrawID);
                PayDB.InsertDownOrderTransferLog("Exception 错误,修改为失败单,单号:" + WithdrawModel.WithdrawID, 2, "", body.OrderID, body.CompanyCode, true);
                #endregion

            }
            int errorLine = new System.Diagnostics.StackTrace(ex, true).GetFrame(0).GetFileLineNumber();
            PayDB.InsertDownOrderTransferLog("行号:" + errorLine.ToString() + "," + ex.Message, 2, "", "", "", true);
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK); ;
            GatewayCommon.ReturnByRequireWithdraw withdrawResult = new GatewayCommon.ReturnByRequireWithdraw() { Status = GatewayCommon.ResultStatus.ERR };
            withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
            withdrawResult.Message = ex.Message;
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
            return response;
            throw;
        }

    }

    [HttpPost]
    [ActionName("RequireWithdraw3")]
    public HttpResponseMessage RequireWithdraw3([FromBody] FromBodyWithdrawRequire frombody)
    {
        //发生 Exception时 将订单改为失败单
        int ExceptionWithdrawID = -1;
        FromBodyRequireWithdraw body = new FromBodyRequireWithdraw();

        if (frombody != null)
        {
            body.CompanyCode = frombody.ManageCode;
            body.CurrencyType = frombody.Currency;
            body.OrderAmount = frombody.OrderAmount;
            body.BankCard = frombody.BankCard;
            body.BankCardName = frombody.BankCardName;
            body.BankName = frombody.BankName;
            body.BankBranchName = frombody.BankComponentName;
            body.OwnProvince = frombody.OwnProvince;
            body.OwnCity = frombody.OwnCity;
            body.OrderID = frombody.OrderID;
            body.OrderDate = frombody.OrderDate;
            body.ReturnUrl = frombody.RevolveUrl;
            body.ClientIP = frombody.ClientIP;
            body.Sign = frombody.Sign;
            body.State = frombody.State;
            body.ProviderCode = frombody.ProviderCode;
            body.ServiceType = frombody.ServiceType;
        }

        try
        {
            #region 回傳相關
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            GatewayCommon.ReturnByRequireWithdraw withdrawResult = new GatewayCommon.ReturnByRequireWithdraw() { Status = GatewayCommon.ResultStatus.ERR };

            //response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            #endregion

            string redirectURL = string.Empty;
            System.Data.DataTable DT;
            DateTime SummaryDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

            IList<GatewayCommon.GPayWithdrawRelation> GPayWithdrawRelationModels;
     
            //公司相關變數
            GatewayCommon.Company CompanyModel;
            GatewayCommon.ProxyProvider ProxyProviderModel = null;
            GatewayCommon.CompanyPoint CompanyPointModel;
            GatewayCommon.CompanyServicePoint CompanyServicePointModel;
            GatewayCommon.WithdrawLimit CompanyWithdrawLimitModel;
            List<GatewayCommon.WithdrawLimit> LstCompanyWithdrawLimitModel;
            GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
            //交易單相關
            GatewayCommon.Withdrawal WithdrawModel;
            string WithdrawSerial;
            int GroupID = 0;
            GatewayCommon.ReturnWithdrawByProvider withdrawReturn;

            List<Tuple<GatewayCommon.WithdrawLimit, GatewayCommon.GPayRelation, GatewayCommon.Provider>> GPaySelectModels = new List<Tuple<GatewayCommon.WithdrawLimit, GatewayCommon.GPayRelation, GatewayCommon.Provider>>();
            //供应商相关
            GatewayCommon.WithdrawLimit withdrawLimitModel = null;
            GatewayCommon.Provider provider = null;
            IEnumerable<string> token;
            if (Request.Headers.TryGetValues("token", out token))
            {
                if (token.First() != Pay.Token)
                {
                    PayDB.InsertDownOrderTransferLog("通訊代碼有誤", 2, "", "", "", true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "通訊代碼有誤";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                    return response;
                }
            }
            else
            {
                PayDB.InsertDownOrderTransferLog("通訊代碼有誤", 2, "", "", "", true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "通訊代碼有誤";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            //API送出相關
            if (string.IsNullOrEmpty(body.CompanyCode))
            {
                PayDB.InsertDownOrderTransferLog("商户代码不得为空", 2, "", "", "", true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户代码不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            //API送出相關
            if (string.IsNullOrEmpty(frombody.ProviderCode))
            {
                PayDB.InsertDownOrderTransferLog("供應商代码不得为空", 2, "", "", "", true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "供應商代码不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            //API送出相關
            if (string.IsNullOrEmpty(frombody.ServiceType))
            {
                PayDB.InsertDownOrderTransferLog("服務類型不得为空", 2, "", "", "", true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "服務類型不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.OrderID))
            {
                PayDB.InsertDownOrderTransferLog("商户订单号不得为空", 2, "", "", body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户订单号不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            #region 渠道总开关检查
            DT = RedisCache.WebSetting.GetWebSetting("WithdrawOption");
            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("未开启代付功能", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "未开启代付功能";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (DT.Rows[0]["SettingValue"].ToString() == "1")
            {
                PayDB.InsertDownOrderTransferLog("代付功能关闭中", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "代付功能关闭中";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion

            PayDB.InsertDownOrderTransferLog("代付申请:" + JsonConvert.SerializeObject(body) + ",IP:" + CodingControl.GetUserIP(), 2, "", body.OrderID, body.CompanyCode, false);
  
            if (string.IsNullOrEmpty(body.ClientIP))
            {
                PayDB.InsertDownOrderTransferLog("提单会员IP不得为空", 2, "", "", body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "提单会员IP不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            #region 传入参数检查

            if (string.IsNullOrEmpty(body.BankCard))
            {
                PayDB.InsertDownOrderTransferLog("银行卡号不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "银行卡号不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.BankName))
            {
                PayDB.InsertDownOrderTransferLog("银行名称不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "银行名称不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.BankCardName))
            {
                PayDB.InsertDownOrderTransferLog("开户名不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "开户名不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.BankBranchName))
            {
                PayDB.InsertDownOrderTransferLog("支行名称不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "支行名称不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.OwnProvince))
            {
                PayDB.InsertDownOrderTransferLog("省份不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "省份不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }

            if (string.IsNullOrEmpty(body.OwnCity))
            {
                PayDB.InsertDownOrderTransferLog("城市名称不得为空", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "城市名称不得为空";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion

            #region 公司檢查
            //DT = PayDB.GetCompanyByCode(body.CompanyCode, true);
            DT = RedisCache.Company.GetCompanyByCode(body.CompanyCode);
            if (!(DT != null && DT.Rows.Count > 0))
            {
                PayDB.InsertDownOrderTransferLog("商户代码有误", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户代码有误";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion

            CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(DT).FirstOrDefault();

            #region API提单权限检查
            if (!((CompanyModel.WithdrawAPIType & 2) == 2))
            {
                PayDB.InsertDownOrderTransferLog("没有API代付权限", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "没有API代付权限";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                return response;
            }
            #endregion

            #region 白名單
            if (!Pay.IsTestSite)
            {
                var InIP = HttpContext.Current.Request.Headers["X-Forwarded-For"];


                if (PayDB.GetWithdrawalIP(InIP, body.CompanyCode) <= 0)
                {

                    PayDB.InsertDownOrderTransferLog("该IP未在白名单内。 IP：" + InIP + ",公司代码：" + body.CompanyCode, 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "该IP未在白名单内。 IP：" + InIP;
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));

                    return response;
                }
            }
            #endregion

            #region 簽名檢查
            var sign = GatewayCommon.GetGPayWithdrawSign(body.OrderID, body.OrderAmount, body.OrderDate, body.CurrencyType, body.CompanyCode, CompanyModel.CompanyKey);

            if (sign != body.Sign.ToUpper())
            {
                PayDB.InsertDownOrderTransferLog("签名错误", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "签名错误";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                return response;
            }

            #endregion

            //查看過去是否已經有建單紀錄
            //DT = PayDB.GetWithdrawalByWithdrawID(CompanyModel.CompanyID, body.OrderID);

            if (false)
            {

                #region 檢查之前存在的單是否為"新建"單
                PayDB.InsertDownOrderTransferLog("商户单号已经存在", 2, "", body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "商户单号已经存在";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                return response;

                #endregion

            }
            else
            {

                #region 營運商相關檢查

                #region 公司狀態檢查
                if (!(CompanyModel.CompanyState == 0))
                {
                    PayDB.InsertDownOrderTransferLog("商户停用中", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "商户停用中";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }
                #endregion

                #region 公司幣別檢查
                DT = RedisCache.CompanyPoint.GetCompanyPointByID(CompanyModel.CompanyID);
                if (!(DT != null && DT.Select("CurrencyType='" + body.CurrencyType + "'").Length > 0))
                {
                    PayDB.InsertDownOrderTransferLog("尚无申请的币别", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "尚无申请的币别";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }
                #endregion

                #region 公司限額檢查
                if (CompanyModel.CompanyType != 4)
                {
                    if (CompanyModel.WithdrawType == 1)
                    {//自动代付
                        DT = RedisCache.CompanyWithdrawLimit.GetCompanyAPIWithdrawLimit(CompanyModel.CompanyID, body.CurrencyType);

                        if (!(DT != null && DT.Rows.Count > 0))
                        {
                            PayDB.InsertDownOrderTransferLog("尚未设定自动代付限额", 2, "", body.OrderID, body.CompanyCode, true);
                            withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                            withdrawResult.Message = "尚未开启代付功能";
                            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                            return response;
                        }

                        CompanyWithdrawLimitModel = GatewayCommon.ToList<GatewayCommon.WithdrawLimit>(DT).FirstOrDefault();
                    }
                    else
                    {//后台审核
                        DT = RedisCache.CompanyWithdrawLimit.GetCompanyBackendtWithdrawLimit(CompanyModel.CompanyID, body.CurrencyType);

                        if (!(DT != null && DT.Rows.Count > 0))
                        {
                            PayDB.InsertDownOrderTransferLog("尚未设定后台审核代付限额", 2, "", body.OrderID, body.CompanyCode, true);
                            withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                            withdrawResult.Message = "尚未开启代付功能";
                            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                            return response;
                        }

                        LstCompanyWithdrawLimitModel = GatewayCommon.ToList<GatewayCommon.WithdrawLimit>(DT).ToList();
                        decimal tmpMaxLimit = LstCompanyWithdrawLimitModel.First().MaxLimit;
                        decimal tmpMinLimit = LstCompanyWithdrawLimitModel.First().MinLimit;
                        decimal tmpCharge = LstCompanyWithdrawLimitModel.First().Charge;
                        for (int i = 0; i < LstCompanyWithdrawLimitModel.Count; i++)
                        {
                            if (LstCompanyWithdrawLimitModel[i].MaxLimit > tmpMaxLimit)
                            {
                                tmpMaxLimit = LstCompanyWithdrawLimitModel[i].MaxLimit;
                            }

                            if (LstCompanyWithdrawLimitModel[i].MinLimit < tmpMinLimit)
                            {
                                tmpMinLimit = LstCompanyWithdrawLimitModel[i].MinLimit;
                            }


                            if (LstCompanyWithdrawLimitModel[i].Charge > tmpCharge)
                            {
                                tmpCharge = LstCompanyWithdrawLimitModel[i].Charge;
                            }
                        }

                        CompanyWithdrawLimitModel = new GatewayCommon.WithdrawLimit()
                        {
                            MaxLimit = tmpMaxLimit,
                            MinLimit = tmpMinLimit,
                            Charge = tmpCharge
                        };
                        #endregion
                    }

                    #region 公司單筆上下限制檢查

                    if (body.OrderAmount > CompanyWithdrawLimitModel.MaxLimit)
                    {
                        PayDB.InsertDownOrderTransferLog("单笔上限超过额度", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "单笔上限超过额度";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    if (body.OrderAmount < CompanyWithdrawLimitModel.MinLimit)
                    {
                        PayDB.InsertDownOrderTransferLog("单笔下限超过额度", 2, "", body.OrderID, body.CompanyCode, true);
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "单笔下限超过额度";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    #endregion
                }
                else
                {
                    CompanyWithdrawLimitModel = new GatewayCommon.WithdrawLimit()
                    {
                        MaxLimit = 0,
                        MinLimit = 0,
                        Charge = 0
                    };
                }
                #endregion

                #region 供應商相關檢查


                #region 供應商Limit檢查
                bool checkProviderServiceExist = false;

                //DT = PayDB.GetProviderServiceByProviderServiceType(model.ProviderCode, body.ServiceType, body.CurrencyType, false);
                DT = RedisCache.ProviderWithdrawLimit.GetProviderAPIWithdrawLimit(frombody.ProviderCode, body.CurrencyType);
                if (DT != null && DT.Rows.Count > 0)
                {
                    withdrawLimitModel = GatewayCommon.ToList<GatewayCommon.WithdrawLimit>(DT).FirstOrDefault();
                }
                else
                {
                    withdrawLimitModel = null;
                }

                DT = RedisCache.ProviderCode.GetProviderCode(frombody.ProviderCode);

                if (DT != null && DT.Rows.Count > 0)
                {
                    provider = GatewayCommon.ToList<GatewayCommon.Provider>(DT).FirstOrDefault();
                }
                else
                {
                    provider = null;
                }

                if (withdrawLimitModel != null && provider != null)
                {
                    if (provider.ProviderState == 0)
                    {
                        checkProviderServiceExist = true;
                    }

                }

                if (!checkProviderServiceExist)
                {
                    PayDB.InsertDownOrderTransferLog("渠道设定错误", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "渠道设定错误";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }
                #endregion

                //考慮到未來使用Redis之可能，不在SQL中加入OrderAmount相關的條件

                #region 檢查供應商的是否启用
                if (provider.ProviderState == 1)
                {
                    PayDB.InsertDownOrderTransferLog("渠道未启用", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "渠道未启用";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }

                #endregion

                #region 檢查供應商的是否開啟代付功能
                if (!(((GatewayCommon.ProviderAPIType)provider.ProviderAPIType & GatewayCommon.ProviderAPIType.Withdraw) == GatewayCommon.ProviderAPIType.Withdraw))
                {
                    PayDB.InsertDownOrderTransferLog("渠道代付功能未开启", 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "渠道代付功能未开启";
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }

                #endregion

                #region 檢查供應商的單筆上下限制

                //if (body.OrderAmount > withdrawLimitModel.MaxLimit || body.OrderAmount < withdrawLimitModel.MinLimit)
                //{
                //    PayDB.InsertDownOrderTransferLog("渠道单笔上下限制错误", 2, "", body.OrderID, body.CompanyCode, true);
                //    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                //    withdrawResult.Message = "渠道单笔上下限制错误";
                //    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                //    return response;
                //}

                #endregion

                #endregion

                #region 建立交易單
                int WithdrawID;

                WithdrawModel = new GatewayCommon.Withdrawal()
                {
                    WithdrawType = 1, //0=人工/1=API代付
                    forCompanyID = CompanyModel.CompanyID,
                    ProviderCode = frombody.ProviderCode,
                    CurrencyType = body.CurrencyType,
                    Amount = body.OrderAmount,
                    CollectCharge = CompanyWithdrawLimitModel.Charge,
                    CostCharge = withdrawLimitModel.Charge,
                    Status = 1,
                    BankCard = body.BankCard.Trim(),
                    BankCardName = body.BankCardName.Trim(),
                    BankName = body.BankName.Trim(),
                    BankBranchName = body.BankBranchName,
                    OwnProvince = body.OwnProvince,
                    OwnCity = body.OwnCity,
                    DownStatus = 1,
                    DownUrl = body.ReturnUrl,
                    DownOrderID = body.OrderID,
                    DownOrderDate = body.OrderDate,
                    DownClientIP = body.ClientIP,
                    ServiceType = frombody.ServiceType,
                    State = string.IsNullOrEmpty(body.State) ? "" : body.State,
                    FloatType = 2 //0=後台申請提現單=>後台審核/1=API申請代付=>後台審核/2=API申請代付=>不經後台審核
                };

                //建單
                WithdrawID = PayDB.InsertWithdrawalByDownData(WithdrawModel);
              
                if (WithdrawID==-3)
                {
                    WithdrawModel = PayDB.GetWithdrawalByDownOrderIDAndStatus0(body.OrderID, CompanyModel.CompanyID).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();

                    if (string.IsNullOrEmpty(WithdrawModel.WithdrawSerial))
                    {
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "訂單狀態有誤";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }

                    WithdrawModel.ProviderCode = body.ProviderCode;
                    WithdrawModel.ServiceType = body.ServiceType;
                    WithdrawID = WithdrawModel.WithdrawID;

                    if (PayDB.UpdateWithdrawProvider(WithdrawID, WithdrawModel.ProviderCode, WithdrawModel.ServiceType)==0)
                    {
                        withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                        withdrawResult.Message = "修改訂單供應商失敗";
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                        return response;
                    }
                }
                else if (WithdrawID < 0)
                {
                    PayDB.InsertDownOrderTransferLog("建立订单失败,Return:" + WithdrawID, 2, "", body.OrderID, body.CompanyCode, true);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    switch (WithdrawID)
                    {
                        case -3:
                            withdrawResult.Message = "建立订单失败:重复的订单编号";
                            break;
                        case -4:
                            withdrawResult.Message = "建立订单失败:余额不足";
                            break;
                        default:
                            withdrawResult.Message = "建立订单失败:系统错误";
                            break;
                    }
                    response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                    return response;
                }

                ExceptionWithdrawID = WithdrawID;
                WithdrawModel.WithdrawID = WithdrawID;
                #endregion
            }

            #region 新建單 => 尚未提交
            if (CompanyModel.CompanyType != 4)
            {
                WithdrawSerial = "OP" + System.DateTime.Now.ToString("yyyyMMddHHmm") + (new string('0', 10 - WithdrawModel.WithdrawID.ToString().Length) + WithdrawModel.WithdrawID.ToString());
            }
            else 
            {
                WithdrawSerial = WithdrawModel.DownOrderID;
            }

            if (PayDB.UpdateWithdrawSerialByUpData(1, string.Empty, string.Empty, 0, WithdrawSerial, WithdrawModel.WithdrawID) == 0)
            {
                PayDB.InsertDownOrderTransferLog("修改订单状态失败", 2, WithdrawSerial, body.OrderID, body.CompanyCode, true);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "修改订单状态失败";
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
                return response;
            }

            WithdrawModel.UpStatus = 0;
            WithdrawModel.WithdrawSerial = WithdrawSerial;
            #endregion

            withdrawResult.WithdrawSerial = WithdrawModel.WithdrawSerial;
            withdrawResult.OrderAmount = WithdrawModel.Amount;

            withdrawReturn = GatewayCommon.SendWithdraw(WithdrawModel);
            if (withdrawReturn != null)
            {
                PayDB.InsertDownOrderTransferLog("(人工审核确认)API代付结果:" + JsonConvert.SerializeObject(withdrawReturn), 2, "", body.OrderID, body.CompanyCode, false);
                //SendStatus; 0=申請失敗/1=申請成功/2=交易已完成
                if (withdrawReturn.SendStatus == 1)
                {   //修改状态为上游审核中
                    PayDB.ReviewWithdrawaltoProccessing(WithdrawModel.WithdrawID);
                    withdrawResult.Status = GatewayCommon.ResultStatus.OK;
                    withdrawResult.Message = "申请成功,审核中";
                }
                else
                {
                    PayDB.ReviewWithdrawalProccessingtoDefault(WithdrawModel.WithdrawID);
                    withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                    withdrawResult.Message = "申請失敗:"+ withdrawReturn.ReturnResult;
                }
            }
            else
            {
                PayDB.InsertDownOrderTransferLog("上游回传空值", 2, "", body.OrderID, body.CompanyCode, true);

                PayDB.UpdateWithdrawStatusAndFloatType(0, WithdrawModel.WithdrawSerial);
                withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
                withdrawResult.Message = "申请成功,审核中";
            }

            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
            return response;
        }
        catch (Exception ex)
        {

            var WithdrawDT = PayDB.GetWithdrawalByWithdrawID(ExceptionWithdrawID);
            if (WithdrawDT != null && WithdrawDT.Rows.Count > 0)
            {

                var WithdrawModel = WithdrawDT.ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                #region 檢查之前存在的單是否為"新建"單
                PayDB.UpdateWithdrawStatus(3, WithdrawModel.WithdrawID);
                PayDB.InsertDownOrderTransferLog("Exception 错误,修改为失败单,单号:" + WithdrawModel.WithdrawID, 2, "", body.OrderID, body.CompanyCode, true);
                #endregion

            }
            int errorLine = new System.Diagnostics.StackTrace(ex, true).GetFrame(0).GetFileLineNumber();
            PayDB.InsertDownOrderTransferLog("行号:" + errorLine.ToString() + "," + ex.Message, 2, "", "", "", true);
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK); ;
            GatewayCommon.ReturnByRequireWithdraw withdrawResult = new GatewayCommon.ReturnByRequireWithdraw() { Status = GatewayCommon.ResultStatus.ERR };
            withdrawResult.Status = GatewayCommon.ResultStatus.ERR;
            withdrawResult.Message = ex.Message;
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(withdrawResult));
            return response;
            throw;
        }

    }

    [HttpPost]
    [ActionName("QueryWithdraw")]
    public QueryWithdrawResult1 QueryWithdraw([FromBody] FromBodyQueryWithdraw1 frombody)
    {
        QueryWithdrawResult1 Ret = null;
        string SS;
        System.Data.SqlClient.SqlCommand DBCmd = null;
        System.Data.DataTable CompanyDT;
        System.Data.DataTable DT;
        string signStr;
        string sign;
        string companyKey;
        GatewayCommon.Withdrawal withdraw;
        FromBodyQueryWithdraw body = new FromBodyQueryWithdraw();
        body.CompanyCode = frombody.ManageCode;
        body.WithdrawSerial = frombody.WithdrawSerial;
        body.Sign = frombody.Sign;

        CompanyDT = RedisCache.Company.GetCompanyByCode(body.CompanyCode);
        if (CompanyDT.Rows.Count > 0)
        {
            companyKey = CompanyDT.Rows[0]["CompanyKey"].ToString();
            #region 簽名檢查
            signStr = string.Format("ManageCode={0}&WithdrawSerial={1}&CompanyKey={2}", body.CompanyCode, body.WithdrawSerial, companyKey);
            sign = CodingControl.GetSHA256(signStr, false);
            #endregion

            if (sign.ToUpper() == body.Sign.ToUpper())
            {
                SS = "SELECT * FROM Withdrawal WITH (NOLOCK) WHERE forCompanyID=@CompanyID AND WithdrawSerial=@WithdrawSerial";
                DBCmd = new System.Data.SqlClient.SqlCommand();
                DBCmd.CommandText = SS;
                DBCmd.CommandType = System.Data.CommandType.Text;
                DBCmd.Parameters.Add("@CompanyID", System.Data.SqlDbType.Int).Value = (int)CompanyDT.Rows[0]["CompanyID"];
                DBCmd.Parameters.Add("@WithdrawSerial", System.Data.SqlDbType.VarChar).Value = body.WithdrawSerial;
                DT = DBAccess.GetDB(Pay.DBConnStr, DBCmd);
                if (DT.Rows.Count > 0)
                {
                    Ret = new QueryWithdrawResult1();

                    withdraw = DT.ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                    GatewayCommon.WithdrawResultStatus withdrawResultStatus;

                    if (withdraw.Status == 2)
                    {
                        withdrawResultStatus = GatewayCommon.WithdrawResultStatus.Successs;
                    }
                    else if (withdraw.Status == 3)
                    {
                        withdrawResultStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else if (withdraw.Status == 14)
                    {
                        withdrawResultStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                    }
                    else
                    {
                        withdrawResultStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                    }

                    Ret.SetByWithdraw(withdraw, withdrawResultStatus, body.CompanyCode, companyKey);

                }
                else
                {
                    Ret = new QueryWithdrawResult1() { Status = ResultStatus.ERR, Message = "InvalidWithdrawSerial" };
                    Ret.WithdrawStatus = 1;
                }
            }
            else
            {
                Ret = new QueryWithdrawResult1() { Status = ResultStatus.ERR, Message = "SignError" };
                Ret.WithdrawStatus = 1;
            }
        }
        else
        {
            Ret = new QueryWithdrawResult1() { Status = ResultStatus.ERR, Message = "InvalidCompany" };
            Ret.WithdrawStatus = 1;
        }


        return Ret;
    }

    [HttpPost]
    [ActionName("QueryWithdrawByOrderID")]
    public QueryWithdrawResult1 QueryWithdrawByOrderID([FromBody] FromBodyQueryWithdraw1ByDownOrderID1 frombody)
    {
        QueryWithdrawResult1 Ret = null;
        System.Data.DataTable CompanyDT;
        System.Data.DataTable DT;
        string signStr;
        string sign;
        string companyKey;
        GatewayCommon.Withdrawal withdraw;
        FromBodyQueryWithdraw1ByDownOrderID body = new FromBodyQueryWithdraw1ByDownOrderID();
        body.CompanyCode = frombody.ManageCode;
        body.DownOrderID = frombody.OrderID;
        body.Sign = frombody.Sign;

        CompanyDT = RedisCache.Company.GetCompanyByCode(body.CompanyCode);
        if (CompanyDT.Rows.Count > 0)
        {
            companyKey = CompanyDT.Rows[0]["CompanyKey"].ToString();
            #region 簽名檢查
            signStr = string.Format("ManageCode={0}&OrderID={1}&CompanyKey={2}", body.CompanyCode, body.DownOrderID, companyKey);
            sign = CodingControl.GetSHA256(signStr, false);
            #endregion

            if (sign.ToUpper() == body.Sign.ToUpper())
            {

                DT = PayDB.GetWithdrawalByDownOrderID(body.DownOrderID, (int)CompanyDT.Rows[0]["CompanyID"]);
                if (DT.Rows.Count > 0)
                {
                    Ret = new QueryWithdrawResult1();

                    withdraw = DT.ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                    GatewayCommon.WithdrawResultStatus withdrawResultStatus;

                    if (withdraw.Status == 2)
                    {
                        withdrawResultStatus = GatewayCommon.WithdrawResultStatus.Successs;
                    }
                    else if (withdraw.Status == 3)
                    {
                        withdrawResultStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    }
                    else if (withdraw.Status == 14)
                    {
                        withdrawResultStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                    }
                    else
                    {
                        withdrawResultStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                    }

                    Ret.SetByWithdraw(withdraw, withdrawResultStatus, body.CompanyCode, companyKey);

                }
                else
                {
                    Ret = new QueryWithdrawResult1() { Status = ResultStatus.ERR, Message = "Invalid OrderID" };
                    Ret.WithdrawStatus = 1;
                }
            }
            else
            {
                Ret = new QueryWithdrawResult1() { Status = ResultStatus.ERR, Message = "SignError" };
                Ret.WithdrawStatus = 1;
            }
        }
        else
        {
            Ret = new QueryWithdrawResult1() { Status = ResultStatus.ERR, Message = "Invalid ManageCode" };
            Ret.WithdrawStatus = 1;
        }

        return Ret;
    }

    #endregion

    #region Other

    [HttpPost]
    [ActionName("QueryBalance")]
    public CompanyBalanceResult1 QueryBalance([FromBody] FromBodyBalance1 frombody)
    {
        CompanyBalanceResult1 Ret = new CompanyBalanceResult1();
        System.Data.DataTable DT;
        string signStr;
        string sign;
        GatewayCommon.Company CompanyModel;
        GatewayCommon.CompanyPoint CP;
        FromBodyBalance body = new FromBodyBalance();
        body.CompanyCode = frombody.ManageCode;
        body.CurrencyType = frombody.Currency;
        body.Sign = frombody.Sign;

        DT = RedisCache.Company.GetCompanyByCode(body.CompanyCode);
        if (!(DT != null && DT.Rows.Count > 0))
        {
            Ret.Status = ResultStatus.ERR;
            Ret.Message = "该商户代码不存在";
        }

        CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(DT).FirstOrDefault();
        #region SignCheck
        signStr = string.Format("Currency={0}&ManageCode={1}&CompanyKey={2}"
        , body.CurrencyType
        , body.CompanyCode
        , CompanyModel.CompanyKey
        );

        sign = CodingControl.GetSHA256(signStr, false).ToUpper();

        #endregion

        if (sign.ToUpper() == body.Sign.ToUpper())
        {

            DT = PayDB.GetCanUseCompanyPoint(CompanyModel.CompanyID, body.CurrencyType);
            if (!(DT != null && DT.Rows.Count > 0))
            {
                Ret.Status = ResultStatus.ERR;
                Ret.Message = "该商户钱包不存在";
            }

            CP = GatewayCommon.ToList<GatewayCommon.CompanyPoint>(DT).FirstOrDefault();

            Ret.Status = ResultStatus.OK;
            Ret.AccountBalance = CP.PointValue;
            Ret.Currency = body.CurrencyType;
            Ret.ManageCode = body.CompanyCode;
            Ret.AvailableBalance = CP.CanUsePoint - CP.FrozenPoint;

        }
        else
        {
            Ret.Status = ResultStatus.ERR;
            Ret.Message = "簽名有誤";
        }

        return Ret;
    }


    #endregion

    // 要求用戶付款
    // 由用戶端瀏覽器執行 POST

    [HttpPost]
    [HttpGet]
    [ActionName("GetBankCode")]
    public BankCodeResult GetBankCode()
    {
        BankCodeResult Ret = new BankCodeResult() { BankCodes = new List<BankCodeData>() };
        System.Data.DataTable DT;

        DT = RedisCache.BankCode.GetBankCode();

        for (int i = 0; i < DT.Rows.Count; i++)
        {
            Ret.BankCodes.Add(new BankCodeData()
            {
                BankCode = DT.Rows[i]["BankCode"].ToString(),
                BankName = DT.Rows[i]["BankName"].ToString(),
                CurrencyType = DT.Rows[i]["CurrencyType"].ToString()
            });
        }
        return Ret;
    }

    public class APIResult
    {
        public ResultStatus Status { get; set; }
        public string Message { get; set; }
    }

    public enum ResultStatus
    {
        OK = 0,
        ERR = 1
    }

    public class PaymentResult : APIResult
    {
        public string PaymentSerial { get; set; }
        public int PaymentStatus { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal PaymentAmount { get; set; }
        public string ServiceType { get; set; }
        public string CurrencyType { get; set; }
        public string CompanyCode { get; set; }
        public string BankSequenceID { get; set; }
        public string OrderID { get; set; }
        public string OrderDate { get; set; }
        public string Sign { get; set; }

        public void SetByPayment(GatewayCommon.Payment payment, GatewayCommon.PaymentResultStatus paymentStatus, string CompanyCode, string CompanyKey)
        {

            this.PaymentSerial = payment.PaymentSerial;
            this.PaymentAmount = (payment.PartialOrderAmount == 0 ? payment.OrderAmount : payment.PartialOrderAmount);
            this.OrderAmount = payment.OrderAmount;
            this.PaymentStatus = (int)paymentStatus;
            this.CurrencyType = payment.CurrencyType;
            this.ServiceType = payment.ServiceType;
            this.CompanyCode = CompanyCode;
            this.BankSequenceID = payment.BankSequenceID;
            this.OrderID = payment.OrderID;
            this.OrderDate = payment.OrderDate.ToString("yyyy-MM-dd HH:mm:ss");
            this.Sign = GatewayCommon.GetGPaySign(OrderID, OrderAmount, payment.OrderDate, ServiceType, CurrencyType, CompanyCode, CompanyKey);

        }
    }

    public class PayingResult : APIResult
    {
        public string PayingSerial { get; set; }
        public int PayingStatus { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal PayingAmount { get; set; }
        public string Service { get; set; }
        public string Currency { get; set; }
        public string ManageCode { get; set; }
        public string BankID { get; set; }
        public string OrderID { get; set; }
        public string OrderDate { get; set; }
        public string Sign { get; set; }

        public void SetByPayment(GatewayCommon.Payment payment, GatewayCommon.PaymentResultStatus paymentStatus, string CompanyCode, string CompanyKey)
        {

            this.PayingSerial = payment.PaymentSerial;
            this.PayingAmount = (payment.PartialOrderAmount == 0 ? payment.OrderAmount : payment.PartialOrderAmount);
            this.OrderAmount = payment.OrderAmount;
            this.PayingStatus = (int)paymentStatus;
            this.Currency = payment.CurrencyType;
            this.Service = payment.ServiceType;
            this.ManageCode = CompanyCode;
            this.BankID = payment.BankSequenceID;
            this.OrderID = payment.OrderID;
            this.OrderDate = payment.OrderDate.ToString("yyyy-MM-dd HH:mm:ss");
            this.Sign = GatewayCommon.GetGPaySign(OrderID, OrderAmount, payment.OrderDate, Service, Currency, CompanyCode, CompanyKey);

        }
    }

    public class QueryWithdrawResult : APIResult
    {
        public string WithdrawSerial { get; set; }
        public int WithdrawStatus { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal WithdrawAmount { get; set; }
        public decimal WithdrawCharge { get; set; }
        public string CurrencyType { get; set; }
        public string CompanyCode { get; set; }
        public string OrderID { get; set; }
        public string OrderDate { get; set; }
        public string Sign { get; set; }
        public string ServiceType { get; set; }

        public void SetByWithdraw(GatewayCommon.Withdrawal withdrawal, GatewayCommon.WithdrawResultStatus withdrawalStatus, string CompanyCode, string CompanyKey)
        {

            this.WithdrawSerial = withdrawal.WithdrawSerial;
            this.WithdrawStatus = (int)withdrawalStatus;
            this.OrderAmount = withdrawal.Amount;
            this.WithdrawAmount = withdrawal.FinishAmount;
            this.WithdrawCharge = withdrawal.CollectCharge;
            this.CurrencyType = withdrawal.CurrencyType;
            this.CompanyCode = CompanyCode;
            this.ServiceType = withdrawal.ServiceType;
            this.OrderID = withdrawal.DownOrderID;
            this.OrderDate = withdrawal.DownOrderDate.ToString("yyyy-MM-dd HH:mm:ss");
            this.Sign = GatewayCommon.GetGPayWithdrawSign(this.OrderID, this.OrderAmount, withdrawal.DownOrderDate, this.CurrencyType, CompanyCode, CompanyKey);

        }
    }

    public class QueryWithdrawResult1 : APIResult
    {
        public string WithdrawSerial { get; set; }
        public int WithdrawStatus { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal WithdrawAmount { get; set; }
        public decimal WithdrawCharge { get; set; }
        public string Currency { get; set; }
        public string ManageCode { get; set; }
        public string OrderID { get; set; }
        public string OrderDate { get; set; }
        public string Sign { get; set; }
        public string Service { get; set; }

        public void SetByWithdraw(GatewayCommon.Withdrawal withdrawal, GatewayCommon.WithdrawResultStatus withdrawalStatus, string CompanyCode, string CompanyKey)
        {

            this.WithdrawSerial = withdrawal.WithdrawSerial;
            this.WithdrawStatus = (int)withdrawalStatus;
            this.OrderAmount = withdrawal.Amount;
            this.WithdrawAmount = withdrawal.FinishAmount;
            this.WithdrawCharge = withdrawal.CollectCharge;
            this.Currency = withdrawal.CurrencyType;
            this.ManageCode = CompanyCode;
            this.Service = withdrawal.ServiceType;
            this.OrderID = withdrawal.DownOrderID;
            this.OrderDate = withdrawal.DownOrderDate.ToString("yyyy-MM-dd HH:mm:ss");
            this.Sign = GatewayCommon.GetGPayWithdrawSign(this.OrderID, this.OrderAmount, withdrawal.DownOrderDate, this.Currency, CompanyCode, CompanyKey);

        }
    }

    public class BankCodeResult : APIResult
    {
        public List<BankCodeData> BankCodes { get; set; }
    }

    public class BankCodeData
    {
        public string BankCode { get; set; }
        public string BankName { get; set; }
        public string CurrencyType { get; set; }
    }

    public class WithdrawResult : APIResult
    {
        // 0=即時/1=非即時
        public int SendType;
        public string WithdrawSerial;
        public int WithdrawStatus;
        public decimal WithdrawAmount;
        public decimal WithdrawCharge;
    }


    public class CompanyBalanceResult : APIResult
    {
        public string CompanyCode { get; set; }
        public string CurrencyType { get; set; }
        //帳戶總餘額
        public decimal AccountBalance { get; set; }
        //可用餘額
        public decimal CashBalance { get; set; }
    }

    public class CompanyBalanceResult1 : APIResult
    {
        public string ManageCode { get; set; }
        public string Currency { get; set; }
        //帳戶總餘額
        public decimal AccountBalance { get; set; }
        //可用餘額
        public decimal AvailableBalance { get; set; }
    }

    public enum ResultCode
    {
        Success = 100,
        InvalidCompanyCode = 101,
        SignFailure = 102,
        RepeatOrderID = 103,
        InvalidCompany = 104,
        InvalidCurrencyType = 105,
        InvalidCompanyService = 106,
        OrderAmountExceededLimit = 107,
        OrderAmountExceedsLowerLimit = 108,
        ExceedDaliyAmount = 109,
        NoSettingRelation = 110,
        InvalidProviderService = 111,
        InvalidProviderServiceByCheck1 = 1120,
        InvalidProviderServiceByCheck2 = 1121,
        InvalidPaymentID = 113,
        InvalidOrderID = 114,
        InvalidBankCode = 115,
        InsertUserNameError = 116,
        GateWayUserNameError = 117,
        CallBackUrlError = 118,
        GetRedirectUrlFail = 199,
        InvalidRequire = 999
    }

    #region FrmoBody
    public class FromBodyRequirePayment
    {
        public string CompanyCode { get; set; }
        public string CurrencyType { get; set; }
        public string ServiceType { get; set; }
        public string BankCode { get; set; }
        public string ClientIP { get; set; }
        public string OrderID { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal OrderAmount { get; set; }
        public string ReturnURL { get; set; }
        public string State { get; set; }
        public string SelCurrency { get; set; }
        public string AssignAmount { get; set; }
        public string UserName { get; set; }
        public string Sign { get; set; }
        public string ProviderCode { get; set; }
    }
    public class FromBodyRequirePaying
    {
        public string ManageCode { get; set; }
        public string Currency { get; set; }
        public string Service { get; set; }
        public string BankCode { get; set; }
        public string CustomerIP { get; set; }
        public string OrderID { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal OrderAmount { get; set; }
        public string RevolveURL { get; set; }
        public string State { get; set; }
        public string SelCurrency { get; set; }
        public string AllotAmount { get; set; }
        public string UserName { get; set; }
        public string ProviderCode { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodySavePayingUserName {
        public string UserName { get; set; }
        public int PaymentID { get; set; }
    }

    public class FromBodyQueryPayment
    {

        public string CompanyCode { get; set; }
        public string PaymentSerial { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyQueryPaying
    {

        public string ManageCode { get; set; }
        public string PayingSerial { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyQueryPayingByOrderID
    {

        public string ManageCode { get; set; }
        public string OrderID { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyRequireWithdraw
    {
        public string CompanyCode { get; set; }
        public string CurrencyType { get; set; }
        public decimal OrderAmount { get; set; }
        public string BankCard { get; set; }
        public string BankCardName { get; set; }
        public string BankName { get; set; }
        public string BankBranchName { get; set; }
        public string OwnProvince { get; set; }
        public string OwnCity { get; set; }
        public string OrderID { get; set; }
        public DateTime OrderDate { get; set; }
        public string ReturnUrl { get; set; }
        public string ClientIP { get; set; }
        public string Sign { get; set; }
        public string State { get; set; }
        public string ProviderCode { get; set; }
        public string ServiceType { get; set; }
    }

    public class FromBodyWithdrawRequire
    {
        public string ManageCode { get; set; }
        public string Currency { get; set; }
        public decimal OrderAmount { get; set; }
        public string BankCard { get; set; }
        public string BankCardName { get; set; }
        public string BankName { get; set; }
        public string BankComponentName { get; set; }
        public string OwnProvince { get; set; }
        public string OwnCity { get; set; }
        public string OrderID { get; set; }
        public DateTime OrderDate { get; set; }
        public string RevolveUrl { get; set; }
        public string State { get; set; }
        public string ClientIP { get; set; }
        public string ServiceType { get; set; }
        public string ProviderCode { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyManualWithdrawalReviewConfirme
    {
        public string CompanyCode { get; set; }
        public string CurrencyType { get; set; }
        public decimal OrderAmount { get; set; }
        public string BankName { get; set; }
        public string BankCard { get; set; }
        public string BankCardName { get; set; }

        public string BankBranchName { get; set; }
        public string OrderID { get; set; }
        public string ReturnUrl { get; set; }
        public string Sign { get; set; }

        public string WithdrawSerial { get; set; }
        public int ReviewStatus { get; set; } //0=成功/1=失败
    }

    public class FromBodyBalance
    {
        public string CompanyCode { get; set; }
        public string CurrencyType { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyBalance1
    {
        public string ManageCode { get; set; }
        public string Currency { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyQueryWithdraw
    {

        public string CompanyCode { get; set; }
        public string WithdrawSerial { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyQueryWithdraw1
    {

        public string ManageCode { get; set; }
        public string WithdrawSerial { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyQueryWithdraw1ByDownOrderID
    {

        public string CompanyCode { get; set; }
        public string DownOrderID { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyQueryWithdraw1ByDownOrderID1
    {

        public string ManageCode { get; set; }
        public string OrderID { get; set; }
        public string Sign { get; set; }
    }

    #endregion
}