
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;


/// <summary>
/// ProviderCommon 的摘要描述
/// </summary>
public static class GatewayCommon
{
    #region DTtoClassModel

    public static IList<T> ToList<T>(this DataTable table) where T : new()
    {
        IList<PropertyInfo> properties = typeof(T).GetProperties().ToList();
        IList<T> result = new List<T>();

        //取得DataTable所有的row data
        foreach (var row in table.Rows)
        {
            var item = MappingItem<T>((DataRow)row, properties);
            result.Add(item);
        }

        return result;
    }

    private static T MappingItem<T>(DataRow row, IList<PropertyInfo> properties) where T : new()
    {
        T item = new T();
        foreach (var property in properties)
        {
            if (row.Table.Columns.Contains(property.Name))
            {
                //針對欄位的型態去轉換
                if (property.PropertyType == typeof(DateTime))
                {
                    DateTime dt = new DateTime();
                    if (DateTime.TryParse(row[property.Name].ToString(), out dt))
                    {
                        property.SetValue(item, dt, null);
                    }
                    else
                    {
                        property.SetValue(item, null, null);
                    }
                }
                else if (property.PropertyType == typeof(decimal))
                {
                    decimal val = new decimal();
                    decimal.TryParse(row[property.Name].ToString(), out val);
                    property.SetValue(item, val, null);
                }
                else if (property.PropertyType == typeof(double))
                {
                    double val = new double();
                    double.TryParse(row[property.Name].ToString(), out val);
                    property.SetValue(item, val, null);
                }
                else if (property.PropertyType == typeof(int))
                {
                    int val = new int();
                    int.TryParse(row[property.Name].ToString(), out val);
                    property.SetValue(item, val, null);
                }
                else
                {
                    if (row[property.Name] != DBNull.Value)
                    {
                        property.SetValue(item, row[property.Name], null);
                    }
                }
            }
        }
        return item;
    }
    #endregion

    #region 儲值相關
    //确认是否为此商户下单回调网址
    public static APIResult CheckCompanyWithdrawCallBack(string CompanyCode, string WithdrawCallBackUrl, string OrderID)
    {
        APIResult returnData = new APIResult() { Status = ResultStatus.ERR };

        if (CompanyCode == "bzxigua" || CompanyCode == "333qipai")
        {
            string getWithdrawCallBackUrl = string.Format("{0}?merchantNo={1}&orderNo={2}", WithdrawCallBackUrl, CompanyCode, OrderID);

            try
            {
                string jsonStr = GatewayCommon.RequestGetAPIforProxyServer(getWithdrawCallBackUrl, "", "", "");
                if (!string.IsNullOrEmpty(jsonStr))
                {

                    JObject revjsonObj = JObject.Parse(jsonStr);

                    if (revjsonObj != null && revjsonObj["status"].ToString().ToUpper() == "TRUE")
                    {
                        PayDB.InsertDownOrderTransferLog("确认为商户申请单:" + revjsonObj.ToString(), 2, "", OrderID, CompanyCode, false);
                        returnData.Status = ResultStatus.OK;
                        returnData.Message = revjsonObj["msg"].ToString();
                        return returnData;
                    }
                    else
                    {
                        PayDB.InsertDownOrderTransferLog("(确认为商户申请单)商户回传有误:" + revjsonObj.ToString(), 2, "", OrderID, CompanyCode, true);
                        returnData.Status = ResultStatus.ERR;
                        returnData.Message = "确认订单有误";
                        return returnData;
                    }
                }
                else
                {
                    PayDB.InsertDownOrderTransferLog("(确认为商户申请单)商户回传有误:回传为空值", 2, "", OrderID, CompanyCode, true);
                    returnData.Status = ResultStatus.ERR;
                    returnData.Message = "确认订单有误";
                    return returnData;

                }
            }
            catch (Exception ex)
            {
                PayDB.InsertDownOrderTransferLog("系统错误:" + ex.Message, 2, "", OrderID, CompanyCode, true);
                returnData.Status = ResultStatus.ERR;
                returnData.Message = "系统错误(请联系金流供应商):" + ex.Message;
                return returnData;
                throw;
            }
        }
        else if (CompanyCode == "x955")
        {

            try
            {

                var merchantPublicKey = "<RSAKeyValue><Modulus>y9V0dKp153DL4PsRuEbXlCeVcFs/HIfWf6MqT6SwUig7n5QKKWuldziwy/7KiIg2PvTWHqpaS7GrwSqxq5iCzXhqyMxFSx+3oLXvGGSyS15ZdtvvOt9ZvaE4J3M0yBcY+rqSt2VEQu0iCtfM9+Nv1ldnnSpZBJozO0sNkwbYWrU=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";


                var providerPrivateKey = "<RSAKeyValue><Modulus>oE8xl9Zg6HzWnhMSC1YrmmfUyDFIsP087hZb+95JonxRAAlPW6YYezw6vwOUVfB68+tZvGwGeer/JYp+HdzGg5ILspL3RkwhsrY96l9mlDmK1gpVdLuvN6p7vyNSQCvQgqOjJrlA+kt2L/NjfvkslxeBIhdEya6N9NpxTj66YvM=</Modulus><Exponent>AQAB</Exponent><P>0U3rYVI4Ehmem0cY4CpBJ4UCX5yUZFnsHyuv70kBUytl1xus9zlvr1Oiqxjk/1tUvrcb0Nd9cJMfLtPRNDMR4w==</P><Q>xBMAuvVYag0+XAEb/oC0Zv8YqVZNEze7tfqQS8mKZKNX8oqdYwzKVi1UuK89gyUihQ3vTYM3GHVb+/X0foP3sQ==</Q><DP>pCsF0JP4vtmxegHOFSWPbTv6nJvoGL4fYmOV343XqDCF9K8Uf+VhIJftI16nX1N3qQ9elfQvw7jh4IzrrpHQGQ==</DP><DQ>vHN7NzYKNVvnPKyASHtRaNDz1gWxbLAbhUt/FqhtkE6CgAYEQSgQ7QUCscMUPxEY/YMoJnrgIGzj7OY3iMWz8Q==</DQ><InverseQ>Byv5Q72du2qMuufU8GfI0XkDiw/8qaXeBZmKnp4XqexaNb9pdJN1ucwRAPz9QhHqkJUxy7KKZC3oIjjW3tgtlw==</InverseQ><D>VE6RxEqIGHxe2i8pVDDzKXblnorcscfcXVIA+grDKuK6Loy24XoOcfEQ7BfT0QZxgwoI3WDqXv/JQ1L8VHQhKYdZwJn2emVwgyy0R8n14dIL7hkiYFR4P4ZwOhemOR6xN3d01ehVcvzXo/FSQWRfzTUrO+GYKZrcAUzxjIwlzsE=</D></RSAKeyValue>";

                var rsa = new RSAUtil();

                JObject jobj = new JObject();

                jobj.Add("orderNo", OrderID);
                jobj.Add("merchantNo", CompanyCode);
                var data = rsa.EncryptForX955(merchantPublicKey, jobj.ToString());

                Dictionary<string, string> sendData = new Dictionary<string, string>();
                sendData.Add("merchantNo", CompanyCode);
                sendData.Add("data", data);

                string companyData = GatewayCommon.RequestFormDataConentTypeAPIByProxyServer(WithdrawCallBackUrl, sendData, "", "");
                if (!string.IsNullOrEmpty(companyData))
                {
                    string jsonStr = rsa.DecryptForX955(providerPrivateKey, companyData);
                    JObject revjsonObj = JObject.Parse(jsonStr);

                    if (revjsonObj != null && revjsonObj["status"].ToString().ToUpper() == "TRUE")
                    {
                        PayDB.InsertDownOrderTransferLog("确认为商户申请单:" + revjsonObj.ToString(), 2, "", OrderID, CompanyCode, false);
                        returnData.Status = ResultStatus.OK;
                        returnData.Message = revjsonObj["msg"].ToString();
                        return returnData;
                    }
                    else
                    {
                        PayDB.InsertDownOrderTransferLog("(确认为商户申请单)商户回传有误:" + revjsonObj.ToString(), 2, "", OrderID, CompanyCode, true);
                        returnData.Status = ResultStatus.ERR;
                        returnData.Message = "确认订单有误";
                        return returnData;
                    }
                }
                else
                {
                    PayDB.InsertDownOrderTransferLog("(确认为商户申请单)商户回传有误:回传为空值", 2, "", OrderID, CompanyCode, true);
                    returnData.Status = ResultStatus.ERR;
                    returnData.Message = "确认订单有误";
                    return returnData;

                }
            }
            catch (Exception ex)
            {
                PayDB.InsertDownOrderTransferLog("系统错误:" + ex.Message, 2, "", OrderID, CompanyCode, true);
                returnData.Status = ResultStatus.ERR;
                returnData.Message = "系统错误(请联系金流供应商):" + ex.Message;
                return returnData;
                throw;
            }
        }
        else
        {

            JObject data = new JObject();
            data.Add("OrderID", OrderID);
            data.Add("CompanyCode", CompanyCode);

            try
            {
                string jsonStr = GatewayCommon.RequestJsonAPIforProxyServer(WithdrawCallBackUrl, data.ToString(), OrderID, "");
                if (!string.IsNullOrEmpty(jsonStr))
                {

                    JObject revjsonObj = JObject.Parse(jsonStr);

                    if (revjsonObj != null && revjsonObj["status"].ToString().ToUpper() == "TRUE")
                    {
                        var mag = "反查成功,单号:" + OrderID;
                        var msgProperty = revjsonObj.Property("msg");

                        //check if property exists
                        if (msgProperty != null)
                        {
                            mag = msgProperty.Value.ToString();
                        }

                        PayDB.InsertDownOrderTransferLog("确认为商户申请单:" + revjsonObj.ToString(), 2, "", OrderID, CompanyCode, false);
                        returnData.Status = ResultStatus.OK;
                        returnData.Message = mag;
                        return returnData;
                    }
                    else
                    {

                        PayDB.InsertDownOrderTransferLog("(确认为商户申请单)商户回传有误:" + revjsonObj.ToString(), 2, "", OrderID, CompanyCode, true);
                        returnData.Status = ResultStatus.ERR;
                        returnData.Message = "确认订单有误";
                        return returnData;
                    }
                }
                else
                {
                    PayDB.InsertDownOrderTransferLog("(确认为商户申请单)商户回传有误:回传为空值", 2, "", OrderID, CompanyCode, true);
                    returnData.Status = ResultStatus.ERR;
                    returnData.Message = "确认订单有误";
                    return returnData;

                }
            }
            catch (Exception ex)
            {
                PayDB.InsertDownOrderTransferLog("系统错误:" + ex.Message, 2, "", OrderID, CompanyCode, true);
                returnData.Status = ResultStatus.ERR;
                returnData.Message = "确认订单有误";
                return returnData;
                throw;
            }
        }

    }

