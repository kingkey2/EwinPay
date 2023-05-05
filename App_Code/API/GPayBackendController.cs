using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json;

public class GPayBackendController : ApiController {


    [HttpGet]
    [ActionName("GetUnSendMsgToBot")]
    public MsgList GetUnSendMsgToBot(int LastMsgID) {

        MsgList ReturnMsgList = new MsgList();
        //ReturnMsgList.List = new List<Msg>();
        ReturnMsgList.List = GatewayCommon.ToList<Msg>(PayDB.GetUnSendMsgToBot(LastMsgID)) as List<Msg>;

        return ReturnMsgList;
    }

    [HttpGet]
    [ActionName("SetMsgToBotSended")]
    public void SetMsgToBotSended(int LastMsgID) {
         
        PayDB.SetMsgToBotSended(LastMsgID);
    }

    [HttpGet]
    [ActionName("QueryProviderOrder")]
    public string QueryProviderOrder(string PaymentSerial)
    {

        var DT = PayDB.GetPaymentByPaymentID(PaymentSerial);
        GatewayCommon.Payment paymentModel;
        paymentModel = GatewayCommon.ToList<GatewayCommon.Payment>(DT).FirstOrDefault();
        GatewayCommon.PaymentByProvider providerRequestData = new GatewayCommon.PaymentByProvider();
        providerRequestData = GatewayCommon.QueryPaymentByProvider(paymentModel);
      
        return JsonConvert.SerializeObject(providerRequestData);
    }


    [HttpPost]
    [ActionName("GetProviderPointList")]
    public GetProviderPointListResult GetProviderPointList([FromBody] FromBodyQueryPointByPayment body)
    {

        #region SignCheck
        string strSign;
        string sign;
        GetProviderPointListResult Ret = new GetProviderPointListResult() { Providers = new List<FromBodyQueryProviderPoint>() };
        strSign = string.Format("GPayBackendKey={0}"
        , Pay.GPayBackendKey
        );
   
        sign = CodingControl.GetSHA256(strSign);

        if (sign.ToUpper() == body.Sign.ToUpper())
        {
            string jsonResult = "";
            var DT = PayDB.GetProviderPointList();

            if (DT != null && DT.Rows.Count > 0)
            {

                jsonResult = JsonConvert.SerializeObject(DT);
                Ret.Status = ResultStatus.OK;
                Ret.Providers = GatewayCommon.ToList<FromBodyQueryProviderPoint>(DT).ToList();

            }
            else
            {
                Ret.Status = ResultStatus.ERR;
                Ret.Message = "沒有資料";
            }
        }
        else
        {
            Ret.Status = ResultStatus.SignErr;
            Ret.Message = "簽名有誤";
        }

        #endregion

        return Ret;
    }

    [HttpPost]
    [ActionName("QueryProviderPoint")]
    public QueryProviderPointResult QueryProviderPoint([FromBody] FromBodyQueryProviderPoint body)
    {
        #region SignCheck
        string strSign;
        string sign;
        QueryProviderPointResult Ret = new QueryProviderPointResult() { BalanceData = new GatewayCommon.BalanceByProvider() };
        strSign = string.Format("CurrencyType={0}&ProviderCode={1}&GPayBackendKey={2}"
        , body.CurrencyType
        , body.ProviderCode
        , Pay.GPayBackendKey
        );

        sign = CodingControl.GetSHA256(strSign);

        if (sign.ToUpper() == body.Sign.ToUpper())
        {
            GatewayCommon.BalanceByProvider providerRequestData = new GatewayCommon.BalanceByProvider();
            providerRequestData = GatewayCommon.QueryProviderBalance(body.ProviderCode, body.CurrencyType);

            if (providerRequestData != null)
            {

                Ret.Status = ResultStatus.OK;
                Ret.BalanceData = providerRequestData;

            }
            else
            {
                Ret.Status = ResultStatus.ERR;
                Ret.Message = "沒有資料";
            }
        }
        else
        {
            Ret.Status = ResultStatus.SignErr;
            Ret.Message = "簽名有誤";
        }

        #endregion

        return Ret;
    }


