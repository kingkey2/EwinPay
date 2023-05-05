using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;



/// <summary>
/// Provider_AsiaPay 的摘要描述
/// </summary>
public class Provider_GASH : GatewayCommon.ProviderGateway
{

    public Provider_GASH()
    {
        //初始化設定檔資料
        SettingData = GatewayCommon.GetProverderSettingData("GASH");
        _TransactionKey = new CodingControl.TransactionKey() { Key= SettingData.MerchantKey,IV= SettingData.OtherDatas[0], Password= SettingData.OtherDatas[1]};
    }

    Dictionary<string, string> GatewayCommon.ProviderGateway.GetSubmitData(GatewayCommon.Payment payment)
    {
   
        string sign="";

        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        string CUID = "TWD";
        string strXmlDoc = String.Empty;

        strXmlDoc = String.Empty;
        strXmlDoc += "<?xml version=\"1.0\"?><TRANS>";
        strXmlDoc += "<MSG_TYPE>" + "0100" + "</MSG_TYPE>";
        strXmlDoc += "<PCODE>" + "300000" + "</PCODE>";
        strXmlDoc += "<CID>" + SettingData.MerchantCode + "</CID>";
        strXmlDoc += "<COID>" + payment.PaymentSerial + "</COID>";
        strXmlDoc += "<CUID>" + CUID + "</CUID>";
        strXmlDoc += "<AMOUNT>" + payment.OrderAmount.ToString("0.00") + "</AMOUNT>";
        strXmlDoc += "<ERQC>" + sign + "</ERQC>";
        strXmlDoc += "<RETURN_URL>" + SettingData.NotifySyncUrl + "</RETURN_URL>";
        strXmlDoc += "<ORDER_TYPE>" + "E" + "</ORDER_TYPE>";
        //strXmlDoc += "<ERP_ID>" + szERPID + "</ERP_ID>";
        //strXmlDoc += "<PRODUCT_ID>" + orderProduct + "</PRODUCT_ID>";
        strXmlDoc += "</TRANS>";
       
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(strXmlDoc);

        sign = CodingControl.GetERQC(xmlDoc, _TransactionKey);
        xmlDoc.DocumentElement["ERQC"].InnerText = sign;

        sendDic.Add("data", GetSendData(xmlDoc));

        return sendDic;
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
        GatewayCommon.PaymentByProvider ApplyWithdrawalRet= new GatewayCommon.PaymentByProvider();
        string sign="";
        string CUID = "TWD";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        Ret.IsQuerySuccess = false;
        Ret.IsPaymentSuccess = false;
        Ret.ProviderCode = SettingData.ProviderCode;
        string strXmlDoc = String.Empty;

        strXmlDoc += "<?xml version=\"1.0\"?><TRANS>";
        strXmlDoc += "<MSG_TYPE>" + "0100" + "</MSG_TYPE>";
        strXmlDoc += "<PCODE>" + "200000" + "</PCODE>";
        strXmlDoc += "<CID>" + SettingData.MerchantCode + "</CID>";
        strXmlDoc += "<COID>" + payment.PaymentSerial + "</COID>";
        strXmlDoc += "<CUID>" + CUID + "</CUID>";
        strXmlDoc += "<AMOUNT>" + payment.OrderAmount.ToString("0.00") + "</AMOUNT>";
        strXmlDoc += "<ERQC>" + sign + "</ERQC>";
        strXmlDoc += "</TRANS>";

        //Put Data in XML
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(strXmlDoc);

        //Obtain ERQC for Future Verification
        sign = CodingControl.GetERQC(xmlDoc, _TransactionKey);
        xmlDoc.DocumentElement["ERQC"].InnerText = sign;

        string soapBody =
           @"<?xml version=""1.0"" encoding=""utf-8""?>
	<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
	  <soap:Body>
		<getResponse  xmlns=""http://egsys.org/"">
		    <data>{0}</data>
		</getResponse>
	  </soap:Body>
	</soap:Envelope>";

        soapBody = string.Format(soapBody, GetSendData(xmlDoc));

        System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(SettingData.QueryOrderUrl);
        req.Headers.Add("SOAPAction", "\"http://egsys.org/getResponse\"");
        req.ContentType = "text/xml;charset=\"utf-8\"";
        req.Accept = "text/xml";
        req.Method = "POST";

        using (Stream stm = req.GetRequestStream())
        {
            using (StreamWriter stmw = new StreamWriter(stm))
            {
                stmw.Write(soapBody);
            }
        }

        System.Net.WebResponse response = req.GetResponse();
        XmlDocument ResponsexmlDoc = new XmlDocument();
        using (StreamReader sr = new StreamReader(response.GetResponseStream()))
        {
            string responseBody = sr.ReadToEnd();

            ResponsexmlDoc.LoadXml(responseBody);
        }


        var InnerText = ResponsexmlDoc.InnerText;

        try
        {
            if (!string.IsNullOrEmpty(InnerText))
            {
                xmlDoc.LoadXml(Encoding.UTF8.GetString(Convert.FromBase64String(InnerText)));

                //Check whether the Transaction is correlative
                if (xmlDoc.DocumentElement["MSG_TYPE"].InnerText != "0110" || xmlDoc.DocumentElement["PCODE"].InnerText != "200000")
                {
                    Ret.IsPaymentSuccess = false;
                    return Ret;
                }
                else
                {
                    //Verify the ERPC
                    if (!CodingControl.VerifyERPC(ref xmlDoc, xmlDoc.DocumentElement["ERPC"].InnerText, _TransactionKey))
                    {
                        Ret.IsPaymentSuccess = false;
                        return Ret;
                    }
                    else
                    {
                        #region Process Order

                        //Check Order Number
                        if (payment.PaymentSerial != xmlDoc.DocumentElement["COID"].InnerText)
                        {
                            Ret.IsPaymentSuccess = false;
                            return Ret;
                        }
                        else
                        {
                            #region Process Order

                            //Retrieve Response Result
                            string orderRRN = xmlDoc.DocumentElement["RRN"].InnerText;                  //Transaction Number
                            string orderPaymentStatus = xmlDoc.DocumentElement["PAY_STATUS"].InnerText; //Pay Status
                            string orderRCode = xmlDoc.DocumentElement["RCODE"].InnerText;              //Response Code

                            //Order Success
                            if (orderPaymentStatus == "S")
                            {
                                Ret.IsPaymentSuccess = true;
                                return Ret;
                            }
                            //Order Failure
                            else if (orderPaymentStatus == "F" || orderPaymentStatus == "T")
                            {
                                Ret.IsPaymentSuccess = false;
                                return Ret;
                            }
                            //Order in Process
                            else if (orderPaymentStatus == "0" || orderPaymentStatus == "W")
                            {
                                Ret.IsPaymentSuccess = false;
                                return Ret;
                            }
                            else {
                                Ret.IsPaymentSuccess = false;
                                return Ret;
                            }
                            #endregion
                        }
                        #endregion
                    }
                }
            }
            else
            {
                Ret.IsPaymentSuccess = false;
                return Ret;
            }
        }
        catch (Exception ex)
        {
            Ret.IsPaymentSuccess = false;
            return Ret;
            throw;
        }
    }