    //隨機擇一供應商之渠道
    public static int SelectProviderService(List<Tuple<ProviderService, GPayRelation>> GPaySelectModels)
    {
        //考慮到未來使用Redis之可能，不在SQL中加入OrderAmount相關的條件
        //回傳值
        int returnValue = 0;
        //權重隨機結果
        int randomWeight;
        //總權種數
        int totalWeight = 0;


        foreach (var GPaySelectModel in GPaySelectModels)
        {
            totalWeight += GPaySelectModel.Item2.Weight;
        }
        //產生隨機數，方式可能需要再調整，故此處帶入整個陣列

        System.Random ran = new System.Random(GetRandomSeed());
        randomWeight = (ran.Next(totalWeight)) + 1;

        int calWeight = 0;
        for (int i = 0; i < GPaySelectModels.Count; i++)
        {
            calWeight += GPaySelectModels[i].Item2.Weight;
            if (calWeight >= randomWeight)
            {
                returnValue = i;
                break;
            }
        }

        return returnValue;
    }

    //隨機擇一供應商之渠道
    public static int SelectProviderByWithdraw(List<Tuple<WithdrawLimit, GPayRelation, Provider>> GPaySelectModels)
    {
        //考慮到未來使用Redis之可能，不在SQL中加入OrderAmount相關的條件
        //回傳值
        int returnValue = 0;
        //權重隨機結果
        int randomWeight;
        //總權種數
        int totalWeight = 0;


        foreach (var GPaySelectModel in GPaySelectModels)
        {
            totalWeight += GPaySelectModel.Item2.Weight;
        }
        //產生隨機數，方式可能需要再調整，故此處帶入整個陣列

        System.Random ran = new System.Random(GetRandomSeed());
        randomWeight = new Random().Next(totalWeight);

        int calWeight = 0;
        for (int i = 0; i < GPaySelectModels.Count; i++)
        {
            calWeight += GPaySelectModels[i].Item2.Weight;
            if (calWeight == randomWeight)
            {
                returnValue = i;
                break;
            }
        }

        return returnValue;
    }

    //隨機擇一专属供應商群组
    public static int SelectProxyProviderGroup(string ProxyProviderCode, decimal OrderAmount)
    {
        //回傳值
        int returnValue = 1;
        //權重隨機結果
        int randomWeight;
        //總權種數
        int totalWeight = 0;
        List<ProxyProviderGroup> ProxyProviderGroupModel = null;

        //0=启用/1=停用
        var DT = PayDB.GetProxyProviderGroupByState(ProxyProviderCode, 0);

        if (DT != null && DT.Rows.Count > 0)
        {
            ProxyProviderGroupModel = ToList<ProxyProviderGroup>(DT).ToList();

            ProxyProviderGroupModel = ProxyProviderGroupModel.Where(x => {
                //檢查上下限制
                if (OrderAmount > x.MaxAmount || OrderAmount < x.MinAmount)
                {
                    return false;
                }
                return true;
            }).ToList();

            if (ProxyProviderGroupModel.Count == 0)
            {
                DT = PayDB.GetProxyProviderGroupByState(ProxyProviderCode, 0);
                ProxyProviderGroupModel = ToList<ProxyProviderGroup>(DT).ToList();
            }

            foreach (var SelectModel in ProxyProviderGroupModel)
            {
                totalWeight += SelectModel.Weight;
            }
            //產生隨機數，方式可能需要再調整，故此處帶入整個陣列

            System.Random ran = new System.Random(GetRandomSeed());
            randomWeight = (ran.Next(totalWeight)) + 1;

            int calWeight = 0;
            for (int i = 0; i < ProxyProviderGroupModel.Count; i++)
            {
                calWeight += ProxyProviderGroupModel[i].Weight;
                if (calWeight >= randomWeight)
                {
                    returnValue = ProxyProviderGroupModel[i].GroupID;
                    break;
                }
            }

        }

        return returnValue;
    }

    public static int SelectProxyProviderGroupByCompanySelected(string ProxyProviderCode, decimal OrderAmount, string ProviderGroups)
    {
        //回傳值
        int returnValue = 1;
        //權重隨機結果
        int randomWeight;
        //總權種數
        int totalWeight = 0;
        List<ProxyProviderGroup> ProxyProviderGroupModel = null;

        List<string> LstProviderGroups = ProviderGroups.Split(',').ToList();
        //0=启用/1=停用
        var DT = PayDB.GetProxyProviderGroupByState(ProxyProviderCode, 0);

        if (DT != null && DT.Rows.Count > 0)
        {
            ProxyProviderGroupModel = ToList<ProxyProviderGroup>(DT).ToList();

            ProxyProviderGroupModel = ProxyProviderGroupModel.Where(w => LstProviderGroups.Contains(w.GroupID.ToString())).ToList();

            ProxyProviderGroupModel = ProxyProviderGroupModel.Where(x => {
                //檢查上下限制
                if (OrderAmount > x.MaxAmount || OrderAmount < x.MinAmount)
                {
                    return false;
                }
                return true;
            }).ToList();

            if (ProxyProviderGroupModel.Count == 0)
            {
                DT = PayDB.GetProxyProviderGroupByState(ProxyProviderCode, 0);
                ProxyProviderGroupModel = ToList<ProxyProviderGroup>(DT).ToList();

                foreach (var SelectModel in ProxyProviderGroupModel)
                {
                    totalWeight += SelectModel.Weight;
                }
                //產生隨機數，方式可能需要再調整，故此處帶入整個陣列

                System.Random ran = new System.Random(GetRandomSeed());
                randomWeight = (ran.Next(totalWeight)) + 1;

                int calWeight = 0;
                for (int i = 0; i < ProxyProviderGroupModel.Count; i++)
                {
                    calWeight += ProxyProviderGroupModel[i].Weight;
                    if (calWeight >= randomWeight)
                    {
                        returnValue = ProxyProviderGroupModel[i].GroupID;
                        break;
                    }
                }
            }
            else
            {
                foreach (var SelectModel in ProxyProviderGroupModel)
                {
                    totalWeight += 1;
                }
                //產生隨機數，方式可能需要再調整，故此處帶入整個陣列

                System.Random ran = new System.Random(GetRandomSeed());
                randomWeight = (ran.Next(totalWeight)) + 1;

                int calWeight = 0;
                for (int i = 0; i < ProxyProviderGroupModel.Count; i++)
                {
                    calWeight += 1;
                    if (calWeight >= randomWeight)
                    {
                        returnValue = ProxyProviderGroupModel[i].GroupID;
                        break;
                    }
                }
            }


        }

        return returnValue;
    }