    [HttpPost]
    [ActionName("QueryProviderBalance")]
    public ProviderBalanceResult QueryProviderBalance([FromBody] FromBodyBalance body) {
        ProviderBalanceResult Ret = new ProviderBalanceResult() { ArrayProviderBalance = new List<ProviderBalance>() };

        #region SignCheck
        string strSign;
        string sign;

        strSign = string.Format("CurrencyType={0}&GPayBackendKey={1}"
        , body.CurrencyType
        , Pay.GPayBackendKey
        );

        sign = CodingControl.GetSHA256(strSign);

        #endregion

        if (sign.ToUpper() == body.Sign.ToUpper()) {
            GatewayCommon.Provider provider;

            foreach (var item in body.ArrayProviderCode) {
                provider = GatewayCommon.ToList<GatewayCommon.Provider>(RedisCache.ProviderCode.GetProviderCode(item)).FirstOrDefault();

                //if (((GatewayCommon.ProviderAPIType)provider.ProviderAPIType & GatewayCommon.ProviderAPIType.QueryBalance) != GatewayCommon.ProviderAPIType.QueryBalance) {
                //    Ret.Status = ResultStatus.ERR;
                //    Ret.Message = "該廠商不支援此功能";
                //    return Ret;
                //}

                var apiReturn = GatewayCommon.QueryProviderBalance(item, body.CurrencyType);
                var providerBalance = new ProviderBalance();

                providerBalance.ProviderCode = item;

                if (apiReturn != null) {
                    providerBalance.IsProviderSupport = true;
                    providerBalance.AccountBalance = apiReturn.AccountBalance;
                    providerBalance.CashBalance = apiReturn.CashBalance;
                    providerBalance.CurrencyType = body.CurrencyType;
                    providerBalance.ProviderReturn = apiReturn.ProviderReturn;
                } else {
                    providerBalance.IsProviderSupport = false;
                }

                Ret.ArrayProviderBalance.Add(providerBalance);
            }
        } else {
            Ret.Status = ResultStatus.SignErr;
            Ret.Message = "簽名有誤";
        }

        return Ret;
    }

    [HttpPost]
    [ActionName("GetTestSign")]
    public string GetTestSign([FromBody] FromBodyTest body) {
        string strSign;
        string sign;

        if (body.Type == 0) {
            strSign = string.Format("CompanyCode={0}&PaymentSerial={1}&GPayBackendKey={2}"
            , body.CompanyCode
            , body.PaymentSerial
            , Pay.GPayBackendKey
            );

            return sign = CodingControl.GetSHA256(strSign);
        } else if (body.Type == 1) {
            strSign = string.Format("CurrencyType={0}&GPayBackendKey={1}"
             , body.CurrencyType
             , Pay.GPayBackendKey
             );

            return sign = CodingControl.GetSHA256(strSign);
        } else if (body.Type == 2) {
            var companyKey = GatewayCommon.ToList<GatewayCommon.Company>(RedisCache.Company.GetCompanyByCode(body.CompanyCode)).FirstOrDefault().CompanyKey;
            return GatewayCommon.GetGPaySign(body.OrderID, decimal.Parse(body.OrderAmount), DateTime.Parse(body.OrderDate), body.ServiceType, body.CurrencyType, body.CompanyCode, companyKey);
        } else {
            return "error";
        }
    }
    
    [HttpPost]
    [ActionName("GetTestSign2")]
    public string GetTestSign2([FromBody] FromBodyTest body)
    {
        var companyKey = GatewayCommon.ToList<GatewayCommon.Company>(RedisCache.Company.GetCompanyByCode(body.CompanyCode)).FirstOrDefault().CompanyKey;
        return GatewayCommon.GetGPayWithdrawSign(body.OrderID, decimal.Parse(body.OrderAmount), DateTime.Parse(body.OrderDate), body.CurrencyType, body.CompanyCode, companyKey);
    }