    public GatewayCommon.PaymentByProvider ApplyWithdrawal(GatewayCommon.Payment payment,string PAID)
    {
        GatewayCommon.PaymentByProvider Ret = new GatewayCommon.PaymentByProvider();
        string sign="";
        string CUID = "TWD";
        Dictionary<string, string> sendDic = new Dictionary<string, string>();
        Ret.IsQuerySuccess = false;
        Ret.IsPaymentSuccess = false;
        Ret.ProviderCode = SettingData.ProviderCode;

        string strXmlDoc = String.Empty;
        strXmlDoc += "<?xml version=\"1.0\"?><TRANS>";
        strXmlDoc += "<MSG_TYPE>" + "0500" + "</MSG_TYPE>";
        strXmlDoc += "<PCODE>" + "300000" + "</PCODE>";
        strXmlDoc += "<CID>" + SettingData.MerchantCode + "</CID>";
        strXmlDoc += "<COID>" + payment.PaymentSerial + "</COID>";
        strXmlDoc += "<CUID>" + CUID + "</CUID>";
        strXmlDoc += "<PAID>" + PAID + "</PAID>";
        strXmlDoc += "<AMOUNT>" + payment.OrderAmount.ToString("0.00") + "</AMOUNT>";
        strXmlDoc += "<ERQC>" + sign + "</ERQC>";
        strXmlDoc += "</TRANS>";

        //Put Data in XML
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(strXmlDoc);

        //Obtain ERQC for Future Verification
        sign = CodingControl.GetERQC(xmlDoc, _TransactionKey);
        xmlDoc.DocumentElement["ERQC"].InnerText = sign;

        string soapBody =
    @"<?xml version=""1.0"" encoding=""utf-8""?>
	<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
	  <soap:Body>
		<getResponse  xmlns=""http://egsys.org/"">
		    <data>{0}</data>
		</getResponse>
	  </soap:Body>
	</soap:Envelope>";

        soapBody = string.Format(soapBody, GetSendData(xmlDoc));

        System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(SettingData.WithdrawUrl);
        req.Headers.Add("SOAPAction", "\"http://egsys.org/getResponse\"");
        req.ContentType = "text/xml;charset=\"utf-8\"";
        req.Accept = "text/xml";
        req.Method = "POST";

        using (Stream stm = req.GetRequestStream())
        {
            using (StreamWriter stmw = new StreamWriter(stm))
            {
                stmw.Write(soapBody);
            }
        }

        System.Net.WebResponse response = req.GetResponse();
        XmlDocument ResponsexmlDoc = new XmlDocument();
        using (StreamReader sr = new StreamReader(response.GetResponseStream()))
        {
            string responseBody = sr.ReadToEnd();

            ResponsexmlDoc.LoadXml(responseBody); 
        }

        var InnerText = ResponsexmlDoc.InnerText;

        try
        {
            if (!string.IsNullOrEmpty(InnerText))
            {
                xmlDoc.LoadXml(Encoding.UTF8.GetString(Convert.FromBase64String(InnerText)));
               
                if (xmlDoc.DocumentElement["MSG_TYPE"].InnerText != "0510" || xmlDoc.DocumentElement["PCODE"].InnerText != "300000")
                {
                    Ret.IsPaymentSuccess = false;
                    return Ret;
                }
                else
                {
                    //Verify the ERPC
                    if (!CodingControl.VerifyERPC(ref xmlDoc, xmlDoc.DocumentElement["ERPC"].InnerText, _TransactionKey))
                    {
                        Ret.IsPaymentSuccess = false;
                        return Ret;
                    }
                    else
                    {
                        #region 請款處理
                        // Check Order Number
                        if (payment.PaymentSerial != xmlDoc.DocumentElement["COID"].InnerText)
                        {
                            Ret.IsPaymentSuccess = false;
                            return Ret;
                        }
                        else
                        {
                            #region Update Response Result
                            string settleRCode = xmlDoc.DocumentElement["RCODE"].InnerText;  //Response Code
                            string settleStatus = (settleRCode == "0000" ? "s" : "f");       //Settle Status
                            #endregion

                            if (settleStatus == "s")
                            {
                                Ret.IsPaymentSuccess = true;
                                return Ret;
                            }
                            else {
                                Ret.IsPaymentSuccess = false;
                                return Ret;
                            }
                        }
                        #endregion
                    }
                }
            }
            else
            {
                Ret.IsPaymentSuccess = false;
                return Ret;
            }
        }
        catch (Exception ex)
        {
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

    public  String GetSendData(XmlDocument xmlDoc)
    {
        StringWriter sw = new StringWriter();
        XmlTextWriter xw = new XmlTextWriter(sw);
        xmlDoc.WriteTo(xw);
        string returnValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(sw.ToString()));
        xw.Close();
        sw.Close();
        return returnValue;
    }

    private GatewayCommon.ProviderSetting SettingData;

    private CodingControl.TransactionKey _TransactionKey;
 
}