    private static int GetRandomSeed()
    {
        byte[] bytes = new byte[4];
        System.Security.Cryptography.RNGCryptoServiceProvider rng = new System.Security.Cryptography.RNGCryptoServiceProvider();
        rng.GetBytes(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }
    //取得submitData
    public static ProviderRequestData GetProviderRequestData(Payment payment)
    {
        ProviderRequestData retValue = new ProviderRequestData();

        ProviderGateway providerGateway;
        switch (payment.ProviderCode)
        {
            case "GASH":
                providerGateway = new Provider_GASH();
                break;
            case "Nissin":
                providerGateway = new Provider_Nissin();
                break;
            case "AsiaPlay888":
                providerGateway = new Provider_AsiaPlay888();
                break;
            case "YuHong":
                providerGateway = new Provider_YuHong();
                break;
            case "DiDiPay":
                providerGateway = new Provider_DiDiPay();
                break;
            case "DiDiPay2":
                providerGateway = new Provider_DiDiPay2();
                break;
            case "TigerPay":
                providerGateway = new Provider_TigerPay();
                break;
            case "coolemons":
                providerGateway = new Provider_coolemons();
                break;
            case "FeibaoPay":
                providerGateway = new Provider_FeibaoPay();
                break;
            case "FeibaoPayGrabpay":
                providerGateway = new Provider_FeibaoPayGrabpay();
                break;
            case "FeibaoPayPaymaya":
                providerGateway = new Provider_FeibaoPayPaymaya();
                break;
            case "FeibaoPayBank":
                providerGateway = new Provider_FeibaoPayBank();
                break;
            case "FIFIPay":
                providerGateway = new Provider_FIFIPay();
                break;
            case "GCPay":
                providerGateway = new Provider_GCPay();
                break;
            case "GCpay":
                providerGateway = new Provider_GCPay();
                break;
            case "ZINPay":
                providerGateway = new Provider_ZINPay();
                break;
            case "EASYPAY":
                providerGateway = new Provider_EASYPAY();
                break;
            case "CLOUDPAY":
                providerGateway = new Provider_CLOUDPAY();
                break;
            case "PoPay":
                providerGateway = new Provider_PoPay();
                break;
            case "HeroPay":
                providerGateway = new Provider_HeroPay();
                break;
            case "JBPay":
                providerGateway = new Provider_JBPay();
                break;
            case "LUMIPay":
                providerGateway = new Provider_LUMIPay();
                break;
            case "LUMIPay2":
                providerGateway = new Provider_LUMIPay2();
                break;
            case "GstarPay":
                providerGateway = new Provider_GstarPay();
                break;
            case "CPay":
                providerGateway = new Provider_CPay();
                break;
            case "VirtualPay":
                providerGateway = new Provider_VirtualPay();
                break;
            case "AeePay":
                providerGateway = new Provider_AeePay();
                break;
            default:
                return null;
                break;
        }

        retValue.RequestType = providerGateway.GetRequestType();
        retValue.ProviderUrl = providerGateway.GetCompleteUrl(payment);
        retValue.FormDatas = providerGateway.GetSubmitData(payment);

        return retValue;
    }

    public static ProviderRequestData GetProviderRequestData2(Payment payment)
    {
        ProviderRequestData retValue = new ProviderRequestData();

        ProviderGateway providerGateway;
        switch (payment.ProviderCode)
        {
            case "GASH":
                providerGateway = new Provider_GASH();
                break;
            case "TigerPay":
                providerGateway = new Provider_TigerPay();
                break;
            case "Nissin":
                providerGateway = new Provider_Nissin();
                break;
            case "AsiaPlay888":
                providerGateway = new Provider_AsiaPlay888();
                break;
            case "YuHong":
                providerGateway = new Provider_YuHong();
                break;
            case "DiDiPay":
                providerGateway = new Provider_DiDiPay();
                break;
            case "DiDiPay2":
                providerGateway = new Provider_DiDiPay2();
                break;
            case "coolemons":
                providerGateway = new Provider_coolemons();
                break;
            case "FeibaoPay":
                providerGateway = new Provider_FeibaoPay();
                break;
            case "FeibaoPayGrabpay":
                providerGateway = new Provider_FeibaoPayGrabpay();
                break;
            case "FeibaoPayPaymaya":
                providerGateway = new Provider_FeibaoPayPaymaya();
                break;
            case "FeibaoPayBank":
                providerGateway = new Provider_FeibaoPayBank();
                break;
            case "FIFIPay":
                providerGateway = new Provider_FIFIPay();
                break;
            case "GCPay":
                providerGateway = new Provider_GCPay();
                break;
            case "GCpay":
                providerGateway = new Provider_GCPay();
                break;
            case "ZINPay":
                providerGateway = new Provider_ZINPay();
                break;
            case "EASYPAY":
                providerGateway = new Provider_EASYPAY();
                break;
            case "CLOUDPAY":
                providerGateway = new Provider_CLOUDPAY();
                break;
            case "PoPay":
                providerGateway = new Provider_PoPay();
                break;
            case "HeroPay":
                providerGateway = new Provider_HeroPay();
                break;
            case "JBPay":
                providerGateway = new Provider_JBPay();
                break;
            case "LUMIPay":
                providerGateway = new Provider_LUMIPay();
                break;
            case "LUMIPay2":
                providerGateway = new Provider_LUMIPay2();
                break;
            case "GstarPay":
                providerGateway = new Provider_GstarPay();
                break;
            case "CPay":
                providerGateway = new Provider_CPay();
                break;
            case "VirtualPay":
                providerGateway = new Provider_VirtualPay();
                break;
            case "AeePay":
                providerGateway = new Provider_AeePay();
                break;
            default:
                return null;
                break;
        }

        retValue.ProviderUrl = providerGateway.GetCompleteUrl(payment);

        return retValue;
    }


    public static string GetGPaySign(string OrderID, decimal OrderAmount, DateTime OrderDateTime, string ServiceType, string CurrencyType, string CompanyCode, string CompanyKey)
    {
        string sign;
        string signStr = "ManageCode=" + CompanyCode;
        signStr += "&Currency=" + CurrencyType;
        signStr += "&Service=" + ServiceType;
        signStr += "&OrderID=" + OrderID;
        signStr += "&OrderAmount=" + OrderAmount.ToString("#.##");
        signStr += "&OrderDate=" + OrderDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        signStr += "&CompanyKey=" + CompanyKey;

        sign = CodingControl.GetSHA256(signStr, false);
        return sign.ToUpper();
    }


    public static string GetGPaySign2(string OrderID, decimal OrderAmount, DateTime OrderDateTime, string ServiceType, string CurrencyType, string CompanyCode, string CompanyKey, decimal PaymentAmount)
    {
        string sign;
        string signStr = "CompanyCode=" + CompanyCode;
        signStr += "&CurrencyType=" + CurrencyType;
        signStr += "&ServiceType=" + ServiceType;
        signStr += "&OrderID=" + OrderID;
        signStr += "&OrderAmount=" + OrderAmount.ToString("#.##");
        signStr += "&PaymentAmount=" + PaymentAmount.ToString("#.####");
        signStr += "&OrderDate=" + OrderDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        signStr += "&CompanyKey=" + CompanyKey;

        sign = CodingControl.GetSHA256(signStr, false);

        return sign.ToUpper();
    }

    public static string GetGPayWithdrawSign(string OrderID, decimal OrderAmount, DateTime OrderDateTime, string CurrencyType, string CompanyCode, string CompanyKey)
    {
        string sign;
        string signStr = "ManageCode=" + CompanyCode;
        signStr += "&Currency=" + CurrencyType;
        signStr += "&OrderID=" + OrderID;
        signStr += "&OrderAmount=" + OrderAmount.ToString("#.##");
        signStr += "&OrderDate=" + OrderDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        signStr += "&CompanyKey=" + CompanyKey;

        sign = CodingControl.GetSHA256(signStr, false);
        return sign.ToUpper();
    }

    public static string GetGPaySimpleWithdrawSign(string WithdrawSerial, decimal OrderAmount, DateTime OrderDateTime, string CurrencyType, string CompanyCode, string BankCard, string CompanyKey)
    {
        string sign;
        string signStr = "CompanyCode=" + CompanyCode;
        signStr += "&CurrencyType=" + CurrencyType;
        signStr += "&WithdrawSerial=" + WithdrawSerial;
        signStr += "&OrderAmount=" + OrderAmount.ToString("#.##");
        signStr += "&OrderDate=" + OrderDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        signStr += "&BankCard=" + BankCard;
        signStr += "&CompanyKey=" + CompanyKey;

        sign = CodingControl.GetSHA256(signStr, false);

        return sign.ToUpper();
    }

    public static string GetGPayWithdrawSignByManualWithdrawalReview(string WithdrawSerial, decimal OrderAmount, string CurrencyType, string CompanyCode, string CompanyKey)
    {
        string sign;
        string signStr = "CompanyCode=" + CompanyCode;
        signStr += "&CurrencyType=" + CurrencyType;
        signStr += "&WithdrawSerial=" + WithdrawSerial;
        signStr += "&OrderAmount=" + OrderAmount.ToString("#.##");
        signStr += "&CompanyKey=" + CompanyKey;

        sign = CodingControl.GetSHA256(signStr, false);

        return sign.ToUpper();
    }
    #endregion

    #region 代付、下發

    //取得submitData

    public static ReturnWithdrawByProvider SendWithdraw(Withdrawal withdrawal)
    {
        ReturnWithdrawByProvider retValue = new ReturnWithdrawByProvider();
        ProviderGatewayByWithdraw providerGateway;

        switch (withdrawal.ProviderCode)
        {
            case "Nissin":
                providerGateway = new Provider_Nissin();
                break;
            case "AsiaPlay888":
                providerGateway = new Provider_AsiaPlay888();
                break;
            case "YuHong":
                providerGateway = new Provider_YuHong();
                break;
            case "DiDiPay":
                providerGateway = new Provider_DiDiPay();
                break;
            case "DiDiPay2":
                providerGateway = new Provider_DiDiPay2();
                break;
            case "TigerPay":
                providerGateway = new Provider_TigerPay();
                break;
            case "coolemons":
                providerGateway = new Provider_coolemons();
                break;
            case "FeibaoPay":
                providerGateway = new Provider_FeibaoPay();
                break;
            case "FeibaoPayGrabpay":
                providerGateway = new Provider_FeibaoPayGrabpay();
                break;
            case "FeibaoPayPaymaya":
                providerGateway = new Provider_FeibaoPayPaymaya();
                break;
            case "FeibaoPayBank":
                providerGateway = new Provider_FeibaoPayBank();
                break;
            case "FIFIPay":
                providerGateway = new Provider_FIFIPay();
                break;
            case "GCPay":
                providerGateway = new Provider_GCPay();
                break;
            case "GCpay":
                providerGateway = new Provider_GCPay();
                break;
            case "ZINPay":
                providerGateway = new Provider_ZINPay();
                break;
            case "EASYPAY":
                providerGateway = new Provider_EASYPAY();
                break;
            case "CLOUDPAY":
                providerGateway = new Provider_CLOUDPAY();
                break;
            case "PoPay":
                providerGateway = new Provider_PoPay();
                break;
            case "HeroPay":
                providerGateway = new Provider_HeroPay();
                break;
            case "JBPay":
                providerGateway = new Provider_JBPay();
                break;
            case "LUMIPay":
                providerGateway = new Provider_LUMIPay();
                break;
            case "LUMIPay2":
                providerGateway = new Provider_LUMIPay2();
                break;
            case "GstarPay":
                providerGateway = new Provider_GstarPay();
                break;
            case "CPay":
                providerGateway = new Provider_CPay();
                break;
            case "VirtualPay":
                providerGateway = new Provider_VirtualPay();
                break;
            case "AeePay":
                providerGateway = new Provider_AeePay();
                break;
            default:
                return null;
        }
        return providerGateway.SendWithdrawal(withdrawal);
    }


    public static WithdrawalByProvider QueryWithdrawalByProvider(Withdrawal withdrawal)
    {
        WithdrawalByProvider Ret = null;
        ProviderGatewayByWithdraw providerGateway;

        switch (withdrawal.ProviderCode)
        {
            case "Nissin":
                providerGateway = new Provider_Nissin();
                break;
            case "AsiaPlay888":
                providerGateway = new Provider_AsiaPlay888();
                break;
            case "YuHong":
                providerGateway = new Provider_YuHong();
                break;
            case "DiDiPay":
                providerGateway = new Provider_DiDiPay();
                break;
            case "DiDiPay2":
                providerGateway = new Provider_DiDiPay2();
                break;
            case "TigerPay":
                providerGateway = new Provider_TigerPay();
                break;
            case "coolemons":
                providerGateway = new Provider_coolemons();
                break;
            case "FeibaoPay":
                providerGateway = new Provider_FeibaoPay();
                break;
            case "FeibaoPayGrabpay":
                providerGateway = new Provider_FeibaoPayGrabpay();
                break;
            case "FeibaoPayPaymaya":
                providerGateway = new Provider_FeibaoPayPaymaya();
                break;
            case "FeibaoPayBank":
                providerGateway = new Provider_FeibaoPayBank();
                break;
            case "FIFIPay":
                providerGateway = new Provider_FIFIPay();
                break;
            case "GCPay":
                providerGateway = new Provider_GCPay();
                break;
            case "GCpay":
                providerGateway = new Provider_GCPay();
                break;
            case "ZINPay":
                providerGateway = new Provider_ZINPay();
                break;
            case "EASYPAY":
                providerGateway = new Provider_EASYPAY();
                break;
            case "CLOUDPAY":
                providerGateway = new Provider_CLOUDPAY();
                break;
            case "PoPay":
                providerGateway = new Provider_PoPay();
                break;
            case "HeroPay":
                providerGateway = new Provider_HeroPay();
                break;
            case "JBPay":
                providerGateway = new Provider_JBPay();
                break;
            case "LUMIPay":
                providerGateway = new Provider_LUMIPay();
                break;
            case "LUMIPay2":
                providerGateway = new Provider_LUMIPay2();
                break;
            case "GstarPay":
                providerGateway = new Provider_GstarPay();
                break;
            case "CPay":
                providerGateway = new Provider_CPay();
                break;
            case "VirtualPay":
                providerGateway = new Provider_VirtualPay();
                break;
            case "AeePay":
                providerGateway = new Provider_AeePay();
                break;
            default:
                return null;
        }

        Ret = providerGateway.QueryWithdrawal(withdrawal);

        return Ret;
    }


    #endregion

    #region 查單、補單

    public static PaymentByProvider QueryPaymentByProvider(Payment payment)
    {
        PaymentByProvider Ret = null;
        ProviderGateway providerGateway;

        switch (payment.ProviderCode)
        {
            case "GASH":
                providerGateway = new Provider_GASH();
                break;
            case "Nissin":
                providerGateway = new Provider_Nissin();
                break;
            case "AsiaPlay888":
                providerGateway = new Provider_AsiaPlay888();
                break;
            case "YuHong":
                providerGateway = new Provider_YuHong();
                break;
            case "DiDiPay":
                providerGateway = new Provider_DiDiPay();
                break;
            case "DiDiPay2":
                providerGateway = new Provider_DiDiPay2();
                break;
            case "TigerPay":
                providerGateway = new Provider_TigerPay();
                break;
            case "coolemons":
                providerGateway = new Provider_coolemons();
                break;
            case "FeibaoPay":
                providerGateway = new Provider_FeibaoPay();
                break;
            case "FeibaoPayGrabpay":
                providerGateway = new Provider_FeibaoPayGrabpay();
                break;
            case "FeibaoPayPaymaya":
                providerGateway = new Provider_FeibaoPayPaymaya();
                break;
            case "FeibaoPayBank":
                providerGateway = new Provider_FeibaoPayBank();
                break;
            case "FIFIPay":
                providerGateway = new Provider_FIFIPay();
                break;
            case "GCPay":
                providerGateway = new Provider_GCPay();
                break;
            case "GCpay":
                providerGateway = new Provider_GCPay();
                break;
            case "ZINPay":
                providerGateway = new Provider_ZINPay();
                break;
            case "EASYPAY":
                providerGateway = new Provider_EASYPAY();
                break;
            case "CLOUDPAY":
                providerGateway = new Provider_CLOUDPAY();
                break;
            case "PoPay":
                providerGateway = new Provider_PoPay();
                break;
            case "HeroPay":
                providerGateway = new Provider_HeroPay();
                break;
            case "JBPay":
                providerGateway = new Provider_JBPay();
                break;
            case "LUMIPay":
                providerGateway = new Provider_LUMIPay();
                break;
            case "LUMIPay2":
                providerGateway = new Provider_LUMIPay2();
                break;
            case "GstarPay":
                providerGateway = new Provider_GstarPay();
                break;
            case "CPay":
                providerGateway = new Provider_CPay();
                break;
            case "VirtualPay":
                providerGateway = new Provider_VirtualPay();
                break;
            case "AeePay":
                providerGateway = new Provider_AeePay();
                break;
            default:
                return null;
        }


        Ret = providerGateway.QueryPayment(payment);

        return Ret;
    }

    public static BalanceByProvider QueryProviderBalance(string ProviderCode, string Currency)
    {
        BalanceByProvider Ret = null;
        ProviderGateway providerGateway;

        switch (ProviderCode)
        {

            default:
                return null;
        }


        Ret = providerGateway.QueryPoint(Currency);

        return Ret;
    }

    #endregion

    #region 雜項

    //取得供應商設定json
    public static ProviderSetting GetProverderSettingData(string ProviderCode)
    {
        GatewayCommon.ProviderSetting RetValue;
        //初始化設定檔資料
        string path = Pay.ProviderSettingPath + "\\" + (Pay.IsTestSite ? "Test" : "Official") + "\\" + ProviderCode + ".json";
        string jsonContent;
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using (StreamReader sr = new StreamReader(stream))
            {
                jsonContent = sr.ReadToEnd();
            }
        }

        RetValue = JsonConvert.DeserializeObject<GatewayCommon.ProviderSetting>(jsonContent);
        return RetValue;
    }