    [HttpPost]
    [ActionName("ReSendPaymentByManualPayment")]
    public APIResult ReSendPaymentByManualPayment([FromBody] FromBodyReSendPayment body)
    {
        GatewayCommon.Payment paymentModel;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        APIResult Ret = new APIResult();
        bool companyRequestResult = false;

        #region SignCheck
        string strSign;
        string sign;

        strSign = string.Format("PaymentSerial={0}&GPayBackendKey={1}"
        , body.PaymentSerial
        , Pay.GPayBackendKey
        );

        sign = CodingControl.GetSHA256(strSign);

        #endregion

        #region 檢查Sign

        //簽名檢查
        if (sign != body.Sign)
        {
            Ret.Status = ResultStatus.SignErr;
            Ret.Message = "簽名有誤";

            return Ret;
        }

        paymentModel = PayDB.GetPaymentByPaymentID(body.PaymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();

        #region 單號檢查

        if (!(paymentModel != null && paymentModel.PaymentID != 0))
        {
            Ret.Status = ResultStatus.Invalidate;
            Ret.Message = "查無此單";

            return Ret;
        }

        #endregion

        #region Status 是否已經進入可以使用API下發之狀態


        if (!(paymentModel.ProcessStatus == 2|| paymentModel.ProcessStatus == 4))
        {
            Ret.Status = ResultStatus.Invalidate;
            Ret.Message = "此流程無法補單";
            return Ret;
        }

        #endregion

        System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
        if (!(CompanyDT != null && CompanyDT.Rows.Count > 0))
        {
            Ret.Status = ResultStatus.ERR;
            Ret.Message = "找不到此商户资讯";

            return Ret;
        }

        GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();

        System.Threading.Tasks.Task.Run(() =>
        {
            gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
    
            if (CompanyModel.IsProxyCallBack==0)
            {
                companyRequestResult = GatewayCommon.ReturnCompany4(30, gpayReturn, paymentModel.ProviderCode);
            }
            else
            {
                companyRequestResult = GatewayCommon.ReturnCompany(30, gpayReturn, paymentModel.ProviderCode);
            }

            if (companyRequestResult)
            {
                PayDB.UpdatePaymentComplete(paymentModel.PaymentSerial);
            }
        });

        #endregion


        return Ret;
    }

    [HttpPost]
    [ActionName("ReSendPayment")]
    public APIResult ReSendPayment([FromBody] FromBodyReSendPayment body)
    {
        GatewayCommon.Payment paymentModel;
        GatewayCommon.GPayReturn gpayReturn = new GatewayCommon.GPayReturn() { SetByPaymentRetunData = new GatewayCommon.SetByPaymentRetunData() };
        APIResult Ret = new APIResult();
        bool companyRequestResult = false;

        #region SignCheck
        string strSign;
        string sign;

        strSign = string.Format("PaymentSerial={0}&GPayBackendKey={1}"
        , body.PaymentSerial
        , Pay.GPayBackendKey
        );

        sign = CodingControl.GetSHA256(strSign);

        #endregion

        #region 檢查Sign

        //簽名檢查
        if (sign != body.Sign)
        {
            Ret.Status = ResultStatus.SignErr;
            Ret.Message = "簽名有誤";

            return Ret;
        }

        paymentModel = PayDB.GetPaymentByPaymentID(body.PaymentSerial).ToList<GatewayCommon.Payment>().FirstOrDefault();

        #region 單號檢查

        if (!(paymentModel != null && paymentModel.PaymentID != 0))
        {
            Ret.Status = ResultStatus.Invalidate;
            Ret.Message = "查無此單";

            return Ret;
        }

        #endregion

        #region Status 是否已經進入可以使用API下發之狀態


        if (paymentModel.ProcessStatus != 2)
        {


            Ret.Status = ResultStatus.Invalidate;
            Ret.Message = "此流程無法補單";
            return Ret;

        }

        #endregion

        System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(paymentModel.forCompanyID, true);
        if (!(CompanyDT != null && CompanyDT.Rows.Count > 0))
        {
            Ret.Status = ResultStatus.ERR;
            Ret.Message = "找不到此商户资讯";

            return Ret;
        }
        
        GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();

        if (paymentModel.ProcessStatus == 2 || paymentModel.ProcessStatus == 4)
        {
            gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Successs);
        }
        else if (paymentModel.ProcessStatus == 3)
        {
            gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.Failure);
        }
        else {
            gpayReturn.SetByPayment(paymentModel, GatewayCommon.PaymentResultStatus.PaymentProgress);
        }
       
      
        if (CompanyModel.IsProxyCallBack==0)
        {
            companyRequestResult = GatewayCommon.ReturnCompany3(gpayReturn, paymentModel.ProviderCode);
        }
        else
        {
            companyRequestResult = GatewayCommon.ReturnCompany2(gpayReturn, paymentModel.ProviderCode);
        }

        #endregion
    
        if (companyRequestResult) {
            PayDB.UpdatePaymentComplete(paymentModel.PaymentSerial);
       
        }
   
        return Ret;
    }

    [HttpPost]
    [ActionName("ReSendWithdraw")]
    public APIResult ReSendWithdraw([FromBody] FromBodyReSendWithdraw body)
    {
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        APIResult Ret = new APIResult();
     
        try
        {
            #region SignCheck
            string strSign;
            string sign;
            GatewayCommon.WithdrawResultStatus WithdrawResultStatus;
            #endregion

            #region 檢查Sign

            strSign = string.Format("WithdrawSerial={0}&GPayBackendKey={1}"
                                    , body.WithdrawSerial
                                    , Pay.GPayBackendKey
                                    );

            sign = CodingControl.GetSHA256(strSign);

            if (sign != body.Sign)
            {
                Ret.Status = ResultStatus.SignErr;
                Ret.Message = "簽名有誤";

                return Ret;
            }

            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(body.WithdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();

            #region 單號檢查

            if (!(withdrawalModel != null && withdrawalModel.WithdrawID != 0))
            {
                Ret.Status = ResultStatus.Invalidate;
                Ret.Message = "查無此單";

                return Ret;
            }

            #endregion

            #region 單號狀態檢查

            if (withdrawalModel.FloatType == 0)
            {
                Ret.Status = ResultStatus.Invalidate;
                Ret.Message = "此單為後台提現單,無法發送API";

                return Ret;
            }

            #endregion
            switch (withdrawalModel.Status)
            {
                case 2:
                    WithdrawResultStatus = GatewayCommon.WithdrawResultStatus.Successs;
                    break;
                case 3:
                    WithdrawResultStatus = GatewayCommon.WithdrawResultStatus.Failure;
                    break;
                case 14:
                    WithdrawResultStatus = GatewayCommon.WithdrawResultStatus.ProblemWithdraw;
                    break;
                default:
                    WithdrawResultStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
                    break;
            }
            gpayReturn.SetByWithdraw(withdrawalModel, WithdrawResultStatus);

            System.Data.DataTable CompanyDT = PayDB.GetCompanyByID(withdrawalModel.forCompanyID, true);
            if (!(CompanyDT != null && CompanyDT.Rows.Count > 0))
            {
                Ret.Status = ResultStatus.ERR;
                Ret.Message = "找不到此商户资讯";

                return Ret;
            }

            GatewayCommon.Company CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(CompanyDT).FirstOrDefault();
            
            if (body.isReSendWithdraw)
            {
     
                //经过代理server 回调
                if (CompanyModel.IsProxyCallBack == 0)
                {
                    //發送一次回調 補單用
                    if (GatewayCommon.ReturnCompanyByWithdraw3(30, gpayReturn, withdrawalModel.ProviderCode))
                    {
                        //修改下游狀態
                        PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                    }
                }
                else {
                    //發送一次回調 補單用
                    if (GatewayCommon.ReturnCompanyByWithdraw2(30, gpayReturn, withdrawalModel.ProviderCode))
                    {
                        //修改下游狀態
                        PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                    }
                }
            }
            else
            {
                //發送三次回調(後台手動發款後用)
                if (CompanyModel.IsProxyCallBack == 0)
                {
                    //發送一次回調 補單用
                    if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, withdrawalModel.ProviderCode))
                    {
                        //修改下游狀態
                        PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                    }
                }
                else
                {
                    //發送一次回調 補單用
                    if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, withdrawalModel.ProviderCode))
                    {
                        //修改下游狀態
                        PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                    }
                }
            }

            #endregion
        }
        catch (Exception ex)
        {
            Ret.Message = ex.Message;
            throw;
        }



        return Ret;
    }

    [HttpPost]
    [ActionName("QueryWithdraw")]
    public APIResult QueryWithdraw([FromBody] FromBodyReSendWithdraw body)
    {
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        APIResult Ret = new APIResult();
        string withdrawSerial = "";
        bool SendCompanyReturn = false;
        GatewayCommon.WithdrawalByProvider withdrawReturn;
        try
        {
            #region SignCheck
            string strSign;
            string sign;

            #endregion

            #region 檢查Sign

            strSign = string.Format("WithdrawSerial={0}&GPayBackendKey={1}"
                                    , body.WithdrawSerial
                                    , Pay.GPayBackendKey
                                    );

            sign = CodingControl.GetSHA256(strSign);

            if (sign != body.Sign)
            {
                Ret.Status = ResultStatus.SignErr;
                Ret.Message = "簽名有誤";

                return Ret;
            }


            #endregion

            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(body.WithdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();

            #region 單號檢查

            if (!(withdrawalModel != null && withdrawalModel.WithdrawID != 0))
            {
                Ret.Status = ResultStatus.Invalidate;
                Ret.Message = "查無此單";
                return Ret;
            }

            #endregion

            #region 單號狀態檢查
            if (!(withdrawalModel.WithdrawType == 1 && withdrawalModel.Status == 1))
            {
                Ret.Status = ResultStatus.Invalidate;
                Ret.Message = "当前订单状态无法查询";
                return Ret;
            }
            #endregion

            withdrawSerial = withdrawalModel.WithdrawSerial;

            withdrawReturn = GatewayCommon.QueryWithdrawalByProvider(withdrawalModel);

            GatewayCommon.WithdrawResultStatus returnStatus = GatewayCommon.WithdrawResultStatus.WithdrawProgress;
            //0=成功/1=失败/2=审核中
            if (withdrawReturn.WithdrawalStatus == 0)
            {
                //2代表已成功且扣除額度,避免重複上分
                if (withdrawalModel.UpStatus != 2)
                {
                    //不修改Withdraw之狀態，預存中調整
                    PayDB.UpdateWithdrawSerialByUpData(2, withdrawReturn.ProviderReturn, withdrawReturn.UpOrderID, withdrawReturn.Amount, withdrawSerial);
                    var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                    switch (intReviewWithdrawal)
                    {
                        case 0:
                            PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, withdrawReturn.ProviderCode);
                            returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                            SendCompanyReturn = true;
                            break;
                        default:
                            //調整訂單為系統失敗單
                            PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                            PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, withdrawReturn.ProviderCode);
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
                    PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, withdrawReturn.ProviderCode);
                    SendCompanyReturn = true;

                }
            }
            else if (withdrawReturn.WithdrawalStatus == 1)
            {
                if (withdrawalModel.UpStatus != 2)
                {
                    PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, withdrawReturn.ProviderCode);
             
                    PayDB.UpdateWithdrawSerialByUpData(2, withdrawReturn.ProviderReturn, withdrawReturn.UpOrderID, withdrawReturn.Amount, withdrawSerial);

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
                    PayDB.InsertPaymentTransferLog("订单完成,供应商通知此单失败,尚未通知商户", 4, withdrawSerial, withdrawReturn.ProviderCode);
                }

                SendCompanyReturn = true;
            }
            else if (withdrawReturn.WithdrawalStatus == 2)
            {
                Ret.Status = ResultStatus.OK;
                Ret.Message = "上游审核中";

            }
            else
            {
                Ret.Status = ResultStatus.ERR;
                Ret.Message = "查询失败";
            }
            if (SendCompanyReturn)
            {
                if (withdrawReturn.WithdrawalStatus == 0 || withdrawReturn.WithdrawalStatus == 1)
                {
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
                            if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, withdrawalModel.ProviderCode))
                            {
                                //修改下游狀態
                                PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                            }
                        }
                        else
                        {
                            //發送一次回調 補單用
                            if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, withdrawalModel.ProviderCode))
                            {
                                //修改下游狀態
                                PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                            }
                        }
                    }
                }
            }
            

        }
        catch (Exception ex)
        {

            Ret.Status = ResultStatus.ERR;
            Ret.Message = "查询失败:" + ex.Message;
            throw;
        }

        return Ret;
    }

    [HttpPost]
    [ActionName("SendWithdraw")]
    public APIResult SendWithdraw([FromBody] FromBodySendWithdrawal body)
    {
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        APIResult Ret = new APIResult() { Status= ResultStatus.ERR };
        string withdrawSerial = "";
        GatewayCommon.ReturnWithdrawByProvider withdrawReturn;        
        try
        {
            #region 檢查Sign
            string strSign;
            string sign;
            strSign = string.Format("WithdrawSerial={0}&GPayBackendKey={1}"
                                    , body.WithdrawSerial
                                    , Pay.GPayBackendKey
                                    );

            sign = CodingControl.GetSHA256(strSign);

            if (sign != body.Sign)
            {
                Ret.Status = ResultStatus.SignErr;
                Ret.Message = "簽名有誤";

                return Ret;
            }

            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(body.WithdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();

            #region 單號檢查

            if (!(withdrawalModel != null && withdrawalModel.WithdrawID != 0))
            {
                Ret.Status = ResultStatus.Invalidate;
                Ret.Message = "查無此單";

                return Ret;
            }

            #endregion

            withdrawSerial = withdrawalModel.WithdrawSerial;

            withdrawReturn = GatewayCommon.SendWithdraw(withdrawalModel);
            //SendStatus; 0=申請失敗/1=申請成功/2=交易已完成
            if (withdrawReturn.SendStatus == 1)
            {   //修改状态为上游审核中
                PayDB.UpdateWithdrawUpStatus(1, withdrawSerial);
                Ret.Status = ResultStatus.OK;
                Ret.Message = "上游審核中";
            }
            else if (withdrawReturn.SendStatus == 2)
            {
                //先將訂單改為進行中
                PayDB.UpdateWithdrawUpStatus(1, withdrawalModel.WithdrawSerial);
                Ret.Message = withdrawReturn.ReturnResult;
                GatewayCommon.WithdrawResultStatus returnStatus;
                TigerPayWithdrawData NotifyBody = JsonConvert.DeserializeObject<TigerPayWithdrawData>(withdrawReturn.ReturnResult);
                if (NotifyBody.result == "00" && NotifyBody.status.ToUpper() == "OK")
                {
                    withdrawalModel = PayDB.GetWithdrawalByWithdrawID(withdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
                    //2代表已成功且扣除額度,避免重複上分
                    if (withdrawalModel.UpStatus != 2)
                    {
                        //不修改Withdraw之狀態，預存中調整
                        PayDB.UpdateWithdrawSerialByUpData(2, JsonConvert.SerializeObject(NotifyBody), NotifyBody.transaction_number, decimal.Parse(NotifyBody.amount), withdrawSerial);
                        var intReviewWithdrawal = PayDB.ReviewWithdrawal(withdrawSerial);
                        switch (intReviewWithdrawal)
                        {
                            case 0:
                                Ret.Status = ResultStatus.OK;
                                Ret.Message = "訂單完成";
                                PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, withdrawalModel.ProviderCode);
                                returnStatus = GatewayCommon.WithdrawResultStatus.Successs;
                                break;
                            default:
                                //調整訂單為系統失敗單
                                PayDB.UpdateWithdrawStatus(14, withdrawSerial);
                                PayDB.InsertPaymentTransferLog("订单有误,系统问题单:" + intReviewWithdrawal, 4, withdrawSerial, withdrawalModel.ProviderCode);
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
                        PayDB.InsertPaymentTransferLog("订单完成,尚未通知商户", 4, withdrawSerial, withdrawalModel.ProviderCode);

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
                            if (GatewayCommon.ReturnCompanyByWithdraw4(30, gpayReturn, withdrawalModel.ProviderCode))
                            {
                                //修改下游狀態
                                PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                            }
                        }
                        else
                        {
                            //發送一次回調 補單用
                            if (GatewayCommon.ReturnCompanyByWithdraw(30, gpayReturn, withdrawalModel.ProviderCode))
                            {
                                //修改下游狀態
                                PayDB.UpdateWithdrawSerialByStatus(2, withdrawalModel.WithdrawID);
                            }
                        }
                    }
                }
        
            }
            else
            {
                Ret.Status = ResultStatus.ERR;
                Ret.Message = withdrawReturn.ReturnResult;
            }

            #endregion
        }
        catch (Exception ex)
        {
            Ret.Status = ResultStatus.ERR;
            Ret.Message = ex.Message;
            throw;
        }

        return Ret;
    }

    [HttpPost]
    [ActionName("GetAutoDistributionGroupWithdraw")]
    public APIResult GetAutoDistributionGroupWithdraw([FromBody] FromBodyReSendWithdraw body)
    {

        AutoDistributionGroupWithdrawData Ret = new AutoDistributionGroupWithdrawData();
        System.Data.DataTable DT;
        List<string> Withdrawals=new List<string>();
        try
        {
            //#region SignCheck
            //string strSign;
            //string sign;

            //#endregion

            //#region 檢查Sign

            //strSign = string.Format("WithdrawSerial={0}&GPayBackendKey={1}"
            //                        , body.WithdrawSerial
            //                        , Pay.GPayBackendKey
            //                        );

            //sign = CodingControl.GetSHA256(strSign);

            //if (sign != body.Sign)
            //{
            //    Ret.Status = ResultStatus.SignErr;
            //    Ret.Message = "簽名有誤";

            //    return Ret;
            //}

            //#endregion

   
            DT = PayDB.GetAutoDistributionGroupWithdraw();

            if (!(DT != null && DT.Rows.Count > 0))
            {
                Ret.Status = ResultStatus.ERR;
                Ret.Message = "No Data";
                return Ret;
            }

            for (int i = 0; i < DT.Rows.Count; i++)
            {
                Withdrawals.Add(DT.Rows[i]["WithdrawSerial"].ToString());
            }

            Ret.WithdrawSerials = Withdrawals;
            Ret.Status = ResultStatus.OK;
        }
        catch (Exception ex)
        {

            Ret.Status = ResultStatus.ERR;
            Ret.Message = "Search Fail:" + ex.Message;
            throw;
        }

        return Ret;
    }

    [HttpPost]
    [ActionName("AutoDistributionGroupWithdraw")]
    public APIResult AutoDistributionGroupWithdraw([FromBody] FromBodyReSendWithdraw body)
    {
        GatewayCommon.Withdrawal withdrawalModel;
        GatewayCommon.GPayReturnByWithdraw gpayReturn = new GatewayCommon.GPayReturnByWithdraw() { SetByWithdrawRetunData = new GatewayCommon.SetByWithdrawRetunData() };
        APIResult Ret = new APIResult();
        GatewayCommon.Company CompanyModel;
        System.Data.DataTable DT;
        int GroupID = 0;
        int spUpdateProxyProviderOrderGroupReturn = -8;
        try
        {

            withdrawalModel = PayDB.GetWithdrawalByWithdrawID(body.WithdrawSerial).ToList<GatewayCommon.Withdrawal>().FirstOrDefault();
       
            #region 單號狀態檢查
            if (!(withdrawalModel.HandleByAdminID == 0 && withdrawalModel.Status == 1))
            {
                Ret.Status = ResultStatus.Invalidate;
                Ret.Message = "Another Withdrawing";
                return Ret;
            }
            #endregion

            DT = RedisCache.Company.GetCompanyByID(withdrawalModel.forCompanyID);

            #region 公司檢查
            if (!(DT != null && DT.Rows.Count > 0))
            {
                Ret.Status = ResultStatus.ERR;
                Ret.Message = "Company Not Exist";
                return Ret;
            }
            //DT = PayDB.GetCompanyByCode(body.CompanyCode, true);


            CompanyModel = GatewayCommon.ToList<GatewayCommon.Company>(DT).FirstOrDefault();
            #endregion
            if (CompanyModel.ProviderGroups != "0")
            {
                GroupID = GatewayCommon.SelectProxyProviderGroupByCompanySelected(withdrawalModel.ProviderCode, withdrawalModel.Amount, CompanyModel.ProviderGroups);
            }
            else
            {
                GroupID = GatewayCommon.SelectProxyProviderGroup(withdrawalModel.ProviderCode, withdrawalModel.Amount);
            }

            if (GroupID != 1)
            {
                spUpdateProxyProviderOrderGroupReturn = PayDB.spUpdateProxyProviderOrderGroupByAdmin(withdrawalModel.WithdrawSerial, GroupID);

                switch (spUpdateProxyProviderOrderGroupReturn)
                {
                    case 0:
                        Ret.Status = ResultStatus.OK;
                        Ret.Message = "Success";
                        break;
                    case -1:
                        Ret.Status = ResultStatus.ERR;
                        Ret.Message = "Lock Fail";
                        break;
                    case -2:
                        Ret.Status = ResultStatus.ERR;
                        Ret.Message = "Another Withdrawing (sp)";
                        break;
                    default:
                        break;
                }
            }
            else {
                Ret.Status = ResultStatus.ERR;
                Ret.Message = "No Group Can Accept Order";
            }     
        }
        catch (Exception ex)
        {

            Ret.Status = ResultStatus.ERR;
            Ret.Message = "Exception:" + ex.Message;
            throw;
        }

        return Ret;
    }

    [HttpPost]
    [ActionName("SetSummaryCompanyByHour")]
    public APIResult SetSummaryCompanyByHour()
    {

        APIResult Ret = new APIResult();
        System.Data.DataTable DT;
        List<string> Withdrawals = new List<string>();
        int DBreturn = -8;
        try
        {
            //#region SignCheck
            //string strSign;
            //string sign;

            //#endregion

            //#region 檢查Sign

            //strSign = string.Format("WithdrawSerial={0}&GPayBackendKey={1}"
            //                        , body.WithdrawSerial
            //                        , Pay.GPayBackendKey
            //                        );

            //sign = CodingControl.GetSHA256(strSign);

            //if (sign != body.Sign)
            //{
            //    Ret.Status = ResultStatus.SignErr;
            //    Ret.Message = "簽名有誤";

            //    return Ret;
            //}

            //#endregion


            DBreturn = PayDB.SetSummaryCompanyByHour();

            if (DBreturn == 0)
            {
                Ret.Status = ResultStatus.OK;
            }
            else {
                Ret.Status = ResultStatus.ERR;
            }
        }
        catch (Exception ex)
        {

            Ret.Status = ResultStatus.ERR;
            Ret.Message = "Search Fail:" + ex.Message;
            throw;
        }

        return Ret;
    }

    #region Result

    public class APIResult {
        public ResultStatus Status;
        public string Message;
    }

    public enum ResultStatus {
        OK = 0,
        ERR = 1,
        SignErr = 2,
        Invalidate = 3,
        Success=4
    }

    public class PaymentAccountingResult : APIResult {
        public string CompanyCode;
        public string PaymentSerial;
        public int ProcessStatus;
        public string CurrencyType;
        public decimal OrderAmount;
        public decimal PaymentAmount;
        public GatewayCommon.PaymentByProvider PaymentByProvider;

    }

    public class AutoDistributionGroupWithdrawData : APIResult
    {
        public List<string> WithdrawSerials;
    }
    

    public class ProviderBalanceResult : APIResult {
        public List<ProviderBalance> ArrayProviderBalance { get; set; }
    }

    public class GetProviderPointListResult : APIResult
    {
        public List<FromBodyQueryProviderPoint> Providers { get; set; }
    }

    public class QueryProviderPointResult : APIResult {
        public GatewayCommon.BalanceByProvider BalanceData { get; set; }
    }

  

    public class ProviderBalance {
        public string ProviderCode { get; set; }
        public string CurrencyType { get; set; }
        //帳戶總餘額
        public decimal AccountBalance { get; set; }
        //可用餘額
        public decimal CashBalance { get; set; }
        public bool IsProviderSupport { get; set; }
        public string ProviderReturn { get; set; }
    }

    public class WithdrawResult : APIResult {
        // 0=即時/1=非即時
        public int SendType;
        public string WithdrawSerial;
        public string UpOrderID;
        public int SendStatus;
        public decimal DidAmount;
        public decimal Balance;
    }
    public class MsgList {
        public List<Msg> List = new List<Msg>();
    }

    public class Msg {
        public int MsgID { set; get; }
        public string MsgContent { set; get; }
    }

    #endregion

    #region FromBody

    public class TigerPayWithdrawData
    {
        public string result { get; set; }
        public string status { get; set; }
        public string transaction_number { get; set; }
        public string currency { get; set; }
        public string amount { get; set; }
        public string fee { get; set; }
    }

    public class FromBodyQueryWithdrawal
    {
        public List<int> LstWithdrawID { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyBalance {
        public List<string> ArrayProviderCode { get; set; }
        public string CurrencyType { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyQueryProviderPoint
    {
        public string CurrencyType { get; set; }
        public string ProviderCode { get; set; }
        public string Sign { get; set; }
    }

public class FromBodyQueryPointByPayment {
        public string CompanyCode { get; set; }
        public string PaymentSerial { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyRequireWithdrawal {
        public string WithdrawSerial { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyReSendPayment
    {
        public string PaymentSerial { get; set; }
        public string Sign { get; set; }
    }

    public class FromBodyReSendWithdraw
    {
        public string WithdrawSerial { get; set; }
        public string Sign { get; set; }
        public bool isReSendWithdraw { get; set; }
    }

    public class FromBodySendWithdrawal
    {
        public string WithdrawSerial { get; set; }
        public string Sign { get; set; }

    }

    

    public class FromBodyTest {
        public string CurrencyType { get; set; }
        public string CompanyCode { get; set; }
        public string PaymentSerial { get; set; }
        public string OrderID { get; set; }
        public string OrderAmount { get; set; }
        public string OrderDate { get; set; }
        public string ServiceType { get; set; }
        public int Type { get; set; }
    }

    #endregion

}