    public static JArray GetWithdrawBankSettingData(string Currency = "")
    {
        JArray RetValue;
        string path = "";
        //初始化設定檔資料
        if (Currency != "")
        {
            path = Pay.ProviderSettingPath + "\\" + "withdrawBank" + Currency + ".json";
        }
        else
        {
            path = Pay.ProviderSettingPath + "\\" + "withdrawBank.json";
        }

        string jsonContent;
        string jsonArrayContent;
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using (StreamReader sr = new StreamReader(stream))
            {
                jsonContent = sr.ReadToEnd();
            }
        }
        jsonArrayContent = JsonConvert.DeserializeObject<JObject>(jsonContent)["BankCodeSettings"].ToString();
        RetValue = JsonConvert.DeserializeObject<JArray>(jsonArrayContent);
        return RetValue;
    }


    public static JArray GetWithdrawBankSettingData2(string Currency = "")
    {
        JArray RetValue;
        string path = "";
        //初始化設定檔資料
        if (Currency != "")
        {
            path = Pay.ProviderSettingPath + "\\" + "withdrawBank2" + Currency + ".json";
        }
        else
        {
            path = Pay.ProviderSettingPath + "\\" + "withdrawBank2.json";
        }

        string jsonContent;
        string jsonArrayContent;
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using (StreamReader sr = new StreamReader(stream))
            {
                jsonContent = sr.ReadToEnd();
            }
        }
        jsonArrayContent = JsonConvert.DeserializeObject<JObject>(jsonContent)["BankCodeSettings"].ToString();
        RetValue = JsonConvert.DeserializeObject<JArray>(jsonArrayContent);
        return RetValue;
    }

    public static string RequestJsonAPI(string Url, string JsonString, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonString;
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值:" + result, 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static string RequestFormDataConentTypeAPIByAuthorization(string Url, Dictionary<string, string> XmlDic, string PaymentSerial, string ProviderCode, string Authorization)
    {
        string result = null;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Authorization);
                    var formData = new FormUrlEncodedContent(XmlDic);

                    request.Content = formData;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);

                    //return ex.Message;
                }
            }
        }

        return result;
    }

    public static string RequestJsonAPIByAuthorization(string Url, string JsonString, string PaymentSerial, string ProviderCode, string Authorization)
    {
        string result = null;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Authorization);
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonString;
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值:" + result, 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static string RequestGetAPI(string Url, string JsonString, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Url);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static string RequestGetAPIforProxyServer(string Url, string JsonString, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Pay.ProxyServerUrl + "Get");
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容


                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);


                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static string RequestGetAPIforProxyServerJufu(string Url, string JsonString, string PaymentSerial, string ProviderCode, string Authorization)
    {
        string result = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Pay.ProxyServerUrl + "Get");
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容


                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Authorization);

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static string RequestGetAPIforProxyServerHaungxing(string Url, string JsonString, string PaymentSerial, string ProviderCode, string Authorization)
    {
        string result = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Pay.ProxyServerUrl + "Get");
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容


                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Authorization);

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static string QueryPoint_RequestJsonAPIforProxyServer(string Url, string JsonString, string ProviderCode)
    {
        string result = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);

                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonString;
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        }
                        else
                        {

                        }
                    }
                    else
                    {

                    }
                    #endregion

                }
                catch (Exception ex)
                {

                }
            }
        }

        return result;
    }

    public static string RequestJsonAPIforProxyServerByTC(string Url, string JsonString, string PaymentSerial, string ProviderCode, string Authorization)
    {
        string result = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", " api-key " + Authorization);
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonString;
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static string RequestJsonAPIforProxyServerByAuthorization(string Url, string JsonString, string PaymentSerial, string ProviderCode, string Authorization)
    {
        string result = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Authorization);
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonString;
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static string RequestJsonAPIforProxyServer(string Url, string JsonString, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);

                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonString;
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content + ",response body:" + JsonConvert.SerializeObject(response), 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static string RequestJsonAPIforProxyServerLine2(string Url, string JsonString, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl2);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);

                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonString;
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content + ",response body:" + JsonConvert.SerializeObject(response), 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static string RequestJsonAPI2(string Url, string JsonString, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonString;
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        // 取得呼叫完成 API 後的回報內容
                        result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static string RequestXmlAPI(string Url, Dictionary<string, string> XmlDic, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
                    var formData = new FormUrlEncodedContent(XmlDic);

                    request.Content = formData;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }

                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);

                    //return ex.Message;
                }
            }
        }

        return result;
    }

    public static string RequestXmlAPIforProxyServer(string Url, Dictionary<string, string> XmlDic, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
                    var formData = new FormUrlEncodedContent(XmlDic);

                    request.Content = formData;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {

                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }

                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);

                    //return ex.Message;
                }
            }
        }

        return result;
    }

    public static System.Xml.Linq.XElement RequestXmlAPIforProxyServerAndResponseAsXML(string Url, Dictionary<string, string> XmlDic, string PaymentSerial, string ProviderCode)
    {
        System.Xml.Linq.XElement returnXml = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
                    var formData = new FormUrlEncodedContent(XmlDic);

                    request.Content = formData;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            returnXml = System.Xml.Linq.XElement.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode, 2, PaymentSerial, ProviderCode);
                        }

                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);

                    //return ex.Message;
                }
            }
        }

        return returnXml;
    }

    public static System.Xml.Linq.XElement QueryPoint_RequestXmlAPIforProxyServerAndResponseAsXML(string Url, Dictionary<string, string> XmlDic, string ProviderCode)
    {
        System.Xml.Linq.XElement returnXml = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");

                    System.Xml.Linq.XElement el = new System.Xml.Linq.XElement("root",
                        XmlDic.Select(kv => new System.Xml.Linq.XElement(kv.Key, kv.Value)));


                    var httpContent = new StringContent(el.ToString(), Encoding.UTF8, "application/xml");
                    request.Content = httpContent;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            returnXml = System.Xml.Linq.XElement.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

                            //PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode, 2, "", ProviderCode);
                        }
                        else
                        {
                            //PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode, 2, "", ProviderCode);
                        }

                    }
                    else
                    {
                        //PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, "", ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    //PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, "", ProviderCode);

                    //return ex.Message;
                }
            }
        }

        return returnXml;
    }

    public static string RequestXmlAPIByHonor6767(string Url, Dictionary<string, string> XmlDic, string PaymentSerial, string ProviderCode, string Authorization)
    {
        string result = null;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Authorization);
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
                    var formData = new FormUrlEncodedContent(XmlDic);

                    request.Content = formData;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);

                    //return ex.Message;
                }
            }
        }

        return result;
    }



    public static string RequestFormDataConentTypeAPIByFiftySeven(string Url, Dictionary<string, string> XmlDic, string PaymentSerial, string ProviderCode, string signature, string key, string timestamp)
    {
        string result = null;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("key", key);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("signature", signature);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("timestamp", timestamp);
                    var formData = new FormUrlEncodedContent(XmlDic);

                    request.Content = formData;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);

                    //return ex.Message;
                }
            }
        }

        return result;
    }

    public static string RequestFormDataConentTypeAPI(string Url, Dictionary<string, string> XmlDic, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    var formData = new FormUrlEncodedContent(XmlDic);

                    request.Content = formData;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);

                    //return ex.Message;
                }
            }
        }

        return result;
    }

    public static string RequestFormDataConentTypeAPI2(string Url, Dictionary<string, string> XmlDic, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    client.DefaultRequestHeaders.Add("User-Agent", "ewinpay");
                    var formData = new FormUrlEncodedContent(XmlDic);

                    request.Content = formData;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);

                    //return ex.Message;
                }
            }
        }

        return result;
    }

    public static string QueryPoint_RequestFormDataConentTypeAPIByProxyServer(string Url, Dictionary<string, string> XmlDic, string ProviderCode)
    {
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        string result = null;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);
                    var formData = new FormUrlEncodedContent(XmlDic);

                    request.Content = formData;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        }
                        else
                        {

                        }
                    }
                    else
                    {

                    }
                    #endregion

                }
                catch (Exception ex)
                {


                    //return ex.Message;
                }
            }
        }

        return result;
    }


    public static string RequestFormDataConentTypeAPIByProxyServer(string Url, Dictionary<string, string> XmlDic, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);
                    var formData = new FormUrlEncodedContent(XmlDic);

                    request.Content = formData;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);

                    //return ex.Message;
                }
            }
        }

        return result;
    }

    public static string RequestFormDataConentTypeAPIByProxyServerLine2(string Url, Dictionary<string, string> XmlDic, string PaymentSerial, string ProviderCode)
    {
        string result = null;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl2);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                    // Content-Type 用於宣告遞送給對方的文件型態
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", Url);
                    var formData = new FormUrlEncodedContent(XmlDic);

                    request.Content = formData;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {
                            // 取得呼叫完成 API 後的回報內容
                            result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 供应商回传结果:" + result, 2, PaymentSerial, ProviderCode);
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, PaymentSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("供应商回传有误:回传为空值", 2, PaymentSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, PaymentSerial, ProviderCode);

                    //return ex.Message;
                }
            }
        }

        return result;
    }


    #endregion

    #region 回傳營運商

    //各營運商儲值統一回傳

    public static bool ReturnCompany(int sec, GatewayCommon.GPayReturn gPayReturn, string ProviderCode)
    {
        bool result = false;
        int retryCount = 0;

        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(180);

                #region 呼叫遠端 Web API
                retry:
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, gPayReturn.RetunUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("User-Agent", "ewinpay");
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(gPayReturn.SetByPaymentRetunData);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    DateTime StartTime = DateTime.Now;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    DateTime EndTime = DateTime.Now;
                    double seconds = EndTime.Subtract(StartTime).TotalSeconds;
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {

                            // 取得呼叫完成 API 後的回報內容
                            string strResult = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); ;
                            if (strResult.Trim() == "SUCCESS" || strResult.Trim() == "\"SUCCESS\"")
                            {

                                if (seconds >= 2)
                                {
                                    PayDB.InsertPaymentTransferLog("起始結束時間:" + StartTime.ToString("yyyy-MM-dd HH:mm:ss") + "~" + EndTime.ToString("yyyy-MM-dd HH:mm:ss") + "|" + "状态码:" + response.StatusCode + "回調商户成功,秒數:" + seconds, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                                }
                                else
                                {
                                    PayDB.InsertPaymentTransferLog("起始結束時間:" + StartTime.ToString("yyyy-MM-dd HH:mm:ss") + "~" + EndTime.ToString("yyyy-MM-dd HH:mm:ss") + "|" + "状态码:" + response.StatusCode + "回調商户成功", 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                                }

                                result = true;
                            }
                            else
                            {
                                PayDB.InsertPaymentTransferLog("起始結束時間:" + StartTime.ToString("yyyy-MM-dd HH:mm:ss") + "~" + EndTime.ToString("yyyy-MM-dd HH:mm:ss") + "|" + "状态码:" + response.StatusCode + "商户未打印SUCCESS,秒數:" + seconds + ",Result" + strResult, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                                if (retryCount < 2)
                                {
                                    retryCount++;
                                    goto retry;
                                }
                            }
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("起始結束時間:" + StartTime.ToString("yyyy-MM-dd HH:mm:ss") + "~" + EndTime.ToString("yyyy-MM-dd HH:mm:ss") + "|" + "状态码有误:" + response.StatusCode + ", 回传结果:" + JsonConvert.SerializeObject(response), 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                            if (retryCount < 2)
                            {
                                retryCount++;
                                goto retry;
                            }
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("起始結束時間:" + StartTime.ToString("yyyy-MM-dd HH:mm:ss") + "~" + EndTime.ToString("yyyy-MM-dd HH:mm:ss") + "|"+"商户回传有误:回传为空值", 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                        if (retryCount < 2)
                        {
                            retryCount++;
                            goto retry;
                        }
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                    //return result;
                }


            }
        }

        return result;
    }
 
    public static bool ReturnCompany2(GatewayCommon.GPayReturn gPayReturn, string ProviderCode)
    {
        bool result = false;

        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {

                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, gPayReturn.RetunUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("User-Agent", "ewinpay");
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(gPayReturn.SetByPaymentRetunData);
                    PayDB.InsertPaymentTransferLog(json, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        PayDB.InsertPaymentTransferLog("response" + JsonConvert.SerializeObject(response), 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                        if (response.IsSuccessStatusCode == true)
                        {

                            // 取得呼叫完成 API 後的回報內容
                            string strResult = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            if (strResult.Trim() == "SUCCESS" || strResult.Trim() == "\"SUCCESS\"")
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",回調商户成功", 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                                result = true;
                            }
                            else
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",商户未打印SUCCESS," + strResult, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                                result = false;
                            }
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 回传结果:" + JsonConvert.SerializeObject(response), 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                            result = false;
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("商户回传有误:回传为空值", 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                        result = false;
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误2:" + ex.Message, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                    //return result;
                }


            }
        }

        return result;
    }
    public static bool ReturnCompany3(GatewayCommon.GPayReturn gPayReturn, string ProviderCode)
    {
        bool result = false;

        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {

                    #region 呼叫遠端 Web API

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("User-Agent", "ewinpay");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", gPayReturn.RetunUrl);
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(gPayReturn.SetByPaymentRetunData);

                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果

                    if (response != null)
                    {

                        if (response.IsSuccessStatusCode == true)
                        {

                            // 取得呼叫完成 API 後的回報內容
                            string strResult = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                            if (strResult.Trim() == "SUCCESS" || strResult.Trim() == "\"SUCCESS\"")
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",回調商户成功", 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                                result = true;
                            }
                            else
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",商户未打印SUCCESS," + strResult + ",returnData:" + json, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                                result = false;
                            }


                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ", 回传结果:" + JsonConvert.SerializeObject(response), 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                            result = false;
                        }

                        PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + JsonConvert.SerializeObject(response), 2, "", "");
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("商户回传有误:回传为空值", 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                        result = false;
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                    //return result;
                }


            }
        }

        return result;
    }

    public static bool ReturnCompany4(int sec, GatewayCommon.GPayReturn gPayReturn, string ProviderCode)
    {
        bool result = false;
        int retryCount = 0;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;

        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(180);

                #region 呼叫遠端 Web API
                retry:
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("User-Agent", "ewinpay");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", gPayReturn.RetunUrl);
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(gPayReturn.SetByPaymentRetunData);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    DateTime StartTime = DateTime.Now;
                    response = client.SendAsync(request).GetAwaiter().GetResult();
                    DateTime EndTime = DateTime.Now;

                    double seconds = EndTime.Subtract(StartTime).TotalSeconds;
                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {

                            // 取得呼叫完成 API 後的回報內容
                            string strResult = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); ;
                            if (strResult.Trim() == "SUCCESS" || strResult.Trim() == "\"SUCCESS\"")
                            {
                                if (seconds >= 2)
                                {
                                    PayDB.InsertPaymentTransferLog("起始結束時間:"+ StartTime.ToString("yyyy-MM-dd HH:mm:ss")+"~"+ EndTime.ToString("yyyy-MM-dd HH:mm:ss") + "|"+"状态码:" + response.StatusCode + "回調商户成功,秒數:"+ seconds, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                                }
                                else {
                                    PayDB.InsertPaymentTransferLog("起始結束時間:" + StartTime.ToString("yyyy-MM-dd HH:mm:ss") + "~" + EndTime.ToString("yyyy-MM-dd HH:mm:ss") + "|" + "状态码:" + response.StatusCode + "回調商户成功", 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                                }
                               
                                result = true;
                            }
                            else
                            {
                                PayDB.InsertPaymentTransferLog("起始結束時間:" + StartTime.ToString("yyyy-MM-dd HH:mm:ss") + "~" + EndTime.ToString("yyyy-MM-dd HH:mm:ss") + "|" + "状态码:" + response.StatusCode + "商户未打印SUCCESS,秒數:"+ seconds+ ",Result" + strResult, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                                if (retryCount < 2)
                                {
                                    retryCount++;
                                    goto retry;
                                }
                            }
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("起始結束時間:" + StartTime.ToString("yyyy-MM-dd HH:mm:ss") + "~" + EndTime.ToString("yyyy-MM-dd HH:mm:ss") + "|" + "状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                            if (retryCount < 2)
                            {
                                retryCount++;
                                goto retry;
                            }
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("商户回传有误:回传为空值", 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                        if (retryCount < 2)
                        {
                            retryCount++;
                            goto retry;
                        }
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 2, gPayReturn.SetByPaymentRetunData.PayingSerial, ProviderCode);
                    //return result;
                }


            }
        }

        return result;
    }
    //各營運商代付統一回傳
    public static bool ReturnCompanyByWithdraw(int sec, GatewayCommon.GPayReturnByWithdraw gPayReturn, string ProviderCode)
    {
        bool result = false;
        int retryCount = 0;

        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(90);

                #region 呼叫遠端 Web API
                retry:
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, gPayReturn.RetunUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("User-Agent", "ewinpay");
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(gPayReturn.SetByWithdrawRetunData);

                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {

                            // 取得呼叫完成 API 後的回報內容
                            string strResult = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); ;
                            if (strResult.Trim() == "SUCCESS" || strResult.Trim() == "\"SUCCESS\"")
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",回調商户成功", 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                                result = true;
                            }
                            else
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",商户未打印SUCCESS,result:"+ strResult, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                                if (retryCount < 2)
                                {
                                    retryCount++;
                                    goto retry;
                                }
                            }
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content + "json:" + json, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                            if (retryCount < 2)
                            {
                                retryCount++;
                                goto retry;
                            }
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("商户回传有误:回传为空值", 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                        if (retryCount < 2)
                        {
                            retryCount++;
                            goto retry;
                        }
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);

                }


            }
        }

        return result;
    }
    //各營運商代付統一回傳
    public static bool ReturnCompanyByWithdraw2(int sec, GatewayCommon.GPayReturnByWithdraw gPayReturn, string ProviderCode)
    {
        bool result = false;

        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(sec);

                    #region 呼叫遠端 Web API
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, gPayReturn.RetunUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("User-Agent", "ewinpay");
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(gPayReturn.SetByWithdrawRetunData);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {

                            // 取得呼叫完成 API 後的回報內容
                            string strResult = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); ;
                            if (strResult.Trim() == "SUCCESS" || strResult.Trim() == "\"SUCCESS\"")
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",回調商户成功", 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                                result = true;
                            }
                            else
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",商户未打印SUCCESS," + strResult, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                            }
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("商户回传有误:回传为空值", 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);

                }


            }
        }

        return result;
    }

    public static JObject ReturnCompanyBySimpleWithdraw(int sec, GatewayCommon.GPayReturnBySimpleWithdraw gPayReturn)
    {
        JObject result = new JObject();
        int retryCount = 0;

        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(sec);

                #region 呼叫遠端 Web API
                retry:
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, gPayReturn.RetunUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("User-Agent", "ewinpay");
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(gPayReturn.SimpleWithdrawReturnData);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {

                            // 取得呼叫完成 API 後的回報內容
                            string strResult = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                            result = JsonConvert.DeserializeObject<JObject>(strResult);
                            if (result["Status"].ToString() == "0")

                            {
                                PayDB.InsertPaymentTransferLog("回传资讯:" + strResult + ",回調商户成功", 6, gPayReturn.SimpleWithdrawReturnData.WithdrawSerial, gPayReturn.SimpleWithdrawReturnData.CompanyCode);

                            }
                            else
                            {
                                PayDB.InsertPaymentTransferLog("回传资讯:" + strResult + ",回調商户成功", 6, gPayReturn.SimpleWithdrawReturnData.WithdrawSerial, gPayReturn.SimpleWithdrawReturnData.CompanyCode);
                            }
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 6, gPayReturn.SimpleWithdrawReturnData.WithdrawSerial, gPayReturn.SimpleWithdrawReturnData.CompanyCode);
                            if (retryCount < 2)
                            {
                                retryCount++;
                                goto retry;
                            }
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("商户回传有误: 回传为空值", 6, gPayReturn.SimpleWithdrawReturnData.WithdrawSerial, gPayReturn.SimpleWithdrawReturnData.CompanyCode);
                        if (retryCount < 2)
                        {
                            retryCount++;
                            goto retry;
                        }
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 6, gPayReturn.SimpleWithdrawReturnData.WithdrawSerial, gPayReturn.SimpleWithdrawReturnData.CompanyCode);

                }


            }
        }

        return result;
    }

    public static JObject ReturnCompanyBySimpleWithdrawByProxyServer(int sec, GatewayCommon.GPayReturnBySimpleWithdraw gPayReturn)
    {
        JObject result = new JObject();
        int retryCount = 0;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(sec);

                #region 呼叫遠端 Web API
                retry:
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("User-Agent", "ewinpay");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", gPayReturn.RetunUrl);
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(gPayReturn.SimpleWithdrawReturnData);
                    //PayDB.InsertPaymentTransferLog("senddata:" + json, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, gPayReturn.GPayRetunData.CompanyCode);

                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {

                            // 取得呼叫完成 API 後的回報內容
                            string strResult = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            PayDB.InsertPaymentTransferLog("回传資料:" + strResult, 6, gPayReturn.SimpleWithdrawReturnData.WithdrawSerial, gPayReturn.SimpleWithdrawReturnData.CompanyCode);
                            result = JsonConvert.DeserializeObject<JObject>(strResult);

                            if (result["Status"].ToString() == "0")
                            {
                                PayDB.InsertPaymentTransferLog("回传资讯:" + strResult + ",回調商户成功", 6, gPayReturn.SimpleWithdrawReturnData.WithdrawSerial, gPayReturn.SimpleWithdrawReturnData.CompanyCode);
                            }
                            else
                            {
                                PayDB.InsertPaymentTransferLog("回传资讯:" + strResult + ",回調商户成功", 6, gPayReturn.SimpleWithdrawReturnData.WithdrawSerial, gPayReturn.SimpleWithdrawReturnData.CompanyCode);
                            }
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 6, gPayReturn.SimpleWithdrawReturnData.WithdrawSerial, gPayReturn.SimpleWithdrawReturnData.CompanyCode);
                            if (retryCount < 2)
                            {
                                retryCount++;
                                goto retry;
                            }
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("商户回传有误: 回传为空值", 6, gPayReturn.SimpleWithdrawReturnData.WithdrawSerial, gPayReturn.SimpleWithdrawReturnData.CompanyCode);
                        if (retryCount < 2)
                        {
                            retryCount++;
                            goto retry;
                        }
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 6, gPayReturn.SimpleWithdrawReturnData.WithdrawSerial, gPayReturn.SimpleWithdrawReturnData.CompanyCode);

                }


            }
        }

        return result;
    }

    public static bool ReturnCompanyByWithdraw3(int sec, GatewayCommon.GPayReturnByWithdraw gPayReturn, string ProviderCode)
    {
        bool result = false;

        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(sec);

                    #region 呼叫遠端 Web API
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("User-Agent", "ewinpay");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", gPayReturn.RetunUrl);

                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(gPayReturn.SetByWithdrawRetunData);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {

                            // 取得呼叫完成 API 後的回報內容
                            string strResult = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            if (strResult.Trim() == "SUCCESS" || strResult.Trim() == "\"SUCCESS\"")
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",回調商户成功", 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                                result = true;
                            }
                            else
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",商户未打印SUCCESS," + json, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                            }
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("商户回传有误:回传为空值", 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                }
            }
        }

        return result;
    }

    public static bool ReturnCompanyByWithdraw4(int sec, GatewayCommon.GPayReturnByWithdraw gPayReturn, string ProviderCode)
    {
        bool result = false;
        int retryCount = 0;
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;

        using (HttpClientHandler handler = new HttpClientHandler())
        {
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(90);

                #region 呼叫遠端 Web API
                retry:
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Pay.ProxyServerUrl);
                    HttpResponseMessage response = null;

                    #region  設定相關網址內容

                    // Accept 用於宣告客戶端要求服務端回應的文件型態 (底下兩種方法皆可任選其一來使用)
                    //client.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("User-Agent", "ewinpay");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("DestinationUrl", gPayReturn.RetunUrl);
                    // Content-Type 用於宣告遞送給對方的文件型態
                    //client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");


                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(gPayReturn.SetByWithdrawRetunData);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    response = client.SendAsync(request).GetAwaiter().GetResult();

                    #endregion
                    #endregion

                    #region 處理呼叫完成 Web API 之後的回報結果
                    if (response != null)
                    {
                        if (response.IsSuccessStatusCode == true)
                        {

                            // 取得呼叫完成 API 後的回報內容
                            string strResult = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            if (strResult.Trim() == "SUCCESS" || strResult.Trim() == "\"SUCCESS\"")
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",回調商户成功", 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                                result = true;
                            }
                            else
                            {
                                PayDB.InsertPaymentTransferLog("状态码:" + response.StatusCode + ",商户未打印SUCCESS", 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                                if (retryCount < 2)
                                {
                                    retryCount++;
                                    goto retry;
                                }
                            }
                        }
                        else
                        {
                            PayDB.InsertPaymentTransferLog("状态码有误:" + response.StatusCode + ", 回传结果:" + response.Content + "json:" + json, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                            if (retryCount < 2)
                            {
                                retryCount++;
                                goto retry;
                            }
                        }
                    }
                    else
                    {
                        PayDB.InsertPaymentTransferLog("商户回传有误:回传为空值", 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);
                        if (retryCount < 2)
                        {
                            retryCount++;
                            goto retry;
                        }
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    PayDB.InsertPaymentTransferLog("系统错误:" + ex.Message, 6, gPayReturn.SetByWithdrawRetunData.WithdrawSerial, ProviderCode);

                }


            }
        }

        return result;
    }

    #endregion

    #region 各Class

    #region interface

    //供應商串接介面，新增供應商必須繼承
    public interface ProviderGateway
    {
        //取得代收的PostData
        Dictionary<string, string> GetSubmitData(Payment payment);
        //取得代收的完整Url
        string GetCompleteUrl(Payment payment);
        //代收的ContentType類型
        ProviderRequestType GetRequestType();
        //查詢代收數據
        PaymentByProvider QueryPayment(Payment payment);
        BalanceByProvider QueryPoint(string Currency);
    }

    public interface ProviderGatewayByWithdraw
    {
        WithdrawalByProvider QueryWithdrawal(Withdrawal payment);
        ReturnWithdrawByProvider SendWithdrawal(Withdrawal withdrawal);
    }

    #endregion

    #region Post至供應商相關資料

    public class ProviderRequestData
    {
        public ProviderRequestType RequestType;
        public string ProviderUrl { get; set; }
        public Dictionary<string, string> FormDatas { get; set; }
    }

    public enum ProviderRequestType
    {
        FormData = 0,
        Json = 1,
        RedirectUrl = 2
    }


    public class WithdrawalByProvider
    {
        public string ProviderCode { get; set; }
        public string ProviderReturn { get; set; }
        public decimal Amount { get; set; }
        public int WithdrawalStatus { get; set; }//0=成功/1=失败/2=审核中
        public bool IsQuerySuccess { get; set; }
        public string UpOrderID { get; set; }
    }

    public class PaymentByProvider
    {
        public string ProviderCode { get; set; }
        public string ProviderReturn { get; set; }
        public decimal OrderAmount { get; set; }
        public bool IsPaymentSuccess { get; set; }
        public bool IsQuerySuccess { get; set; }
    }

    public class BalanceByProvider
    {
        public decimal AccountBalance { get; set; }
        public decimal CashBalance { get; set; }
        public string ProviderReturn { get; set; }
    }

    public class ReturnWithdrawByProvider
    {

        public string UpOrderID;
        public int SendStatus;//0=申請失敗/1=申請成功/2=交易已完成
        public decimal DidAmount;
        public decimal Balance;
        public string WithdrawSerial;
        public string ReturnResult;
    }

    #endregion

    #region Provider(含各項設定)

    public class Provider
    {
        public string ProviderCode { get; set; }
        public string ProviderName { get; set; }
        public string Introducer { get; set; }
        public string ProviderUrl { get; set; }
        public int ProviderAPIType { get; set; }
        public int CollectType { get; set; }
        public string MerchantCode { get; set; }
        public string MerchantKey { get; set; }
        public string NotifyAsyncUrl { get; set; }
        public string WithdrawNotifyAsyncUrl { get; set; }
        public string NotifySyncUrl { get; set; }
        public int ProviderState { get; set; }

    }

    [Flags]
    public enum ProviderAPIType
    {
        None = 0x00,
        Payment = 0x01,
        Withdraw = 0x02,
        QueryBalance = 0x04,
        QueryPayment = 0x08,
        RepairPayment = 0x10,
    }

    public class ProviderSetting : Provider
    {
        public string QueryOrderUrl { get; set; }
        public List<string> ProviderIP { get; set; }
        public string NotifyAsyncIP { get; set; }
        public string QueryBalanceUrl { get; set; }
        public string WithdrawUrl { get; set; }
        public string QueryWithdrawUrl { get; set; }
        public string RequestWithdrawIP { get; set; }
        public string ProviderPublicKey { get; set; }
        public string Charset { get; set; }
        public string CallBackUrl { get; set; }
        public ProviderRequestType RequestType { get; set; }
        public List<ServiceSetting> ServiceSettings { get; set; }
        public List<BankCodeSetting> CityCodeSettings { get; set; }
        public List<BankCodeSetting> ProvinceCodeSettings { get; set; }
        public List<BankCodeSetting> BankCodeSettings { get; set; }
        public List<CurrencyTypeSetting> CurrencyTypeSettings { get; set; }
        public List<string> OtherDatas { get; set; }
    }

    public class ServiceSetting
    {
        public string ServiceType { get; set; }
        public string TradeType { get; set; }
        public string UrlType { get; set; }
    }

    public class BankCodeSetting
    {
        public string BankCode { get; set; }
        public string ProviderBankCode { get; set; }
    }

    public class CurrencyTypeSetting
    {
        public string CurrencyType { get; set; }
        public string ProviderCurrencyType { get; set; }
    }

    #endregion

    #region 各model

    public class ProviderService
    {
        public string ProviderCode { get; set; }
        public string ServiceType { get; set; }
        public string CurrencyType { get; set; }
        public decimal CostRate { get; set; }
        public decimal CostCharge { get; set; }
        public decimal MaxOnceAmount { get; set; }
        public decimal MinOnceAmount { get; set; }
        public decimal MaxDaliyAmount { get; set; }
        public int CheckoutType { get; set; }
        public int DeviceType { get; set; }
        public int State { get; set; }
    }

    public class GPayRelation
    {
        public int forCompanyID { get; set; }
        public string ProviderCode { get; set; }
        public string ServiceType { get; set; }
        public string CurrencyType { get; set; }
        public int Weight { get; set; }
    }

    public class Company
    {
        public int CompanyID { get; set; }
        public int CompanyState { get; set; }
        public string CompanyCode { get; set; }
        public string URL { get; set; }
        public int InsideLevel { get; set; }
        public string SortKey { get; set; }
        public DateTime CreateDate { get; set; }
        public int CompanyType { get; set; }
        public string CompanyName { get; set; }
        public decimal CompanyDrawCharge { get; set; }
        public int ParentCompanyID { get; set; }
        public int CreateAdminID { get; set; }
        public string CompanyKey { get; set; }
        public string ContacterName { get; set; }
        public string ContacterMobile { get; set; }
        public int ContacterMethod { get; set; }
        public string ContacterMethodAccount { get; set; }
        public string ContacterEmail { get; set; }
        public int WithdrawType { get; set; }
        public string CheckCompanyWithdrawUrl { get; set; }
        public string AutoWithdrawalServiceType { get; set; }
        public int IsProxyCallBack { get; set; }
        public int WithdrawAPIType { get; set; }
        public int ProviderGroupID { get; set; }
        public int BackendWithdrawType { get; set; }
        public int CheckCompanyWithdrawType { get; set; }
        public string ProviderGroups { get; set; }

    }

    public class GPayWithdrawRelation
    {
        public int forCompanyID { get; set; }
        public string ProviderCode { get; set; }
        public string ServiceType { get; set; }
        public string CurrencyType { get; set; }
        public int Weight { get; set; }
    }

    public class CompanyService
    {
        public int forCompanyID { get; set; }
        public string ServiceType { get; set; }
        public string CurrencyType { get; set; }
        public decimal CollectRate { get; set; }
        public decimal CollectCharge { get; set; }
        public decimal MaxOnceAmount { get; set; }
        public decimal MinOnceAmount { get; set; }
        public decimal MaxDaliyAmount { get; set; }
        public int DeviceType { get; set; }
        public int State { get; set; }
    }

    //交易所需的基本資料
    public class Payment
    {
        public int PaymentID { get; set; }
        public int forCompanyID { get; set; }
        public string PaymentSerial { get; set; }
        public string CurrencyType { get; set; }
        public string ServiceType { get; set; }
        public string BankCode { get; set; }
        public string ProviderCode { get; set; }
        public int ProcessStatus { get; set; }
        public string ReturnURL { get; set; }
        public string State { get; set; }
        public string BankSequenceID { get; set; }
        public string ClientIP { get; set; }
        public string UserIP { get; set; }
        public string OrderID { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal PaymentAmount { get; set; }
        public DateTime CreateDate { get; set; }
        public decimal CostRate { get; set; }
        public decimal CostCharge { get; set; }
        public decimal CollectRate { get; set; }
        public decimal CollectCharge { get; set; }
        public string ProviderOrderID { get; set; }
        public int Accounting { get; set; }
        public decimal PartialOrderAmount { get; set; }
        public string UserName { get; set; }
    }

    public class Withdrawal
    {
        public int WithdrawID { get; set; }
        // 0=下發(人工)/1=下發(API)
        public int WithdrawType { get; set; }
        public string WithdrawSerial { get; set; }
        public int forCompanyID { get; set; }
        public string ProviderCode { get; set; }
        public string CurrencyType { get; set; }
        public decimal Amount { get; set; }
        public decimal FinishAmount { get; set; }
        public decimal CollectCharge { get; set; }
        public decimal CostCharge { get; set; }
        // 下發狀態，0=建立/1=進行中/2=成功/3=失敗/4=審核確認中
        public int Status { get; set; }
        public string BankCard { get; set; }
        public string BankCardName { get; set; }
        public string BankName { get; set; }
        public string BankBranchName { get; set; }
        public string OwnProvince { get; set; }
        public string OwnCity { get; set; }
        public string ServiceType { get; set; }
        //代付相關
        public int DownStatus { get; set; }
        public string DownUrl { get; set; }
        public string DownOrderID { get; set; }
        public DateTime DownOrderDate { get; set; }
        public string DownClientIP { get; set; }
        //API下發相關，(代付必定會使用API下發)
        public int UpStatus { get; set; }
        public string UpResult { get; set; }
        public string UpOrderID { get; set; }
        public decimal UpDidAmount { get; set; }
        public int UpAccounting { get; set; }
        public int HandleByAdminID { get; set; }
        public int ConfirmByAdminID { get; set; }
        public DateTime FinishDate { get; set; }
        public DateTime CreateDate { get; set; }
        public int FloatType { get; set; } //0=後台申請提現單=>後台審核/1=API申請代付=>後台審核/2=API申請代付=>不經後台審核
        public string State { get; set; }
    }

    public class WithdrawLimit
    {
        public int WithdrawLimitID { get; set; }
        public string CurrencyType { get; set; }
        public int WithdrawLimitType { get; set; }
        public string ProviderCode { get; set; }
        public int forCompanyID { get; set; }
        public decimal MaxLimit { get; set; }
        public decimal MinLimit { get; set; }
        public decimal Charge { get; set; }
    }

    public class ProxyProvider
    {
        public string forProviderCode { get; set; }
        public decimal Charge { get; set; }
        public decimal Rate { get; set; }
        public decimal ProxyProviderPoint { get; set; }
        public decimal CanUsePoint { get; set; }
        public decimal MaxWithdrawalAmount { get; set; }

    }

    public class ProxyProviderGroup
    {
        public string forProviderCode { get; set; }
        public decimal ProxyProviderPoint { get; set; }
        public decimal CanUsePoint { get; set; }
        public int GroupID { get; set; }
        public int Weight { get; set; }
        public string GroupName { get; set; }
        public DateTime CreateDate { get; set; }
        public int State { get; set; }
        public string CreateDate2 { get; set; }
        public string GroupAccounts { get; set; }
        public decimal MinAmount { get; set; }
        public decimal MaxAmount { get; set; }
    }

    public class CompanyPoint
    {
        public int forCompanyID { get; set; }
        public string CurrencyType { get; set; }
        public decimal PointValue { get; set; }
        public decimal CanUsePoint { get; set; }
        public decimal FrozenPoint { get; set; }
    }

    public class CompanyServicePoint
    {
        public int CompanyID { get; set; }
        public string CurrencyType { get; set; }
        public string ServiceType { get; set; }
        public decimal SystemPointValue { get; set; }
        public decimal MaxLimit { get; set; }
        public decimal MinLimit { get; set; }
        public decimal FrozenPoint { get; set; }
        public decimal CanUsePoint { get; set; }
        public decimal Charge { get; set; }
        public string CompanyName { get; set; }
        public string ServiceTypeName { get; set; }

    }


    #endregion



    #endregion

    #region 回傳營運商

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

    public class SimpleWithdrawAPIResult
    {
        public int Status { get; set; }
        public string Message { get; set; }
    }

    public enum SimpleWithdrawResultStatus
    {
        OK = 0,
        UserBalanceError = 1,
        SignError = 2,
        OrderStatusError = 3,
        OrderNotExist = 4,
        OtherError = 99
    }

    #region Payment

    //交易所需的基本資料
    public enum PaymentResultStatus
    {
        Successs = 0,
        Failure = 1,
        PaymentProgress = 2,
        ProblemPayment = 3
        //CompanyCodeNotFound = 3,
        //SignFail = 4,
        //PaymentNotFound = 5,
        //SystemFailure = 99
    }

    public class GPayRetunData : APIResult
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
        public string State { get; set; }
        public string Sign { get; set; }
        public string Sign2 { get; set; }
    }
    public class SetByPaymentRetunData : APIResult
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
        public string State { get; set; }
        public string Sign { get; set; }
        public string Sign2 { get; set; }
    }

    public class GPayReturn
    {
        //public GPayRetunData GPayRetunData { get; set; }
        public SetByPaymentRetunData SetByPaymentRetunData { get; set; }
        public string RetunUrl { get; set; }

        public void SetByPayment(Payment payment, PaymentResultStatus paymentStatus)
        {
            Company companyModel = PayDB.GetCompanyByID(payment.forCompanyID, false).ToList<Company>().FirstOrDefault();

            this.RetunUrl = payment.ReturnURL;
            this.SetByPaymentRetunData.PayingSerial = payment.PaymentSerial;
            this.SetByPaymentRetunData.PayingAmount = (payment.PartialOrderAmount == 0 ? payment.OrderAmount : payment.PartialOrderAmount);
            this.SetByPaymentRetunData.OrderAmount = payment.OrderAmount;
            this.SetByPaymentRetunData.PayingStatus = (int)paymentStatus;
            this.SetByPaymentRetunData.Currency = payment.CurrencyType;
            this.SetByPaymentRetunData.Service = payment.ServiceType;
            this.SetByPaymentRetunData.ManageCode = companyModel.CompanyCode;
            this.SetByPaymentRetunData.BankID = payment.BankSequenceID;
            this.SetByPaymentRetunData.OrderID = payment.OrderID;
            this.SetByPaymentRetunData.State = payment.State;
            this.SetByPaymentRetunData.OrderDate = payment.OrderDate.ToString("yyyy-MM-dd HH:mm:ss");
            this.SetByPaymentRetunData.Sign = GatewayCommon.GetGPaySign(SetByPaymentRetunData.OrderID, SetByPaymentRetunData.OrderAmount, payment.OrderDate, SetByPaymentRetunData.Service, SetByPaymentRetunData.Currency, SetByPaymentRetunData.ManageCode, companyModel.CompanyKey);


        }
    }

    #endregion

    #region Withdraw

    //交易所需的基本資料
    public enum WithdrawResultStatus
    {
        Successs = 0,
        Failure = 1,
        WithdrawProgress = 2,
        ProblemWithdraw = 3
        //CompanyCodeNotFound = 3,
        //SignFail = 4,
        //PaymentNotFound = 5,
        //SystemFailure = 99
    }

    public class GPayRetunDataByWithdraw
    {
        public string WithdrawSerial { get; set; }
        public int WithdrawStatus { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal WithdrawAmount { get; set; }
        public decimal WithdrawCharge { get; set; }
        public string CurrencyType { get; set; }
        public string CompanyCode { get; set; }
        public string ServiceType { get; set; }
        public string OrderID { get; set; }
        public string OrderDate { get; set; }
        public string Sign { get; set; }
    }
    public class SetByWithdrawRetunData
    {
        public string WithdrawSerial { get; set; }
        public int WithdrawStatus { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal WithdrawAmount { get; set; }
        public decimal WithdrawCharge { get; set; }
        public string Currency { get; set; }
        public string ManageCode { get; set; }
        public string Service { get; set; }
        public string OrderID { get; set; }
        public string OrderDate { get; set; }
        public string State { get; set; }
        public string Sign { get; set; }
    }

    public class GPayRetunDataBySimpleWithdraw
    {
        public string CompanyCode { get; set; }
        public string CurrencyType { get; set; }
        public string WithdrawSerial { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal WithdrawAmount { get; set; }
        public string DownOrderID { get; set; }
        public string OrderDate { get; set; }
        public string BankCard { get; set; }
        public string BankCardName { get; set; }
        public string BankName { get; set; }
        public string BankBranchName { get; set; }
        public string OwnProvince { get; set; }
        public string ServiceType { get; set; }
        public string OwnCity { get; set; }
        public string Sign { get; set; }
    }

    public class ReturnByRequireWithdraw : APIResult
    {
        public string WithdrawSerial { get; set; }
        public decimal OrderAmount { get; set; }
    }

    public class ReturnByRequirePayment : APIResult
    {
        public string PaymentSerial { get; set; }
        public decimal OrderAmount { get; set; }
        public string Code { get; set; }
        public string Url { get; set; }
    }

    public class ReturnByRequirePayment2 : APIResult
    {
        public string PayingSerial { get; set; }
        public decimal OrderAmount { get; set; }
        public string Url { get; set; }
    }


    public class GPayReturnByWithdraw
    {
        //public GPayRetunDataByWithdraw GPayRetunData { get; set; }
        public SetByWithdrawRetunData SetByWithdrawRetunData { get; set; }
        public string RetunUrl { get; set; }

        public void SetByWithdraw(Withdrawal withdrawal, WithdrawResultStatus withdrawalStatus)
        {
            Company companyModel = PayDB.GetCompanyByID(withdrawal.forCompanyID, false).ToList<Company>().FirstOrDefault();

            this.SetByWithdrawRetunData.Service = withdrawal.ServiceType;
            this.RetunUrl = withdrawal.DownUrl;
            this.SetByWithdrawRetunData.WithdrawSerial = withdrawal.WithdrawSerial;
            this.SetByWithdrawRetunData.WithdrawStatus = (int)withdrawalStatus;
            this.SetByWithdrawRetunData.OrderAmount = withdrawal.Amount;
            this.SetByWithdrawRetunData.WithdrawAmount = withdrawal.FinishAmount;
            this.SetByWithdrawRetunData.WithdrawCharge = withdrawal.CollectCharge;
            this.SetByWithdrawRetunData.Currency = withdrawal.CurrencyType;
            this.SetByWithdrawRetunData.ManageCode = companyModel.CompanyCode;
            this.SetByWithdrawRetunData.State = withdrawal.State;
            this.SetByWithdrawRetunData.OrderID = withdrawal.DownOrderID;
            this.SetByWithdrawRetunData.OrderDate = withdrawal.DownOrderDate.ToString("yyyy-MM-dd HH:mm:ss");
            this.SetByWithdrawRetunData.Sign = GatewayCommon.GetGPayWithdrawSign(SetByWithdrawRetunData.OrderID, SetByWithdrawRetunData.OrderAmount, withdrawal.DownOrderDate, SetByWithdrawRetunData.Currency, SetByWithdrawRetunData.ManageCode, companyModel.CompanyKey);

        }
    }

    public class GPayReturnBySimpleWithdraw
    {
        public GPayRetunDataBySimpleWithdraw SimpleWithdrawReturnData { get; set; }

        public string RetunUrl { get; set; }

        public void SetByWithdraw(Withdrawal withdrawal)
        {
            Company companyModel = PayDB.GetCompanyByID(withdrawal.forCompanyID, false).ToList<Company>().FirstOrDefault();
            this.SimpleWithdrawReturnData.WithdrawAmount = withdrawal.Amount + withdrawal.CollectCharge;
            this.SimpleWithdrawReturnData.CompanyCode = companyModel.CompanyCode;
            this.SimpleWithdrawReturnData.CurrencyType = withdrawal.CurrencyType;
            this.SimpleWithdrawReturnData.WithdrawSerial = withdrawal.WithdrawSerial;
            this.SimpleWithdrawReturnData.OrderAmount = withdrawal.Amount;
            this.SimpleWithdrawReturnData.BankName = withdrawal.BankName;
            this.SimpleWithdrawReturnData.BankCard = withdrawal.BankCard;
            this.SimpleWithdrawReturnData.BankCardName = withdrawal.BankCardName;
            this.SimpleWithdrawReturnData.BankBranchName = withdrawal.BankBranchName;
            this.SimpleWithdrawReturnData.OwnProvince = withdrawal.OwnProvince;
            this.SimpleWithdrawReturnData.OwnCity = withdrawal.OwnCity;
            this.SimpleWithdrawReturnData.ServiceType = withdrawal.ServiceType;
            this.SimpleWithdrawReturnData.DownOrderID = withdrawal.DownOrderID;
            this.SimpleWithdrawReturnData.OrderDate = withdrawal.DownOrderDate.ToString("yyyy-MM-dd HH:mm:ss");
            this.SimpleWithdrawReturnData.Sign = GatewayCommon.GetGPaySimpleWithdrawSign(SimpleWithdrawReturnData.WithdrawSerial, SimpleWithdrawReturnData.OrderAmount, withdrawal.DownOrderDate, SimpleWithdrawReturnData.CurrencyType, SimpleWithdrawReturnData.CompanyCode, SimpleWithdrawReturnData.BankCard, companyModel.CompanyKey);

        }
    }

    #endregion

    #endregion

}